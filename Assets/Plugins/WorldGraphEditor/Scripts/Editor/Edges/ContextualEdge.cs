using System;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace WorldGraphEditor.Editor
{
    internal class ContextualEdge : Edge
    {
        private Vector2 _mousePosition;

        public static Action<ContextualEdge, DetachFromPanelEvent, Vector2> OnEdgeDetach;
        
        public ContextualEdge()
        {
            RegisterCallback<ContextualMenuPopulateEvent>(OnContextManuPopulate);
            RegisterCallback<DetachFromPanelEvent>(OnDetach);
            
            this.SetGuid(Guid.NewGuid().ToString());

            GraphUtility.AddStyleSheet(this, "Scripts/Editor/EditorWindows/Styles/CustomEdgeStyle.uss");
            AddToClassList("selected-edge");
        }

        public ContextualEdge(string guid) : this()
        {
            this.SetGuid(guid);
        }

        private void OnDetach(DetachFromPanelEvent evt)
        {
            if (input != null && output != null)
                return;

            var cursorPos = Vector2.zero;

            if (input == null && output?.connected == true)
            {
                cursorPos = edgeControl.controlPoints[^1];
            }
            else if (output == null && input?.connected == true)
            {
                cursorPos = edgeControl.controlPoints[0];
            }
            else if (input == null || input.connected)
            {
                cursorPos = edgeControl.controlPoints[^1];
            }
            else if (output == null || output.connected)
            {
                cursorPos = edgeControl.controlPoints[0];
            }
            
            OnEdgeDetach?.Invoke(this, evt, cursorPos);
        }
        
        public override void OnSelected()
        {
            base.OnSelected();
            UndoRedoUtility.SetSelected(this.GetGuid());
        }
        
        public override void OnUnselected()
        {
            base.OnUnselected();
            UndoRedoUtility.SetUnselected(this.GetGuid());
        }
        
        private void OnContextManuPopulate(ContextualMenuPopulateEvent evt)
        {
            evt.menu.AppendAction("As Shortcut", _ => MakeDirectional(true));
            evt.menu.AppendAction("As One-Way", _ => MakeDirectional(false));
        }
        
        private void MakeDirectional(bool isShortcut)
        {
            var inputPort = input;
            var outputPort = output;
            
            var newEdge = new DirectedEdge(isShortcut, true, this.GetGuid())
            {
                input = inputPort,
                output = outputPort
            };

            inputPort?.Connect(newEdge);
            outputPort?.Connect(newEdge);
            
            UndoRedoUtility.Record(newEdge, "Edge marked as Directional");
            UndoRedoUtility.Replace(newEdge);
            parent.Add(newEdge);
            this.Disconnect();
            RemoveFromHierarchy();
        }
    }
}
