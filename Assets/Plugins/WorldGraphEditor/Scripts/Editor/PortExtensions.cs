using System;
using System.Collections.Generic;

namespace WorldGraphEditor.Editor
{
    internal static class PortExtensions
    {
        public static bool HasDuplicatesPorts<T>(this IReadOnlyList<T> list, Func<T, string> selector)
        {
            if (list is not { Count: > 1 })
                return false;

            var set = new HashSet<string>();

            foreach (var item in list)
            {
                var normalized = NormalizeForPortCompare(selector(item));

                if (!set.Add(normalized))
                    return true;
            }

            return false;
        }

        public static string NormalizeForPortCompare(string str)
        {
            if (str == null)
                return string.Empty;
            
            return str.Trim();
        }
    }
}