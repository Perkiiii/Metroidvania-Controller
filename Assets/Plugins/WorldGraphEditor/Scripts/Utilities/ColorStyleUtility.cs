using UnityEditor;
using UnityEngine;

namespace WorldGraphEditor
{
    public static class ColorStyleUtility
    {
        public static bool IsProSkin
        {
#if UNITY_EDITOR
            get { return EditorGUIUtility.isProSkin; }
#else
            get { return true; }
#endif
        }

        public static Color ConsoleRedColor =>
            IsProSkin ? new Color(1f, 0.45f, 0.36f) : new Color(0.85f, 0.08f, 0f);
        public static Color ConsoleYellowColor =>
            IsProSkin ? new Color(1f, 0.89f, 0.38f) : new Color(0.73f, 0.36f, 0f);
        public static Color ConsoleCyanColor =>
            IsProSkin ? new Color(0.54f, 0.99f, 1f) : new Color(0f, 0.64f, 1f);
        public static Color ConsoleGreenColor =>
            IsProSkin ? new Color(0.6f, 1f, 0.53f) : new Color(0.11f, 0.49f, 0f);

        public static Color ColoredTextBackgroundColor => IsProSkin
            ? new Color(0.22f, 0.22f, 0.22f)
            : new Color(0.8f, 0.8f, 0.8f);

        public static Color TextColor => IsProSkin
            ? new Color(0.8f, 0.8f, 0.8f)
            : new Color(0.2f, 0.2f, 0.2f);
    }
}