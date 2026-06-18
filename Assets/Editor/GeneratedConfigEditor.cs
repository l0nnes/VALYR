using System;
using System.Reflection;
using Core.Attributes;
using Core.Configs;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    [CustomEditor(typeof(ScriptableObject), true)]
    public class GeneratedConfigEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var type = target.GetType();
            var dataField = type.GetField("data");

            if (dataField == null || !typeof(ModuleConfig).IsAssignableFrom(dataField.FieldType))
            {
                base.OnInspectorGUI();
                return;
            }

            serializedObject.Update();
            var property = serializedObject.FindProperty("data");

            if (property != null) DrawPropertiesWithCoreAttributes(property, dataField.FieldType);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawPropertiesWithCoreAttributes(SerializedProperty rootProp, Type type)
        {
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var prop = rootProp.FindPropertyRelative(field.Name);
                if (prop == null) continue;

                var header = field.GetCustomAttribute<ConfigHeaderAttribute>();
                if (header != null)
                {
                    EditorGUILayout.Space(10);
                    EditorGUILayout.LabelField(header.Text, EditorStyles.boldLabel);
                }

                var space = field.GetCustomAttribute<ConfigSpaceAttribute>();
                if (space != null) EditorGUILayout.Space(space.Height);

                var label = new GUIContent(prop.displayName);
                var tooltip = field.GetCustomAttribute<ConfigTooltipAttribute>();
                if (tooltip != null) label.tooltip = tooltip.Text;

                var range = field.GetCustomAttribute<ConfigRangeAttribute>();
                if (range != null)
                {
                    if (prop.propertyType == SerializedPropertyType.Float)
                        prop.floatValue = EditorGUILayout.Slider(label, prop.floatValue, range.Min, range.Max);
                    else if (prop.propertyType == SerializedPropertyType.Integer)
                        prop.intValue = EditorGUILayout.IntSlider(label, prop.intValue, (int)range.Min, (int)range.Max);
                    else
                        EditorGUILayout.PropertyField(prop, label, true);
                }
                else
                {
                    EditorGUILayout.PropertyField(prop, label, true);
                }
            }
        }
    }
}