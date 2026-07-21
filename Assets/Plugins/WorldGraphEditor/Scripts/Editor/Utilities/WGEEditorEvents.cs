using System;
using UnityEditor;

namespace WorldGraphEditor.Editor
{
    [Flags]
    internal enum WGEEditorChangeType
    {
        None = 0,
        GraphEditor = 1 << 0,
        GraphSave = 1 << 1,
        Validation = 1 << 2,
        ProjectConfig = 1 << 3,
    }

    internal static class WGEEditorEvents
    {
        private static WGEEditorChangeType _pending;
        private static bool _scheduled;

        public static event Action<WGEEditorChangeType> SettingsChanged;

        public static void Notify(WGEEditorChangeType changeType)
        {
            if (changeType == WGEEditorChangeType.None)
                return;

            _pending |= changeType;

            if (_scheduled)
                return;

            _scheduled = true;
            EditorApplication.delayCall += Flush;
        }

        private static void Flush()
        {
            EditorApplication.delayCall -= Flush;
            _scheduled = false;

            var pending = _pending;
            _pending = WGEEditorChangeType.None;

            if (pending == WGEEditorChangeType.None)
                return;

            SettingsChanged?.Invoke(pending);
        }
    }
}
