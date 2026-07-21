using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using ColorUtility = UnityEngine.ColorUtility;

namespace WorldGraphEditor
{
    public partial class TransitionManager : PersistentSingleton<TransitionManager>, ITransitionManager
    {
        [SerializeField] private WorldGraphContainer _container;
        [Space]
        [SerializeField] private bool _autoLoad = true;
        [SerializeField] private Optional<GameObject> _playerPrefab;
        [Space]
        [SerializeField] private EventCallConfig _portEnteredCallConfig;
        [SerializeField] private EventCallConfig _transitionStartedCallConfig;
        [SerializeField] private EventCallConfig _sceneLoadedCallConfig;
        [SerializeField] private EventCallConfig _transitionEndedCallConfig;
        [Header("Editor Only")]
        [SerializeField] private bool _printTransitions = true;

        public event Action Initialized;
        public event Action PortEntered;
        public event Action TransitionStarted;
        public event Action SceneLoaded;
        public event Action TransitionEnded;
        public event Action PortLeaved;
        public event Action Destroyed;

        public bool AutoLoad => _autoLoad;

        public IWorldGraph Graph { get; private set; }
        
        public TransitionPassStatusType TransitionPassStatus { get; private set; }

        public Vector3 NextSpawnPosition { get; private set; }

        public ITransitionComponent InputTransitionComponent { get; private set; }


        public ITransitionComponent OutputTransitionComponent { get; private set; }

        public PushData PushData { get; private set; }

        public WorldGraphContainer Container
        {
            get
            {
#if UNITY_EDITOR
                if (_container == null)
                {
                    if (!UnityEditor.EditorApplication.isPlaying)
                        return null;
                    
                    WGEConsole.Error($"{nameof(TransitionManager)}.{nameof(Container)} is null");
                    return null;
                }
#endif
                
                return _container;
            }
        }
        
        private bool _isTransitionStarted;

        private static CancellationTokenSource _cts;
        private RuntimeTransitionData _cashedTransitionData;
        private SceneLoader _sceneLoader;
        
        private static readonly EventManager _eventManager = new();
        private static readonly string _managerPath = Path.Combine("TransitionManager");
        
        public static TransitionManager LoadFromResources()
        {
            return Resources.Load<TransitionManager>(_managerPath);
        }

        public static TransitionManager CreateInstance()
        {
            if (Instance != null)
            {
                WGEConsole.Log("Trying to create a new TransitionManager instance while instance already exists.");
                return Instance;
            }

            var manager = LoadFromResources();
            Instantiate(manager);
            return manager;
        }

        public Task GoFromAsync(string currentPortGuid, ITransitionContext context = null)
        {
            return GoFromAsync(currentPortGuid, false, context);
        }

        public async Task GoFromAsync(string currentPortGuid, bool ignoreShortcuts, ITransitionContext context = null)
        {
            var canPass = Graph.CanPassTransition(currentPortGuid, ignoreShortcuts, out var status);
            TransitionPassStatus = status;
                
            if (!canPass)
            {
                var errorReasonMessage = status switch
                {
                    TransitionPassStatusType.BlockedByDirection => "because the connection is \"One-Way\" and you're on the wrong side",
                    TransitionPassStatusType.BlockedByAdditionalPort => $"because connection for \"{currentPortGuid}\" port not exists",
                    _ => "because the \"Shortcut\" is not open"
                };
                
                WGEConsole.Warning($"Transition from this passage is not allowed {errorReasonMessage}.");
                return;
            }
            
            var transitionContext = context as TransitionContext;

            if (Graph.TryGetPassageTransitionData(currentPortGuid, out var data))
            {
                await GoInternalAsync(data, transitionContext);
            }
        }

        public async Task GoToAsync(string currentPortGuid, string targetPortGuid, ITransitionContext context = null)
        {
            TransitionPassStatus = TransitionPassStatusType.Allowed;

            var transitionContext = context as TransitionContext;
            
            var data = Graph.GetTeleportTransitionData(currentPortGuid, targetPortGuid);
            await GoInternalAsync(data, transitionContext);
        }

