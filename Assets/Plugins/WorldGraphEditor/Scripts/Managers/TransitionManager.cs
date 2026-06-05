using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using UnityEngine;
using ColorUtility = UnityEngine.ColorUtility;

namespace WorldGraphEditor
{
    public class TransitionManager : PersistentSingleton<TransitionManager>, ITransitionManager
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
        
        public static event Action OnInitialized;
        public static event Action OnDestroyed;
        public static event Action OnPortEntered;
        public static event Action OnTransitionStarted;
        public static event Action OnSceneLoaded; 
        public static event Action OnTransitionEnded;
        public static event Action OnPortLeaved;

        public bool AutoLoad => _autoLoad;

        public TransitionPassStatusType TransitionPassStatus { get; private set; }

        public Vector3 PlayerSpawnPosition { get; private set; }
        
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
                    WGEConsole.Error($"{nameof(TransitionManager)}.{nameof(Container)} is null");
                    return null;
                }
                
                if (!_container.IsInitialized() && !_container.ContainsErrors())
                    _container.Initialize();
#endif
                
                return _container;
            }
        }
        
        private bool _isTransitionStarted;
        private string _originPortGuid;
        
        private static CancellationTokenSource _cts;
        private RuntimeTransitionData _cashedTransitionData;
        
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
                WGEConsole.Log("Trying to create a new TransitionManager instance while it already exists.");
                return Instance;
            }

            var manager = LoadFromResources();
            Instantiate(manager);
            return manager;
        }
        
        public void GoFrom(string currentPortGuid, bool ignoreShortcuts, TransitionContext context = null)
        {
            var canPass = _container.CanPassTransition(currentPortGuid, ignoreShortcuts, out var status);
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
            
            _originPortGuid = currentPortGuid;
            GoInternal(currentPortGuid, false, context);
        }

        public void GoTo(string currentPortGuid, string targetPortGuid, TransitionContext context = null)
        {
            TransitionPassStatus = TransitionPassStatusType.Allowed;
            
            _originPortGuid = currentPortGuid;
            GoInternal(targetPortGuid, true, context);
        }

        public async Task LoadScene(int buildIndex, TransitionContext context = null)
        {
            _isTransitionStarted = true;
            
            var portEnteredDelay = _portEnteredCallConfig.ResolveDelay(context?.PortEnteredDelay);
            var transitionStartedDelay = _transitionStartedCallConfig.ResolveDelay(context?.TransitionStartedDelay);
            var sceneLoadedDelay = _sceneLoadedCallConfig.ResolveDelay(context?.SceneLoadedDelay);
            var transitionEndedDelay = _transitionEndedCallConfig.ResolveDelay(context?.TransitionEndedDelay);

            try
            {
                await HandleTransitionEventAsync(portEnteredDelay, EventType.OnPortEntered, OnPortEntered);
                _cts.Token.ThrowIfCancellationRequested();
                await HandleTransitionEventAsync(transitionStartedDelay, EventType.OnTransitionStarted, OnTransitionStarted);
                _cts.Token.ThrowIfCancellationRequested();
                await AwaitUtility.LoadSceneAsync(buildIndex, _cts.Token);
                
                PlayerSpawnPosition = GetPlayerSpawnPosition(null);
                
                _cts.Token.ThrowIfCancellationRequested();
                await HandleTransitionEventAsync(sceneLoadedDelay, EventType.OnSceneLoaded, OnSceneLoaded);
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

            OnPortLeaved?.Invoke();
            _isTransitionStarted = false;
        }
        
        [Obsolete]
        public static void RefreshPorts()
        {
        }
        
        /// <summary>
        /// [Obsolete] Use <see cref="RegisterAsyncHandler(EventType, Func{CancellationToken, Task}, int)"/> instead.
        /// </summary>
        [Obsolete("Use RegisterAsyncHandler(EventType, Func<CancellationToken, Task>, int) instead.", true)]
        public static void RegisterAsyncHandler(EventType eventType, Func<Task> func, int priority) { }

        /// <summary>
        /// [Obsolete] Use <see cref="UnregisterAsyncHandler(EventType, Func{CancellationToken, Task})"/> instead.
        /// </summary>
        [Obsolete("Use UnregisterAsyncHandler(EventType, Func<CancellationToken, Task>) instead.", true)]
        public static void UnregisterAsyncHandler(EventType eventType, Func<Task> func) { }

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
                _container.Initialize();
            
            PlayerSpawnPosition = GetDefaultSpawnPosition();
            
            if (_playerPrefab.Enabled)
                InstantiatePlayer(PlayerSpawnPosition);

            _cts = new CancellationTokenSource();
            OnInitialized?.Invoke();
        }
        
        private async void GoInternal(string targetPortGuid, bool isTargetPort, [CanBeNull] TransitionContext context)
        {
            if (_isTransitionStarted)
            {
                WGEConsole.Warning("Transition is already started.");
                return;
            }
            
            _isTransitionStarted = true;
            InputTransitionComponent = FindTransitionComponent(_originPortGuid);
            _cashedTransitionData = _container.GetTransitionData(targetPortGuid, isTargetPort);
            
            var portEnteredDelay = _portEnteredCallConfig.ResolveDelay(context?.PortEnteredDelay);
            var transitionStartedDelay = _transitionStartedCallConfig.ResolveDelay(context?.TransitionStartedDelay);
            var sceneLoadedDelay = _sceneLoadedCallConfig.ResolveDelay(context?.SceneLoadedDelay);
            var transitionEndedDelay = _transitionEndedCallConfig.ResolveDelay(context?.TransitionEndedDelay);

            try
            {
                await HandleTransitionEventAsync(portEnteredDelay, EventType.OnPortEntered, OnPortEntered);
                _cts.Token.ThrowIfCancellationRequested();
                await HandleTransitionEventAsync(transitionStartedDelay, EventType.OnTransitionStarted, OnTransitionStarted);
                _cts.Token.ThrowIfCancellationRequested();
                await AwaitUtility.LoadSceneAsync(_cashedTransitionData.TargetSceneBuildIndex, _cts.Token);
                _cts.Token.ThrowIfCancellationRequested();
                
                OutputTransitionComponent = FindTransitionComponent(_cashedTransitionData.TargetPassageGuid, out var isTransitionCorrect);
                PlayerSpawnPosition = GetPlayerSpawnPosition(OutputTransitionComponent);
                PushData = GetPushData(OutputTransitionComponent);

#if UNITY_EDITOR
                if (_printTransitions || !isTransitionCorrect)
                    PrintTransitionMessage(_cashedTransitionData.TargetPassageGuid, isTransitionCorrect);
#endif
                
                await HandleTransitionEventAsync(sceneLoadedDelay, EventType.OnSceneLoaded, OnSceneLoaded);
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

            OnPortLeaved?.Invoke();
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
                InstantiatePlayer(PlayerSpawnPosition);
            
            OnTransitionEnded?.Invoke();
        }
        
        private void InstantiatePlayer(Vector3 spawnPosition)
        {
            Instantiate(_playerPrefab.Value, spawnPosition, Quaternion.identity);
        }

        private static Vector3 GetPlayerSpawnPosition(ITransitionComponent port)
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
        
        private static ITransitionComponent FindTransitionComponent(string portGuid) => 
            FindTransitionComponent(portGuid, out _);
        
        private static ITransitionComponent FindTransitionComponent(string portGuid, out bool isSceneHasPort)
        {
            var transitionComponents = ObjectUtility.FindObjectsByInterface<ITransitionComponent>();
            ITransitionComponent result = null;
            isSceneHasPort = false;

#if UNITY_EDITOR
            var context = new RefreshContext(Instance);
#endif
            foreach (var transitionComponent in transitionComponents)
            {

#if UNITY_EDITOR
                transitionComponent.Refresh(context);
#endif
                
                var guid = transitionComponent.GetGuid();
                
                if (guid != portGuid) 
                    continue;

                isSceneHasPort = true;
                result = transitionComponent;
                break;
            }
            
            return result;
        }

#if UNITY_EDITOR
        private void PrintTransitionMessage(string targetPort, bool isCorrect)
        {
            var targetPortData = _container.EditorData.GetPortData(targetPort);
            var targetSceneData = _container.EditorData.GetSceneDataByPortGuid(targetPort);
            
            var originPortData = _container.EditorData.GetPortData(_originPortGuid);
            var originSceneData = _container.EditorData.GetSceneDataByPortGuid(_originPortGuid);

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
            _eventManager.Dispose();
            OnDestroyed?.Invoke();
        }
    }
}