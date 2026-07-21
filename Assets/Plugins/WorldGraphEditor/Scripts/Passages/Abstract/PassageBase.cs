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
        
        public virtual void Refresh(RefreshContext context)
        {
            if (context.EditorGraph == null)
                return;
            
            context.FillPortsDropdownData(_assignedPort);
            
            var guid = _assignedPort.GetSelectedValue();

            if (!context.EditorGraph.TryGetPassageTransitionData(guid, out var transitionData))
                return;
            
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