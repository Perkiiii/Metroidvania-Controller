using UnityEngine;

namespace WorldGraphEditor
{
    [RequireComponent(typeof(BoxCollider2D))]
    public class Teleport2D : TeleportBase
    {
        [SerializeField] private Optional<Transform> _customSpawnPosition;
            
        [SerializeField, HideInInspector] private BoxCollider2D _boxCollider;

        private bool _canBeUsed;
        
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
            TransitionManager.Instance.GoTo(GetGuid(), GetTargetGuid());
        }

        private void OnSceneLoaded()
        {
            if (TransitionManager.Instance.OutputTransitionComponent?.GetGuid() == GetGuid())
                _canBeUsed = false;
        }
        
        private void Awake()
        {
            _canBeUsed = true;
        }
        
        private void OnEnable()
        {
            TransitionManager.OnSceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            TransitionManager.OnSceneLoaded -= OnSceneLoaded;
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