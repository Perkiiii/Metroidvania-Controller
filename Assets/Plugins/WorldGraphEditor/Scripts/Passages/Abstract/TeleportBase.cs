using UnityEngine;

namespace WorldGraphEditor
{
    [DisallowMultipleComponent]
    public abstract class TeleportBase : MonoBehaviour, ITransitionComponent
    {
        [Tooltip("Select port name that will represent <b>this</b> passage")]
        [SerializeField] private PortsDropdown _assignedPort = new();
        [SerializeField] private PortsDropdown _goTo = new();
        
#if UNITY_EDITOR
        [SerializeField, ReadOnlyField] private string _assignedGuid;
        [SerializeField, ReadOnlyField] private string _targetGuid;
        
        private WorldGraphContainer _container;
        
        public string TargetName { get; private set; }

        public virtual void Refresh(RefreshContext context)
        {
            if (context.Manager != null)
                _container = context.Manager.Container;

            if (_container == null)
                return;

            context.FillPortsDropdownData(_assignedPort);
            context.FillAllPortsDropdownData(_goTo);

            var currentGuid = _assignedPort.GetSelectedValue();
            var targetGuid = _goTo.GetSelectedValue();

            var assignedPortData = _container.EditorData.GetPortData(currentGuid);
            var gotoPortData = _container.EditorData.GetPortData(targetGuid);

            _assignedGuid = assignedPortData.Guid;
            _targetGuid = gotoPortData.Guid;
            TargetName = gotoPortData.Name;
        }
#endif
        
        public string GetGuid() => _assignedPort.GetSelectedValue();

        public string GetTargetGuid() => _goTo.GetSelectedValue();

        public abstract Vector3 GetSpawnPosition();
    }
}