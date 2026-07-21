#if UNITY_EDITOR

using System;
using UnityEngine.SceneManagement;

namespace WorldGraphEditor
{
    public readonly struct RefreshContext
    {
        [Obsolete("Use EditorGraph property instead")]
        public readonly ITransitionManager Manager;
        public readonly EditorGraph EditorGraph;
        
        public RefreshContext(WGEProjectConfig resolver)
        {
#pragma warning disable CS0618
            Manager = null;
#pragma warning restore CS1998 
            
            EditorGraph = resolver.GetEditorGraph();
        }
        
        /// <summary>
        /// Editor only. Fills the specified <see cref="PortsDropdown"/> with port data from the currently active scene.
        /// </summary>
        /// <param name="dropdown">The dropdown to populate with available ports on the active scene.</param>
        /// <remarks>
        /// This method uses <see cref="ContainerEditorData.GetPortsDropdownData(string)"/> to retrieve all ports located in the active scene.
        /// </remarks>
        public void FillPortsDropdownData(PortsDropdown dropdown)
        {
            if (EditorGraph == null)
            {
                dropdown.SetData(null);
                return;
            }
            
            var data = EditorGraph.GetPortsDropdownData(SceneManager.GetActiveScene().path);
            
            dropdown.SetData(data);
        }
        
        /// <summary>
        /// Editor only. Fills the specified <see cref="PortsDropdown"/> with port data from all scenes in the container.
        /// </summary>
        /// <param name="dropdown">The dropdown to populate with all available ports across all scenes.</param>
        /// <remarks>
        /// This method uses <see cref="ContainerEditorData.GetAllPortsDropdownData"/> to retrieve data about all ports
        /// </remarks>
        public void FillAllPortsDropdownData(PortsDropdown dropdown)
        {
            if (EditorGraph == null)
            {
                dropdown.SetData(null);
                return;
            }
            
            var data = EditorGraph.GetAllPortsDropdownData();
            dropdown.SetData(data);
        }
    }
}
#endif
