using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Attributes;
using UnityEditor;
using UnityEngine;

namespace Editor.Attributes
{
    [CustomPropertyDrawer(typeof(SubclassSelectorAttribute))]
    public class SubclassSelectorDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, true);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.ManagedReference)
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            var rect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

            var fieldType = GetFieldType(property);
            var typeName = property.managedReferenceFullTypename.Split(' ').Last();
            var currentLabel = string.IsNullOrEmpty(typeName) ? "None" : typeName.Split('.').Last();

            var labelRect = new Rect(rect);
            labelRect.width = EditorGUIUtility.labelWidth;
            EditorGUI.LabelField(labelRect, label);

            var buttonRect = new Rect(rect);
            buttonRect.x += EditorGUIUtility.labelWidth;
            buttonRect.width -= EditorGUIUtility.labelWidth;

            if (GUI.Button(buttonRect, currentLabel, EditorStyles.popup)) ShowTypePopup(property, fieldType);

            EditorGUI.PropertyField(position, property, GUIContent.none, true);
        }

        private void ShowTypePopup(SerializedProperty property, Type targetType)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("None"), false, () => ApplyType(property, null));

            var types = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(s => s.GetTypes())
                .Where(p => targetType.IsAssignableFrom(p) && !p.IsInterface && !p.IsAbstract)
                .OrderBy(t => t.Name);

            foreach (var type in types) menu.AddItem(new GUIContent(type.Name), false, () => ApplyType(property, type));

            menu.ShowAsContext();
        }

        private void ApplyType(SerializedProperty property, Type type)
        {
            property.managedReferenceValue = type != null ? Activator.CreateInstance(type) : null;
            property.serializedObject.ApplyModifiedProperties();
        }

        private Type GetFieldType(SerializedProperty property)
        {
            var parts = property.propertyPath.Split('.');
            var currentType = property.serializedObject.targetObject.GetType();
            foreach (var part in parts)
            {
                if (part == "Array") continue;
                if (part.StartsWith("data[")) continue;

                var field = currentType.GetField(part,
                    BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                if (field == null) return null;
                currentType = field.FieldType;
                if (currentType.IsArray) currentType = currentType.GetElementType();
                else if (currentType.IsGenericType && currentType.GetGenericTypeDefinition() == typeof(List<>))
                    currentType = currentType.GetGenericArguments()[0];
            }

            return currentType;
        }
    }
}