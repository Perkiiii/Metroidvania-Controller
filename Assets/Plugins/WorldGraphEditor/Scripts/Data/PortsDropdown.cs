using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace WorldGraphEditor
{
    [Serializable]
    public class PortsDropdown
    {
        [SerializeField] private string _selectedGuid;

#if UNITY_EDITOR
        [SerializeField] private string[] _displayData;
        [SerializeField] private string[] _guidData;
        [SerializeField] private string _selectedName;

        internal static bool ResolveChangedSelection;
#endif

        private List<(string Guid, string DisplayName)> _data;
        
        public string GetSelectedValue()
        {
#if UNITY_EDITOR
            if (!HasLoadedPortCaches())
                return GetPersistedGuidOrNull();
            
            return ResolveSelection();
#else
            return _selectedGuid;
#endif
        }

        public void SetData(IEnumerable<(string Guid, string DisplayName)> data)
        {
#if UNITY_EDITOR
            if (data == null)
            {
                _data = new();
                _displayData = null;
                _guidData = null;

                return;
            }

            _data = data.ToList();
            _displayData = _data.Select(item => item.DisplayName).ToArray();
            _guidData = _data.Select(item => item.Guid).ToArray();

            if (_data.Any(d => d.Guid == _selectedGuid && d.DisplayName == _selectedName))
                return;

            ResolveSelection();
#endif
        }

#if UNITY_EDITOR
        private bool HasLoadedPortCaches()
        {
            return _data != null && _data.Count > 0 && _guidData != null && _guidData.Length > 0;
        }
        
        private string GetPersistedGuidOrNull()
        {
            return string.IsNullOrEmpty(_selectedGuid) ? null : _selectedGuid;
        }
        
        private string ResolveSelection()
        {
            var byName = _data.FindIndex(item => item.DisplayName == _selectedName);
            if (byName != -1)
                return Apply(byName);

            var byGuid = _data.FindIndex(item => item.Guid == _selectedGuid);
            if (byGuid != -1)
                return Apply(byGuid);

            return GetPersistedGuidOrNull();
        }

        private string Apply(int index)
        {
            var newGuid = _guidData[index];
            var newName = _displayData[index];

            if (_selectedGuid != newGuid || _selectedName != newName)
            {
                _selectedGuid = newGuid;
                _selectedName = newName;
                ResolveChangedSelection = true;
            }

            return _selectedGuid;
        }
#endif
    }
}
