using System;
using Unity.Attributes;
using UnityEditor;
using UnityEngine;

namespace Editor.Attributes
{
    [CustomPropertyDrawer(typeof(ShowIfAttribute))]
    public class ShowIfDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (ShouldShow(property))
                return EditorGUI.GetPropertyHeight(property, label, true);
            return 0f;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (ShouldShow(property))
                EditorGUI.PropertyField(position, property, label, true);
        }

        private bool ShouldShow(SerializedProperty property)
        {
            var attr = (ShowIfAttribute)attribute;
            var conditionPath = property.propertyPath.Replace(property.name, attr.ConditionFieldName);
            var conditionProperty = property.serializedObject.FindProperty(conditionPath);

            if (conditionProperty == null)
                conditionProperty = property.serializedObject.FindProperty(attr.ConditionFieldName);

            if (conditionProperty == null)
                return true;

            var isMatch = false;

            if (conditionProperty.propertyType == SerializedPropertyType.Boolean)
            {
                var state = conditionProperty.boolValue;
                if (attr.ExpectedValues != null && attr.ExpectedValues.Length > 0)
                {
                    foreach (var val in attr.ExpectedValues)
                        if (val is bool b && b == state)
                        {
                            isMatch = true;
                            break;
                        }
                }
                else
                {
                    isMatch = state;
                }
            }
            else if (conditionProperty.propertyType == SerializedPropertyType.Enum)
            {
                var state = conditionProperty.intValue;
                if (attr.ExpectedValues != null && attr.ExpectedValues.Length > 0)
                    foreach (var val in attr.ExpectedValues)
                        if (Convert.ToInt32(val) == state)
                        {
                            isMatch = true;
                            break;
                        }
            }
            else if (conditionProperty.propertyType == SerializedPropertyType.Integer)
            {
                var state = conditionProperty.intValue;
                if (attr.ExpectedValues != null && attr.ExpectedValues.Length > 0)
                    foreach (var val in attr.ExpectedValues)
                        if (Convert.ToInt32(val) == state)
                        {
                            isMatch = true;
                            break;
                        }
            }

            return attr.Invert ? !isMatch : isMatch;
        }
    }
}