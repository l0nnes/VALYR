using System;
using UnityEngine;

namespace Unity.Attributes
{
    [AttributeUsage(AttributeTargets.Field)]
    public class ShowIfAttribute : PropertyAttribute
    {
        public ShowIfAttribute(string conditionFieldName, params object[] expectedValues)
        {
            ConditionFieldName = conditionFieldName;
            ExpectedValues = expectedValues;
        }

        public string ConditionFieldName { get; private set; }
        public object[] ExpectedValues { get; private set; }
        public bool Invert { get; set; }
    }
}