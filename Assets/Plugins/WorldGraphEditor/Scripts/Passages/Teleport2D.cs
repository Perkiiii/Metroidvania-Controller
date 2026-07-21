using UnityEngine;

namespace WorldGraphEditor
{
    [RequireComponent(typeof(BoxCollider2D))]
    public class Teleport2D : TeleportBase
    {
        [SerializeField] private Optional<Transform> _customSpawnPosition;
            
        [SerializeField, HideInInspector] private BoxCollider2D _boxCollider;

        private bool _canBeUsed;
        private TransitionManager _manager;
        
        private void OnValidate()
        {
            _boxCollider = GetComponent<BoxCollider2D>();
            _boxCollider.isTrigger = true;
        }

#if UNITY_EDITOR
        public override void Refresh(RefreshContext context)
        {
            base.Refresh(context);
            ApplyName();
        }
        
        private void ApplyName()
        {
            name = $"Teleport to -({TargetName})-";
        }
#endif

        public override Vector3 GetSpawnPosition() =>
            _customSpawnPosition.Enabled ? _customSpawnPosition.Value.position : transform.position;
        
        public void Traverse()
        {
            _ = _manager.GoToAsync(GetGuid(), GetTargetGuid());
        }

        private void OnSceneLoaded()
        {
            if (this.IsOutput(_manager))
                _canBeUsed = false;
        }
        
        private void Awake()
        {
            _canBeUsed = true;
        }
        
        private void OnEnable()
        {
            _manager = TransitionManager.Instance;
            _manager.SceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            _manager.SceneLoaded -= OnSceneLoaded;
        }
        
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_canBeUsed)
                Traverse();
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            _canBeUsed = true;
        }
    }
}