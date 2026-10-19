using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using VALYR.Core;
using VALYR.Core.Modules;
using VALYR.Runtime;

namespace VALYR.Editor
{
    /// <summary>
    /// Custom Inspector for VehicleController.
    /// Provides categorized modular subsystem selection, Transform-based wheel mount setup,
    /// and ScriptableObject preset management without AI-bloat or fake telemetry.
    /// </summary>
    [CustomEditor(typeof(VehicleController))]
    public class VehicleControllerEditor : UnityEditor.Editor
    {
        private SerializedProperty _presetProp;
        private SerializedProperty _wheelMountsProp;
        private SerializedProperty _wheelRadiusProp;
        private SerializedProperty _chassisMassProp;
        private SerializedProperty _centerOfMassProp;
        private SerializedProperty _groundLayersProp;
        private SerializedProperty _visualViewProp;

        private SerializedProperty _wheelContactProp;
        private SerializedProperty _suspensionProp;
        private SerializedProperty _steeringProp;
        private SerializedProperty _engineProp;
        private SerializedProperty _transmissionProp;
        private SerializedProperty _tiresProp;
        private SerializedProperty _assistsProp;

        // Foldout toggles
        private static bool _chassisFoldout = true;
        private static bool _wheelContactFoldout = true;
        private static bool _suspensionFoldout = true;
        private static bool _steeringFoldout = true;
        private static bool _powertrainFoldout = true;
        private static bool _tiresFoldout = true;
        private static bool _assistsFoldout = true;

        private static readonly Dictionary<Type, List<Type>> _typeCache = new();

