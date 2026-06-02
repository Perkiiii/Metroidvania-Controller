using System;
using UnityEngine;

namespace WorldGraphEditor
{
    [AttributeUsage(AttributeTargets.Field)]
    public class ReadOnlyFieldAttribute : PropertyAttribute
    {
        
    }
}