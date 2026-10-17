using UnityEditor;
using UnityEngine;
using VALYR.Core;
using VALYR.Runtime;

namespace VALYR.Editor
{
    /// <summary>
    /// Custom Inspector for VehicleController.
    /// Provides a structured, designer-friendly workflow with category tabs,
    /// quick vehicle presets, modular subsystem toggles, and live Play-Mode telemetry.
    /// </summary>
    [CustomEditor(typeof(VehicleController))]
    public class VehicleControllerEditor : UnityEditor.Editor
    {
        private static int _selectedTab = 0;
        private static readonly string[] TabNames = new string[]
        {
            "Chassis",
            "Suspension",
            "Steering",
            "Powertrain",
            "Tires",
            "Assists",
            "Telemetry"
        };

        // Serialized Properties
        private SerializedProperty _chassisConfigProp;
        private SerializedProperty _groundLayersProp;

        private SerializedProperty _enableSuspensionProp;
        private SerializedProperty _enableSteeringProp;
        private SerializedProperty _enablePowertrainProp;
        private SerializedProperty _enableAntiRollBarProp;
        private SerializedProperty _enableAbsProp;
        private SerializedProperty _enableTcsProp;

        private SerializedProperty _suspensionProp;
        private SerializedProperty _steeringProp;
        private SerializedProperty _engineProp;
        private SerializedProperty _transmissionProp;
        private SerializedProperty _antiRollBarProp;
        private SerializedProperty _absProp;
        private SerializedProperty _tcsProp;
        private SerializedProperty _tiresProp;
        private SerializedProperty _visualViewProp;

        // Custom GUI styles
        private GUIStyle _headerStyle;
        private GUIStyle _subHeaderStyle;
        private GUIStyle _cardBoxStyle;
        private GUIStyle _greenTextStyle;
        private GUIStyle _redTextStyle;
        private GUIStyle _yellowTextStyle;

        private void OnEnable()
        {
            _chassisConfigProp = serializedObject.FindProperty("ChassisConfig");
            _groundLayersProp = serializedObject.FindProperty("GroundLayers");

            _enableSuspensionProp = serializedObject.FindProperty("EnableSuspension");
            _enableSteeringProp = serializedObject.FindProperty("EnableSteering");
            _enablePowertrainProp = serializedObject.FindProperty("EnablePowertrain");
            _enableAntiRollBarProp = serializedObject.FindProperty("EnableAntiRollBar");
            _enableAbsProp = serializedObject.FindProperty("EnableABS");
            _enableTcsProp = serializedObject.FindProperty("EnableTCS");

            _suspensionProp = serializedObject.FindProperty("Suspension");
            _steeringProp = serializedObject.FindProperty("Steering");
            _engineProp = serializedObject.FindProperty("Engine");
            _transmissionProp = serializedObject.FindProperty("Transmission");
            _antiRollBarProp = serializedObject.FindProperty("AntiRollBar");
            _absProp = serializedObject.FindProperty("Abs");
            _tcsProp = serializedObject.FindProperty("Tcs");
            _tiresProp = serializedObject.FindProperty("Tires");
            _visualViewProp = serializedObject.FindProperty("_visualView");
        }

        public override void OnInspectorGUI()
        {
            InitStyles();
            serializedObject.Update();

            var controller = (VehicleController)target;

            // 1. Header Banner
            DrawHeaderBanner();

            // 2. Preset Quick-Bar
            DrawPresetsBar(controller);

            // 3. Subsystem Activation Matrix
            DrawSubsystemsMatrix(controller);

            EditorGUILayout.Space(6);

            // 4. Tab Navigation Toolbar
            _selectedTab = GUILayout.Toolbar(_selectedTab, TabNames, GUILayout.Height(26));
            EditorGUILayout.Space(8);

            // 5. Tab Content
            switch (_selectedTab)
            {
                case 0: // Chassis
                    DrawChassisTab(controller);
                    break;
                case 1: // Suspension
                    DrawSuspensionTab();
                    break;
                case 2: // Steering
                    DrawSteeringTab();
                    break;
                case 3: // Powertrain
                    DrawPowertrainTab();
                    break;
                case 4: // Tires
                    DrawTiresTab();
                    break;
                case 5: // Assists
                    DrawAssistsTab();
                    break;
                case 6: // Telemetry
                    DrawTelemetryTab(controller);
                    break;
            }

            serializedObject.ApplyModifiedProperties();

            // Auto-repaint during Play Mode if on Telemetry tab
            if (Application.isPlaying && _selectedTab == 6)
            {
                Repaint();
            }
        }