        public async Task LoadSceneAsync(int buildIndex, TransitionContext context = null)
        {
            _isTransitionStarted = true;
            
            var portEnteredDelay = _portEnteredCallConfig.ResolveDelay(context?.PortEnteredDelay);
            var transitionStartedDelay = _transitionStartedCallConfig.ResolveDelay(context?.TransitionStartedDelay);
            var sceneLoadedDelay = _sceneLoadedCallConfig.ResolveDelay(context?.SceneLoadedDelay);
            var transitionEndedDelay = _transitionEndedCallConfig.ResolveDelay(context?.TransitionEndedDelay);

            try
            {
                await HandleTransitionEventAsync(portEnteredDelay, EventType.OnPortEntered, PortEntered);
                _cts.Token.ThrowIfCancellationRequested();
                await HandleTransitionEventAsync(transitionStartedDelay, EventType.OnTransitionStarted, TransitionStarted);
                _cts.Token.ThrowIfCancellationRequested();
                await AwaitUtility.LoadSceneAsync(buildIndex, _cts.Token);
                
                NextSpawnPosition = GetNextSpawnPosition(null);
                
                _cts.Token.ThrowIfCancellationRequested();
                await HandleTransitionEventAsync(sceneLoadedDelay, EventType.OnSceneLoaded, SceneLoaded);
                _cts.Token.ThrowIfCancellationRequested();
                await HandleTransitionEventAsync(transitionEndedDelay, EventType.OnTransitionEnded, TrySpawnPlayerAndCallOnTransitionEndedEvent);
                _cts.Token.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException e)
            {
                WGEConsole.Info($"Async operations cancelled: {e}");
            }
            catch (Exception e)
            {
                WGEConsole.Error(e.Message);
            }

            PortLeaved?.Invoke();
            _isTransitionStarted = false;
        }

        public static void RegisterAsyncHandler(EventType eventType, Func<CancellationToken, Task> func, int priority)
        {
            _eventManager.Add(eventType, func, priority);
        }

        public static void UnregisterAsyncHandler(EventType eventType, Func<CancellationToken, Task> func)
        {
            _eventManager.Remove(eventType, func);
        }
        
        private void Start()
        {
            if (_container != null)
            {
                Graph = _container.GetWorldGraph();
            }
            
            NextSpawnPosition = GetDefaultSpawnPosition();
            
            if (_playerPrefab.Enabled)
                InstantiatePlayer(NextSpawnPosition);

            _cts = new CancellationTokenSource();
            _sceneLoader = new SceneLoader();
            Initialized?.Invoke();
        }
        
        private async Task GoInternalAsync(RuntimeTransitionData data, TransitionContext context)
        {
            if (_isTransitionStarted)
            {
                WGEConsole.Warning("Transition is already started.");
                return;
            }
            
            _isTransitionStarted = true;
            _cashedTransitionData = data;
            InputTransitionComponent = FindTransitionComponent(_cashedTransitionData.CurrentPassageGuid);
            
            var portEnteredDelay = _portEnteredCallConfig.ResolveDelay(context?.PortEnteredDelay);
            var transitionStartedDelay = _transitionStartedCallConfig.ResolveDelay(context?.TransitionStartedDelay);
            var sceneLoadedDelay = _sceneLoadedCallConfig.ResolveDelay(context?.SceneLoadedDelay);
            var transitionEndedDelay = _transitionEndedCallConfig.ResolveDelay(context?.TransitionEndedDelay);

            try
            {
                await HandleTransitionEventAsync(portEnteredDelay, EventType.OnPortEntered, PortEntered);
                _cts.Token.ThrowIfCancellationRequested();
                await HandleTransitionEventAsync(transitionStartedDelay, EventType.OnTransitionStarted, TransitionStarted);
                _cts.Token.ThrowIfCancellationRequested();
                await _sceneLoader.LoadAsync(_cashedTransitionData, _cts.Token);
                _cts.Token.ThrowIfCancellationRequested();
                
                OutputTransitionComponent = FindTransitionComponent(_cashedTransitionData.TargetPassageGuid);
                NextSpawnPosition  = GetNextSpawnPosition(OutputTransitionComponent);
                PushData = GetPushData(OutputTransitionComponent);

#if UNITY_EDITOR
                if (_printTransitions || OutputTransitionComponent == null)
                    PrintTransitionMessage(OutputTransitionComponent != null);
#endif
                
                await HandleTransitionEventAsync(sceneLoadedDelay, EventType.OnSceneLoaded, SceneLoaded);
                SceneLoaded?.Invoke();
                _cts.Token.ThrowIfCancellationRequested();
                
                await HandleTransitionEventAsync(transitionEndedDelay, EventType.OnTransitionEnded, TrySpawnPlayerAndCallOnTransitionEndedEvent);
                _cts.Token.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException e)
            {
                WGEConsole.Info($"Async operations cancelled: {e.Message}");
            }
            catch (Exception e)
            {
                WGEConsole.Error(e.Message);
            }

            PortLeaved?.Invoke();
            _isTransitionStarted = false;
        }

