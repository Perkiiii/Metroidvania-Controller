#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

namespace WorldGraphEditor
{
    [CustomEditor(typeof(WGEProjectConfig))]
    internal sealed class TransitionManagerResolverEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            WGEProjectConfig.ValidateSingleInstance();
            base.OnInspectorGUI();
        }
    }
}

#endif
