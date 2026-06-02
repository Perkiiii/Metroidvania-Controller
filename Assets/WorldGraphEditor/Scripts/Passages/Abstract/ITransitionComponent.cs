using UnityEngine;

namespace WorldGraphEditor
{
    public interface ITransitionComponent 
    {
#if UNITY_EDITOR
        public void Refresh(RefreshContext context);
#endif
        public Vector3 GetSpawnPosition();
        public string GetGuid();
    }
}