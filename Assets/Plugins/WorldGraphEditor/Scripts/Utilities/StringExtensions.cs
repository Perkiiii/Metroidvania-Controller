using UnityEngine;

namespace WorldGraphEditor
{
    public enum MessageColor
    {
        Default,
        Red,
        Yellow,
        Green,
        Cyan,
    }
    
    public static class StringExtensions
    {
        public static string SetColor(this string message, Color color)
        {
            var hex = ColorUtility.ToHtmlStringRGBA(color);
            return $"<color=#{hex}>{message}</color>";
        }

        public static string SetColor(this string message, MessageColor messageColor)
        {
            if (messageColor == MessageColor.Default)
                return message;
            
            var color = GetColor(messageColor);
            return message.SetColor(color);
        }

        public static Color GetColor(this MessageColor messageColor)
        {
            return messageColor switch
            {
                MessageColor.Red => ColorStyleUtility.ConsoleRedColor,
                MessageColor.Yellow => ColorStyleUtility.ConsoleYellowColor,
                MessageColor.Cyan => ColorStyleUtility.ConsoleCyanColor,
                MessageColor.Green => ColorStyleUtility.ConsoleGreenColor,
                _ => Color.white
            };
        }

    }
}