        private void OnEnable()
        {
            _presetProp = serializedObject.FindProperty("Preset");
            _wheelMountsProp = serializedObject.FindProperty("WheelMounts");
            _wheelRadiusProp = serializedObject.FindProperty("WheelRadius");
            _chassisMassProp = serializedObject.FindProperty("ChassisMass");
            _centerOfMassProp = serializedObject.FindProperty("CenterOfMass");
            _groundLayersProp = serializedObject.FindProperty("GroundLayers");
            _visualViewProp = serializedObject.FindProperty("_visualView");

            _wheelContactProp = serializedObject.FindProperty("WheelContact");
            _suspensionProp = serializedObject.FindProperty("Suspension");
            _steeringProp = serializedObject.FindProperty("Steering");
            _engineProp = serializedObject.FindProperty("Engine");
            _transmissionProp = serializedObject.FindProperty("Transmission");
            _tiresProp = serializedObject.FindProperty("Tires");
            _assistsProp = serializedObject.FindProperty("Assists");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var controller = (VehicleController)target;

            // 1. ScriptableObject Preset Section
            DrawPresetSection(controller);
            EditorGUILayout.Space(6);

            // 2. Chassis & Wheel Mount Transforms
            DrawChassisSection(controller);
            EditorGUILayout.Space(6);

            // 3. Categorized Subsystem Modules
            EditorGUILayout.LabelField("Modular Subsystems", EditorStyles.boldLabel);

            DrawModuleCategory<IWheelContactModule>("Wheel Contact Subsystem", _wheelContactProp, ref _wheelContactFoldout);
            EditorGUILayout.Space(4);

            DrawModuleCategory<ISuspensionModule>("Suspension Subsystem", _suspensionProp, ref _suspensionFoldout);
            EditorGUILayout.Space(4);

            DrawModuleCategory<ISteeringModule>("Steering Subsystem", _steeringProp, ref _steeringFoldout);
            EditorGUILayout.Space(4);

            DrawPowertrainCategory();
            EditorGUILayout.Space(4);

            DrawModuleCategory<ITireModule>("Tire Dynamics Subsystem", _tiresProp, ref _tiresFoldout);
            EditorGUILayout.Space(4);

            DrawAssistsCategory(controller);

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawPresetSection(VehicleController controller)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            {
                EditorGUILayout.LabelField("Vehicle Preset (ScriptableObject)", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_presetProp, new GUIContent("Active Preset Asset"));

                EditorGUILayout.BeginHorizontal();
                {
                    GUI.enabled = controller.Preset != null;
                    if (GUILayout.Button("Apply Preset to Vehicle"))
                    {
                        if (EditorUtility.DisplayDialog("Apply Preset?",
                            $"Overwrite vehicle parameters and modules with '{controller.Preset.name}' settings?",
                            "Apply", "Cancel"))
                        {
                            Undo.RecordObject(controller, "Apply Vehicle Preset");
                            controller.Preset.ApplyTo(controller);
                            EditorUtility.SetDirty(controller);
                            serializedObject.Update();
                        }
                    }

                    if (GUILayout.Button("Save Vehicle to Preset"))
                    {
                        if (EditorUtility.DisplayDialog("Save to Preset?",
                            $"Save current vehicle settings into '{controller.Preset.name}' asset?",
                            "Save", "Cancel"))
                        {
                            Undo.RecordObject(controller.Preset, "Save to Vehicle Preset");
                            controller.Preset.CaptureFrom(controller);
                            EditorUtility.SetDirty(controller.Preset);
                            AssetDatabase.SaveAssets();
                        }
                    }
                    GUI.enabled = true;

                    if (GUILayout.Button("Create New Asset..."))
                    {
                        string path = EditorUtility.SaveFilePanelInProject("Create Vehicle Preset", "NewVehiclePreset", "asset", "Save preset asset");
                        if (!string.IsNullOrEmpty(path))
                        {
                            var newPreset = CreateInstance<VehiclePreset>();
                            newPreset.CaptureFrom(controller);
                            AssetDatabase.CreateAsset(newPreset, path);
                            AssetDatabase.SaveAssets();

                            Undo.RecordObject(controller, "Assign New Preset");
                            controller.Preset = newPreset;
                            EditorUtility.SetDirty(controller);
                            serializedObject.Update();
                        }
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawChassisSection(VehicleController controller)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            {
                _chassisFoldout = EditorGUILayout.Foldout(_chassisFoldout, "Chassis & Wheel Mounts", true, EditorStyles.foldoutHeader);
                if (_chassisFoldout)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField("Wheel Mounts (Drag transforms from hierarchy):", EditorStyles.miniLabel);

                    SerializedProperty flProp = _wheelMountsProp.FindPropertyRelative("FrontLeft");
                    SerializedProperty frProp = _wheelMountsProp.FindPropertyRelative("FrontRight");
                    SerializedProperty rlProp = _wheelMountsProp.FindPropertyRelative("RearLeft");
                    SerializedProperty rrProp = _wheelMountsProp.FindPropertyRelative("RearRight");

                    EditorGUILayout.PropertyField(flProp, new GUIContent("Front-Left Anchor"));
                    EditorGUILayout.PropertyField(frProp, new GUIContent("Front-Right Anchor"));
                    EditorGUILayout.PropertyField(rlProp, new GUIContent("Rear-Left Anchor"));
                    EditorGUILayout.PropertyField(rrProp, new GUIContent("Rear-Right Anchor"));

                    if (GUILayout.Button("Auto-Find Anchors in Hierarchy", EditorStyles.miniButton))
                    {
                        Undo.RecordObject(controller, "Auto-Find Wheel Mounts");
                        controller.AutoFindWheelMounts();
                        controller.UpdateChassisGeometry();
                        EditorUtility.SetDirty(controller);
                        serializedObject.Update();
                    }

                    EditorGUILayout.Space(4);
                    EditorGUILayout.PropertyField(_wheelRadiusProp, new GUIContent("Wheel Radius (m)"));
                    EditorGUILayout.PropertyField(_chassisMassProp, new GUIContent("Chassis Mass (kg)"));
                    EditorGUILayout.PropertyField(_centerOfMassProp, new GUIContent("Center Of Mass"));
                    EditorGUILayout.PropertyField(_groundLayersProp, new GUIContent("Ground Layers"));
                    EditorGUILayout.PropertyField(_visualViewProp, new GUIContent("Visual View"));

                    if (controller.Rigidbody != null)
                    {
                        EditorGUILayout.HelpBox($"Calculated Wheel Offsets: FL({controller.ChassisConfig.WheelOffsets[0]:F2}), FR({controller.ChassisConfig.WheelOffsets[1]:F2}), RL({controller.ChassisConfig.WheelOffsets[2]:F2}), RR({controller.ChassisConfig.WheelOffsets[3]:F2})", MessageType.None);
                    }

                    EditorGUI.indentLevel--;
                }
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawModuleCategory<TInterface>(string categoryName, SerializedProperty property, ref bool foldout) where TInterface : class
        {
            List<Type> types = GetImplementations(typeof(TInterface));

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            {
                EditorGUILayout.BeginHorizontal();
                foldout = EditorGUILayout.Foldout(foldout, categoryName, true, EditorStyles.foldoutHeader);

                string currentTypeName = property.managedReferenceValue != null
                    ? property.managedReferenceValue.GetType().Name
                    : "None";

                int currentIndex = 0;
                for (int i = 0; i < types.Count; i++)
                {
                    if (types[i].Name == currentTypeName)
                    {
                        currentIndex = i + 1;
                        break;
                    }
                }

                string[] displayNames = new string[types.Count + 1];
                displayNames[0] = "None (Disabled)";
                for (int i = 0; i < types.Count; i++)
                {
                    displayNames[i + 1] = ObjectNames.NicifyVariableName(types[i].Name);
                }

                int newIndex = EditorGUILayout.Popup(currentIndex, displayNames, GUILayout.Width(180));
                if (newIndex != currentIndex)
                {
                    Undo.RecordObject(target, $"Change {categoryName}");
                    if (newIndex == 0)
                    {
                        property.managedReferenceValue = null;
                    }
                    else
                    {
                        property.managedReferenceValue = Activator.CreateInstance(types[newIndex - 1]);
                    }
                    serializedObject.ApplyModifiedProperties();
                }
                EditorGUILayout.EndHorizontal();

                if (foldout && property.managedReferenceValue != null)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.Space(2);
                    DrawChildrenProperties(property);
                    EditorGUI.indentLevel--;
                }
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawPowertrainCategory()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            {
                _powertrainFoldout = EditorGUILayout.Foldout(_powertrainFoldout, "Powertrain Subsystem", true, EditorStyles.foldoutHeader);
                if (_powertrainFoldout)
                {
                    EditorGUI.indentLevel++;

                    // Engine Slot
                    DrawSubModuleSlot<IEngineModule>("Engine Module", _engineProp);
                    EditorGUILayout.Space(4);

                    // Transmission Slot
                    DrawSubModuleSlot<ITransmissionModule>("Transmission Module", _transmissionProp);

                    EditorGUI.indentLevel--;
                }
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawSubModuleSlot<TInterface>(string slotLabel, SerializedProperty property) where TInterface : class
        {
            List<Type> types = GetImplementations(typeof(TInterface));

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(slotLabel, EditorStyles.boldLabel, GUILayout.Width(150));

            string currentTypeName = property.managedReferenceValue != null
                ? property.managedReferenceValue.GetType().Name
                : "None";

            int currentIndex = 0;
            for (int i = 0; i < types.Count; i++)
            {
                if (types[i].Name == currentTypeName)
                {
                    currentIndex = i + 1;
                    break;
                }
            }

            string[] displayNames = new string[types.Count + 1];
            displayNames[0] = "None (Disabled)";
            for (int i = 0; i < types.Count; i++)
            {
                displayNames[i + 1] = ObjectNames.NicifyVariableName(types[i].Name);
            }

            int newIndex = EditorGUILayout.Popup(currentIndex, displayNames);
            if (newIndex != currentIndex)
            {
                Undo.RecordObject(target, $"Change {slotLabel}");
                if (newIndex == 0)
                {
                    property.managedReferenceValue = null;
                }
                else
                {
                    property.managedReferenceValue = Activator.CreateInstance(types[newIndex - 1]);
                }
                serializedObject.ApplyModifiedProperties();
            }
            EditorGUILayout.EndHorizontal();

            if (property.managedReferenceValue != null)
            {
                EditorGUI.indentLevel++;
                DrawChildrenProperties(property);
                EditorGUI.indentLevel--;
            }
        }

        private void DrawAssistsCategory(VehicleController controller)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            {
                EditorGUILayout.BeginHorizontal();
                _assistsFoldout = EditorGUILayout.Foldout(_assistsFoldout, "Driver Assists & Chassis Extensions", true, EditorStyles.foldoutHeader);

                if (GUILayout.Button("+ Add Assist", EditorStyles.miniButton, GUILayout.Width(100)))
                {
                    GenericMenu menu = new GenericMenu();
                    List<Type> assistTypes = GetImplementations(typeof(VehicleModule));
                    // Filter out core modules that already have dedicated slots
                    foreach (var type in assistTypes)
                    {
                        if (typeof(ISuspensionModule).IsAssignableFrom(type) ||
                            typeof(ISteeringModule).IsAssignableFrom(type) ||
                            typeof(IEngineModule).IsAssignableFrom(type) ||
                            typeof(ITransmissionModule).IsAssignableFrom(type) ||
                            typeof(ITireModule).IsAssignableFrom(type))
                        {
                            continue;
                        }

                        menu.AddItem(new GUIContent(ObjectNames.NicifyVariableName(type.Name)), false, () =>
                        {
                            Undo.RecordObject(controller, "Add Assist Module");
                            controller.Assists.Add((VehicleModule)Activator.CreateInstance(type));
                            EditorUtility.SetDirty(controller);
                            serializedObject.Update();
                        });
                    }
                    menu.ShowAsContext();
                }
                EditorGUILayout.EndHorizontal();

                if (_assistsFoldout)
                {
                    EditorGUI.indentLevel++;
                    if (controller.Assists == null || controller.Assists.Count == 0)
                    {
                        EditorGUILayout.LabelField("No assist modules attached.", EditorStyles.miniLabel);
                    }
                    else
                    {
                        for (int i = 0; i < controller.Assists.Count; i++)
                        {
                            var assist = controller.Assists[i];
                            if (assist == null) continue;

                            EditorGUILayout.BeginVertical(GUI.skin.box);
                            EditorGUILayout.BeginHorizontal();
                            EditorGUILayout.LabelField(ObjectNames.NicifyVariableName(assist.GetType().Name), EditorStyles.boldLabel);

                            if (GUILayout.Button("X", GUILayout.Width(22), GUILayout.Height(18)))
                            {
                                Undo.RecordObject(controller, "Remove Assist Module");
                                controller.Assists.RemoveAt(i);
                                EditorUtility.SetDirty(controller);
                                serializedObject.Update();
                                GUIUtility.ExitGUI();
                                break;
                            }
                            EditorGUILayout.EndHorizontal();

                            if (i < _assistsProp.arraySize)
                            {
                                SerializedProperty itemProp = _assistsProp.GetArrayElementAtIndex(i);
                                DrawChildrenProperties(itemProp);
                            }
                            EditorGUILayout.EndVertical();
                            EditorGUILayout.Space(2);
                        }
                    }
                    EditorGUI.indentLevel--;
                }
            }
            EditorGUILayout.EndVertical();
        }

        private static void DrawChildrenProperties(SerializedProperty parentProperty)
        {
            SerializedProperty iterator = parentProperty.Copy();
            SerializedProperty endProperty = iterator.GetEndProperty();

            bool enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                if (SerializedProperty.EqualContents(iterator, endProperty))
                    break;

                enterChildren = false;

                // Skip internal metadata
                if (iterator.name == "ModuleId" || iterator.name == "IsEnabled")
                    continue;

                EditorGUILayout.PropertyField(iterator, true);
            }
        }

        private static List<Type> GetImplementations(Type targetType)
        {
            if (_typeCache.TryGetValue(targetType, out var list))
                return list;

            list = new List<Type>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                string name = assembly.GetName().Name;
                if (!name.StartsWith("VALYR") && !name.StartsWith("Assembly-CSharp"))
                    continue;

                try
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        if (!type.IsAbstract && !type.IsInterface && targetType.IsAssignableFrom(type))
                        {
                            list.Add(type);
                        }
                    }
                }
                catch { }
            }

            _typeCache[targetType] = list;
            return list;
        }
    }
}