        private void DrawHeaderBanner()
        {
            EditorGUILayout.BeginVertical(_cardBoxStyle);
            {
                EditorGUILayout.BeginHorizontal();
                {
                    EditorGUILayout.LabelField("VALYR VEHICLE CONTROLLER", _headerStyle);
                    GUILayout.FlexibleSpace();
                    string modeText = Application.isPlaying ? "[PLAYING]" : "[EDIT MODE]";
                    GUIStyle modeStyle = Application.isPlaying ? _greenTextStyle : _yellowTextStyle;
                    EditorGUILayout.LabelField(modeText, modeStyle, GUILayout.Width(80));
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.LabelField("Deterministic Modular Vehicle Dynamics Pipeline", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        private void DrawPresetsBar(VehicleController controller)
        {
            EditorGUILayout.BeginVertical(_cardBoxStyle);
            {
                EditorGUILayout.LabelField("Quick Vehicle Presets", _subHeaderStyle);
                EditorGUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button("Sedan (Solaris/Logan)", GUILayout.Height(24)))
                    {
                        if (EditorUtility.DisplayDialog("Apply Sedan Preset?",
                            "This will overwrite vehicle parameters with baseline Civilian Sedan settings (FWD 1.6L 6AT). Continue?",
                            "Apply", "Cancel"))
                        {
                            Undo.RecordObject(controller, "Apply Sedan Preset");
                            controller.ApplyCivilianSedanPreset();
                            EditorUtility.SetDirty(controller);
                        }
                    }

                    if (GUILayout.Button("Sport Coupe (RWD V8)", GUILayout.Height(24)))
                    {
                        if (EditorUtility.DisplayDialog("Apply Sport Coupe Preset?",
                            "This will overwrite vehicle parameters with Sport Coupe settings (RWD 4.0L V8). Continue?",
                            "Apply", "Cancel"))
                        {
                            Undo.RecordObject(controller, "Apply Sport Coupe Preset");
                            controller.ApplySportCoupePreset();
                            EditorUtility.SetDirty(controller);
                        }
                    }

                    if (GUILayout.Button("Off-Road SUV (AWD 4x4)", GUILayout.Height(24)))
                    {
                        if (EditorUtility.DisplayDialog("Apply Off-Road Preset?",
                            "This will overwrite vehicle parameters with Off-Road SUV settings (AWD 4x4). Continue?",
                            "Apply", "Cancel"))
                        {
                            Undo.RecordObject(controller, "Apply Off-Road Preset");
                            controller.ApplyOffroadPreset();
                            EditorUtility.SetDirty(controller);
                        }
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        private void DrawSubsystemsMatrix(VehicleController controller)
        {
            EditorGUILayout.BeginVertical(_cardBoxStyle);
            {
                EditorGUILayout.LabelField("Active Subsystems & Modules", _subHeaderStyle);

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.BeginHorizontal();
                {
                    EditorGUILayout.PropertyField(_enableSuspensionProp, new GUIContent("Suspension"));
                    EditorGUILayout.PropertyField(_enableSteeringProp, new GUIContent("Steering"));
                    EditorGUILayout.PropertyField(_enablePowertrainProp, new GUIContent("Powertrain"));
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                {
                    EditorGUILayout.PropertyField(_enableAntiRollBarProp, new GUIContent("Anti-Roll"));
                    EditorGUILayout.PropertyField(_enableAbsProp, new GUIContent("ABS"));
                    EditorGUILayout.PropertyField(_enableTcsProp, new GUIContent("TCS"));
                }
                EditorGUILayout.EndHorizontal();

                if (EditorGUI.EndChangeCheck())
                {
                    serializedObject.ApplyModifiedProperties();
                    controller.SyncModuleEnableStates();
                }
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawChassisTab(VehicleController controller)
        {
            EditorGUILayout.BeginVertical(_cardBoxStyle);
            {
                EditorGUILayout.LabelField("Chassis Geometry & Weight", _subHeaderStyle);
                EditorGUILayout.PropertyField(_chassisConfigProp, true);
                EditorGUILayout.Space(6);
                EditorGUILayout.PropertyField(_groundLayersProp);
                EditorGUILayout.Space(6);
                EditorGUILayout.PropertyField(_visualViewProp);

                if (controller.Rigidbody != null)
                {
                    EditorGUILayout.Space(4);
                    EditorGUILayout.HelpBox($"Rigidbody Mass: {controller.Rigidbody.mass:F0} kg | Center of Mass: {controller.Rigidbody.centerOfMass}", MessageType.Info);
                }
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawSuspensionTab()
        {
            EditorGUILayout.BeginVertical(_cardBoxStyle);
            {
                EditorGUILayout.LabelField("Raycast Suspension Subsystem", _subHeaderStyle);
                EditorGUILayout.PropertyField(_suspensionProp, true);
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawSteeringTab()
        {
            EditorGUILayout.BeginVertical(_cardBoxStyle);
            {
                EditorGUILayout.LabelField("Steering Kinematics Subsystem", _subHeaderStyle);
                EditorGUILayout.PropertyField(_steeringProp, true);
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawPowertrainTab()
        {
            EditorGUILayout.BeginVertical(_cardBoxStyle);
            {
                EditorGUILayout.LabelField("Engine Subsystem", _subHeaderStyle);
                EditorGUILayout.PropertyField(_engineProp, true);
                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("Transmission & Brakes Subsystem", _subHeaderStyle);
                EditorGUILayout.PropertyField(_transmissionProp, true);
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawTiresTab()
        {
            EditorGUILayout.BeginVertical(_cardBoxStyle);
            {
                EditorGUILayout.LabelField("Brush Tire Model Subsystem", _subHeaderStyle);
                EditorGUILayout.PropertyField(_tiresProp, true);
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawAssistsTab()
        {
            EditorGUILayout.BeginVertical(_cardBoxStyle);
            {
                EditorGUILayout.LabelField("Anti-Roll Bar Chassis Assist", _subHeaderStyle);
                EditorGUILayout.PropertyField(_antiRollBarProp, true);
                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("Anti-Lock Braking System (ABS)", _subHeaderStyle);
                EditorGUILayout.PropertyField(_absProp, true);
                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("Traction Control System (TCS)", _subHeaderStyle);
                EditorGUILayout.PropertyField(_tcsProp, true);
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawTelemetryTab(VehicleController controller)
        {
            EditorGUILayout.BeginVertical(_cardBoxStyle);
            {
                EditorGUILayout.LabelField("Real-Time Vehicle Telemetry", _subHeaderStyle);

                if (!Application.isPlaying)
                {
                    EditorGUILayout.HelpBox("Live Telemetry is available while Unity is in Play Mode. Press Play to inspect real-time physics data.", MessageType.Info);
                    EditorGUILayout.EndVertical();
                    return;
                }

                VehicleState state = controller.State;
                VehicleInput input = controller.Input;
                Rigidbody rb = controller.Rigidbody;

                float speedMs = rb != null ? rb.linearVelocity.magnitude : 0f;
                float speedKmh = speedMs * 3.6f;
                string gearText = state.CurrentGear == -1 ? "R" : (state.CurrentGear == 0 ? "N" : state.CurrentGear.ToString());

                // Overview metrics
                EditorGUILayout.BeginHorizontal();
                {
                    EditorGUILayout.LabelField($"Speed: {speedKmh:F1} km/h ({speedMs:F1} m/s)", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField($"RPM: {state.EngineRpm:F0} RPM", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField($"Gear: {gearText} (Clutch: {state.ClutchEngagement * 100f:F0}%)", EditorStyles.boldLabel);
                }
                EditorGUILayout.EndHorizontal();

                // Driver Inputs
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField($"Inputs: Throttle: {input.Throttle * 100f:F0}% | Brake: {input.Brake * 100f:F0}% | Steer: {input.Steer * 100f:+0;-0;0}% | E-Brake: {(input.Handbrake > 0.5f ? "ON" : "OFF")}");

                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("4-Wheel Dynamics & Contact Patch", EditorStyles.boldLabel);

                string[] wheelTitles = { "Front-Left (FL)", "Front-Right (FR)", "Rear-Left (RL)", "Rear-Right (RR)" };

                for (int i = 0; i < 4; i++)
                {
                    WheelState ws = state.Wheels[i];
                    float radius = controller.ChassisConfig.WheelRadii[i];
                    float linearSpeed = ws.AngularVelocity * radius;

                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    {
                        EditorGUILayout.BeginHorizontal();
                        {
                            EditorGUILayout.LabelField($"<b>{wheelTitles[i]}</b>", _subHeaderStyle);
                            string contactText = ws.HasContact ? "[GROUNDED]" : "[AIRBORNE]";
                            GUIStyle cStyle = ws.HasContact ? _greenTextStyle : _redTextStyle;
                            EditorGUILayout.LabelField(contactText, cStyle, GUILayout.Width(90));
                        }
                        EditorGUILayout.EndHorizontal();

                        float compCm = ws.SuspensionCompression * 100f;
                        EditorGUILayout.LabelField($"Travel: {compCm:F1} cm | Damper: {ws.SuspensionVelocity:+0.00;-0.00;0.00} m/s | Load: {ws.NormalLoad:F0} N");
                        EditorGUILayout.LabelField($"Steer: {ws.SteerAngle * Mathf.Rad2Deg:+0.0;-0.0;0.0}° | Speed: {linearSpeed * 3.6f:F1} km/h | Omega: {ws.AngularVelocity:F1} rad/s");

                        Vector3 carForward = controller.transform.forward;
                        Vector3 carUp = controller.transform.up;
                        Vector3 wheelForward = Quaternion.AngleAxis(ws.SteerAngle * Mathf.Rad2Deg, carUp) * carForward;
                        Vector3 wheelRight = Vector3.Cross(carUp, wheelForward);
                        float fx = Vector3.Dot(ws.TireForce, wheelForward);
                        float fy = Vector3.Dot(ws.TireForce, wheelRight);

                        EditorGUILayout.LabelField($"Slip: Sx = {ws.SlipRatio:+0.00;-0.00;0.00} | Sy = {ws.SlipAngle * Mathf.Rad2Deg:+0.0;-0.0;0.0}° | Skid: {ws.SkidIntensity * 100f:F0}%");
                        EditorGUILayout.LabelField($"Forces: Fx (Long) = {fx:+0;-0;0} N | Fy (Lat) = {fy:+0;-0;0} N");
                    }
                    EditorGUILayout.EndVertical();
                }
            }
            EditorGUILayout.EndVertical();
        }

        private void InitStyles()
        {
            if (_headerStyle != null)
                return;

            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13
            };
            _headerStyle.normal.textColor = new Color(0.2f, 0.75f, 1.0f);

            _subHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 11
            };

            _cardBoxStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(10, 10, 8, 8)
            };

            _greenTextStyle = new GUIStyle(EditorStyles.boldLabel);
            _greenTextStyle.normal.textColor = new Color(0.25f, 0.85f, 0.35f);

            _redTextStyle = new GUIStyle(EditorStyles.boldLabel);
            _redTextStyle.normal.textColor = new Color(0.95f, 0.3f, 0.3f);

            _yellowTextStyle = new GUIStyle(EditorStyles.boldLabel);
            _yellowTextStyle.normal.textColor = new Color(0.95f, 0.75f, 0.2f);
        }
    }
}
