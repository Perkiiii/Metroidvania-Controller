using UnityEngine;

namespace WorldGraphEditor
{
    [DisallowMultipleComponent]
    public abstract class PassageBase : MonoBehaviour, ITransitionComponent
    {
        [Tooltip("Select port name that will represent <b>this</b> passage")]
        [SerializeField] private PortsDropdown _assignedPort = new();
#if UNITY_EDITOR
        [SerializeField, ReadOnlyField] private string _assignedGuid;
        [SerializeField, ReadOnlyField] private string _targetScene;
        
        private WorldGraphContainer _container;
        
        public virtual void Refresh(RefreshContext context)
        {
            if (context.Manager != null)
                _container = context.Manager.Container;
            
            if (_container == null)
                return;
            
            context.FillPortsDropdownData(_assignedPort);
            
            var guid = _assignedPort.GetSelectedValue();
            var transitionData = _container.EditorData.GetTransitionData(guid, false);
            
            _assignedGuid = transitionData.CurrentPassageGuid;
            _targetScene = transitionData.TargetScenePath;
        }
#endif
        
        public string GetGuid()
        {
            return _assignedPort.GetSelectedValue();
        }

        public abstract Vector3 GetSpawnPosition();
    }
}