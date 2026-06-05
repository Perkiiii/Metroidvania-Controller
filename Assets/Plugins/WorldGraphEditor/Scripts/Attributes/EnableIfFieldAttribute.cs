using System;
using UnityEngine;

namespace WorldGraphEditor
{
    [AttributeUsage(AttributeTargets.Field)]
    public class EnableIfFieldAttribute : PropertyAttribute
    {
        public string ConditionName { get; private set; }
        public object ExpectedValue { get; private set; }

        public EnableIfFieldAttribute(string conditionName, object expectedValue = null)
        {
            ConditionName = conditionName;
            ExpectedValue = expectedValue;
        }
    }
}