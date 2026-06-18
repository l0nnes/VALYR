using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Core.Interfaces;
using Unity.Data;
using Unity.Interfaces;
using UnityEditor;
using UnityEngine;

namespace Unity.Editor
{
    [CustomEditor(typeof(VehiclePreset))]
    public class VehiclePresetEditor : UnityEditor.Editor
    {
        private VehiclePreset _target;

        private void OnEnable()
        {
            _target = (VehiclePreset)target;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawDefaultInspectorExcept("modules");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Modules", EditorStyles.boldLabel);

            var modulesProp = serializedObject.FindProperty("modules");

            for (var i = 0; i < modulesProp.arraySize; i++)
            {
                var entryProp = modulesProp.GetArrayElementAtIndex(i);
                var logicProp = entryProp.FindPropertyRelative("moduleLogic");
                var configProp = entryProp.FindPropertyRelative("configAsset");
                var name = entryProp.FindPropertyRelative("name").stringValue;

                // Получаем реальные объекты для проверки типов
                var logicObj = GetTargetObjectOfProperty(logicProp) as IVehicleModule;
                var configObj = configProp.objectReferenceValue as IModuleConfigAsset;

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                {
                    // Header Row
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(name, EditorStyles.boldLabel);
                    if (GUILayout.Button(EditorGUIUtility.IconContent("TreeEditor.Trash"), GUILayout.Width(30)))
                    {
                        modulesProp.DeleteArrayElementAtIndex(i);
                        break;
                    }

                    EditorGUILayout.EndHorizontal();

                    // Logic Field
                    EditorGUILayout.PropertyField(logicProp);

                    // Config Field Validation
                    EditorGUI.BeginChangeCheck();

                    // Smart Object Field: подкрашиваем, если ошибка
                    var originalColor = GUI.backgroundColor;
                    var typeMismatch = false;

                    if (logicObj != null && configObj != null)
                    {
                        var expectedType = logicObj.GetType();
                        var actualType = configObj.GetTargetModuleType();

                        if (expectedType != actualType)
                        {
                            typeMismatch = true;
                            GUI.backgroundColor = new Color(1f, 0.5f, 0.5f); // Red tint
                        }
                    }

                    EditorGUILayout.PropertyField(configProp, new GUIContent("Config Asset"));
                    GUI.backgroundColor = originalColor;

                    if (typeMismatch)
                        EditorGUILayout.HelpBox(
                            $"Mismatch! Expected config for {logicObj.GetType().Name}, but got {configObj.GetTargetModuleType()?.Name}.",
                            MessageType.Error);

                    if (EditorGUI.EndChangeCheck()) serializedObject.ApplyModifiedProperties();

                    // Config Preview
                    if (configProp.objectReferenceValue != null && !typeMismatch)
                    {
                        EditorGUI.indentLevel++;
                        // Используем Foldout, чтобы не захламлять инспектор
                        entryProp.isExpanded = EditorGUILayout.Foldout(entryProp.isExpanded, "Show Config", true);
                        if (entryProp.isExpanded)
                        {
                            UnityEditor.Editor cachedEditor = null;
                            CreateCachedEditor(configProp.objectReferenceValue, null, ref cachedEditor);
                            cachedEditor.OnInspectorGUI();
                        }

                        EditorGUI.indentLevel--;
                    }
                }
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(5);
            }

            DrawAddModuleButton();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawAddModuleButton()
        {
            GUILayout.Space(5);
            var rect = GUILayoutUtility.GetRect(new GUIContent("Add Module"), GUI.skin.button, GUILayout.Height(30));
            if (GUI.Button(rect, "Add Module")) ShowAddModuleMenu();
        }

        // Вспомогательный метод для получения реального объекта из SerializedProperty
        // Это необходимо для проверки типов в Editor, так как SerializedProperty не дает доступ к value напрямую для generic типов
        private object GetTargetObjectOfProperty(SerializedProperty prop)
        {
            var path = prop.propertyPath.Replace(".Array.data[", "[").Split('.');
            object obj = prop.serializedObject.targetObject;
            foreach (var part in path)
            {
                if (obj == null) return null;

                if (part.Contains("["))
                {
                    var elementName = part.Substring(0, part.IndexOf("["));
                    var index = Convert.ToInt32(part.Substring(part.IndexOf("[")).Replace("[", "").Replace("]", ""));
                    var list = obj.GetType().GetField(elementName,
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).GetValue(obj) as IList;
                    obj = list != null && index < list.Count ? list[index] : null;
                }
                else
                {
                    obj = obj.GetType()
                        .GetField(part, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                        ?.GetValue(obj);
                }
            }

            return obj;
        }

        // ... (ShowAddModuleMenu, AddModule, DrawDefaultInspectorExcept остаются теми же) ...

        // Повторяю ShowAddModuleMenu и AddModule для полноты контекста Senior-кода
        private void ShowAddModuleMenu()
        {
            var menu = new GenericMenu();

            var moduleTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .Where(t => typeof(IVehicleModule).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

            var existingPhases = _target.modules
                .Where(m => m.moduleLogic != null)
                .Select(m => m.moduleLogic.Phase)
                .ToHashSet();

            foreach (var type in moduleTypes)
            {
                var tempInstance = (IVehicleModule)Activator.CreateInstance(type);
                var phase = tempInstance.Phase;
                var displayName = $"{phase}/{type.Name}";

                if (existingPhases.Contains(phase))
                    menu.AddDisabledItem(new GUIContent($"{displayName} (Occupied)"));
                else
                    menu.AddItem(new GUIContent(displayName), false, () => AddModule(type));
            }

            menu.ShowAsContext();
        }

        private void AddModule(Type moduleType)
        {
            Undo.RecordObject(_target, "Add Module");

            var newEntry = new VehiclePreset.ModuleEntry();
            newEntry.moduleLogic = (IVehicleModule)Activator.CreateInstance(moduleType);
            newEntry.Validate();

            // Auto-assign config
            var expectedConfigName = $"{moduleType.Name}_ConfigAsset";
            var guids = AssetDatabase.FindAssets($"t:{expectedConfigName}");
            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                newEntry.configAsset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            }

            _target.modules.Add(newEntry);
            EditorUtility.SetDirty(_target);
        }

        private void DrawDefaultInspectorExcept(string skipField)
        {
            var prop = serializedObject.GetIterator();
            if (prop.NextVisible(true))
                do
                {
                    if (prop.name != "m_Script" && prop.name != skipField)
                        EditorGUILayout.PropertyField(prop, true);
                } while (prop.NextVisible(false));
        }
    }
}