using UnityEngine;

namespace WorldGraphEditor
{
    [RequireComponent(typeof(BoxCollider2D))]
    public class Passage2D : PassageBase, IPusher
    {
        [Space]
        [SerializeField] private Optional<Vector2> _pushForce;
        [SerializeField] private Optional<Transform> _customSpawnPosition;

        [SerializeField, HideInInspector] private BoxCollider2D _boxCollider;

        private bool _canBeUsed;

        protected void OnValidate()
        {
            _boxCollider = GetComponent<BoxCollider2D>();
            _boxCollider.isTrigger = true;
        }

        public override Vector3 GetSpawnPosition() =>
            _customSpawnPosition.Enabled ? _customSpawnPosition.Value.position : transform.position;
        
        public PushData GetPushData() => _pushForce.Enabled ? new PushData(_pushForce.Value) : default;

        public void Traverse()
        {
            TransitionManager.Instance.GoFrom(GetGuid(), true);
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
        
        private void OnSceneLoaded()
        {
            if (TransitionManager.Instance.OutputTransitionComponent?.GetGuid() == GetGuid())
                _canBeUsed = false;
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