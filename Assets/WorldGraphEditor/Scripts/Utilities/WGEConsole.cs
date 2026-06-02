using UnityEngine;

namespace WorldGraphEditor
{
    public static class WGEConsole
    {
        private static char _arrow => '\u27a4';
        private static string _text => "[WGE]";
        
        public static void Log(string msg)
        {
            Debug.Log($"<b>{_text} {_arrow}</b> {msg}");
        }

        public static void Info(string msg)
        {
            var head = $"<b>{_text} {_arrow}</b>".SetColor(MessageColor.Green);
            Debug.Log($"{head} {msg}");
        }

        public static void Warning(string msg)
        {
            var head = $"<b>{_text} {_arrow}</b>".SetColor(MessageColor.Yellow);
            Debug.LogWarning($"{head} {msg}");
        }
        
        public static void Error(string msg)
        {
            var head = $"<b>{_text} {_arrow}</b>".SetColor(MessageColor.Red);
            Debug.LogError($"{head} {msg}");
        }
    }
}