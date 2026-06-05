using UnityEditor.Experimental.GraphView;

namespace WorldGraphEditor.Editor
{
    internal abstract class BaseNode : Node
    {
        public string Guid;
        public string Name;

        public override void OnSelected()
        {
            base.OnSelected();
            UndoRedoUtility.SetSelected(Guid);
        }

        public override void OnUnselected()
        {
            base.OnUnselected();
            UndoRedoUtility.SetUnselected(Guid);
        }
    }
}