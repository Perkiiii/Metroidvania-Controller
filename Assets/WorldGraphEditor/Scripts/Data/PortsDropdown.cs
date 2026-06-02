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
#endif
        
        private List<(string Guid, string DisplayName)> _data;
        
        public string GetSelectedValue()
        {
#if UNITY_EDITOR
            if (_data == null || _data.Count == 0)
                return null;
            
            var selectedId = _data.FindIndex(item => item.DisplayName == _selectedName);
            if (selectedId != -1)
            {
                _selectedGuid = _guidData[selectedId];
                return _selectedGuid;
            }

            selectedId = _data.FindIndex(item => item.Guid == _selectedGuid);
            if (selectedId != -1)
            {
                _selectedGuid = _guidData[selectedId];
                return _selectedGuid;
            }
            
            _selectedGuid = _guidData[0];
            return _selectedGuid;
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
            
            RefreshSelectedData();
#endif
        }
        
#if UNITY_EDITOR
        private void RefreshSelectedData()
        {
            var selected = _data.FindIndex(item => item.DisplayName == _selectedName);

            if (selected == -1)
                selected = _data.FindIndex(item => item.Guid == _selectedGuid);

            switch (selected)
            {
                case -1 when _data.Count == 0:
                    _selectedName = "";
                    _selectedGuid = "";
                    return;
                    
                case -1 when _data.Count > 0:
                    selected = 0;
                    break;
            }

            _selectedName = _data[selected].DisplayName;
            _selectedGuid = _data[selected].Guid;
        }
#endif
    }
}