        private static PushData GetPushData(ITransitionComponent transitionComponent)
        {
            if (transitionComponent is IPusher pusher)
                return pusher.GetPushData();

            return default;
        }

        private void TrySpawnPlayerAndCallOnTransitionEndedEvent()
        {
            if (_playerPrefab.Enabled)
                InstantiatePlayer(NextSpawnPosition);
            
            TransitionEnded?.Invoke();
        }
        
        private void InstantiatePlayer(Vector3 spawnPosition)
        {
            Instantiate(_playerPrefab.Value, spawnPosition, Quaternion.identity);
        }

        private static Vector3 GetNextSpawnPosition(ITransitionComponent port)
        {
            return port?.GetSpawnPosition() ?? GetDefaultSpawnPosition();
        }

        private static Vector3 GetDefaultSpawnPosition()
        {
            var spawnPoint = FindAnyObjectByType<SpawnPointBase>();
            return spawnPoint == null ? Vector3.zero : spawnPoint.GetPosition();
        }

        private static async Task HandleTransitionEventAsync(TransitionDelayData callConfig, EventType eventType, Action action)
        {
            action?.Invoke();
            
            await AwaitUtility.AwaitByCoroutine(callConfig, Instance, _cts.Token);
            await _eventManager.Invoke(eventType, _cts.Token);
        }

        private static ITransitionComponent FindTransitionComponent(string portGuid) => TransitionComponentUtility.FindByGuid(portGuid);

#if UNITY_EDITOR
        private void PrintTransitionMessage(bool isCorrect)
        {
            _container.EditorGraph.TryGetPortData(_cashedTransitionData.TargetPassageGuid, out var targetPortData);
            _container.EditorGraph.TryGetSceneDataByPortGuid(_cashedTransitionData.TargetPassageGuid, out var targetSceneData);
            
            _container.EditorGraph.TryGetPortData(_cashedTransitionData.CurrentPassageGuid, out var originPortData);
            _container.EditorGraph.TryGetSceneDataByPortGuid(_cashedTransitionData.CurrentPassageGuid, out var originSceneData);
            
            var color = isCorrect ? MessageColor.Green.GetColor() : MessageColor.Red.GetColor();
            var hex = ColorUtility.ToHtmlStringRGB(color);

            if (isCorrect)
            {
                WGEConsole.Info(
                    $"Transition <color=#{hex}>[{originSceneData.ScenePath}] / [{originPortData.Name}] --> " +
                    $"[{targetSceneData.ScenePath}] / [{targetPortData.Name}]</color> was completed correctly.");
            }
            else
            {
                WGEConsole.Error(
                    $"Transition <color=#{hex}>[{originSceneData.ScenePath}] / [{originPortData.Name}] --> " +
                    $"[{targetSceneData.ScenePath}] / [{targetPortData.Name}]</color> was completed incorrectly. " +
                    $"Output passage <color=#{hex}>[{targetPortData.Name}]</color> doesn't exist in the current scene. Target port guid: {targetSceneData.Guid}");
            }
        }
#endif

        private void OnDestroy()
        {
            _cts?.Cancel();
            _sceneLoader?.Dispose();
            _sceneLoader = null;
            _eventManager.Dispose();
            Destroyed?.Invoke();
        }
    }
}