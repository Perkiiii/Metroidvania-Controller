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
        
        public string TargetName { get; private set; }

        public virtual void Refresh(RefreshContext context)
        {
            if (context.EditorGraph == null)
                return;

            context.FillPortsDropdownData(_assignedPort);
            context.FillAllPortsDropdownData(_goTo);

            var currentGuid = _assignedPort.GetSelectedValue();
            var targetGuid = _goTo.GetSelectedValue();

            _ = context.EditorGraph.TryGetPortData(currentGuid, out var assignedPortData);
            _ = context.EditorGraph.TryGetPortData(targetGuid, out var gotoPortData);

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