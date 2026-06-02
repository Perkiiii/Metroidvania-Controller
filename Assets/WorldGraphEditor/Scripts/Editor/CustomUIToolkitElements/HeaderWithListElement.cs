using System.Collections.Generic;
using UnityEngine.UIElements;

namespace WorldGraphEditor.Editor
{
    internal class HeaderWithListElement : VisualElement
    {
        private VisualElement _header;
        private VisualElement _list;
        
        public HeaderWithListElement()
        {
            _header = new VisualElement();
            _list = new VisualElement();

            _header.style.flexDirection = FlexDirection.Row;
            _list.style.flexDirection = FlexDirection.Column;
            
            style.flexDirection = FlexDirection.Column;
            style.justifyContent = Justify.FlexStart;
            style.alignItems = Align.Stretch;
            
            Add(_header);
            Add(_list);
        }

        public void Dispose()
        {
            _list.Clear();
            _header.Clear();
        }

        public void AddToList(IEnumerable<VisualElement> items)
        {
            foreach (var item in items)
            {
                _list.Add(item);
            }
        }

        public void SetHeader(VisualElement visualElement)
        {
            _header.Clear();
            _header.Add(visualElement);
        }

        public void SetHeader(IEnumerable<VisualElement> visualElements)
        {
            _header.Clear();

            foreach (var visualElement in visualElements)
            {
                _header.Add(visualElement);
            }
        }
    }
}