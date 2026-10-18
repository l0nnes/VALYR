using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using VALYR.Core;

namespace VALYR.Runtime.UI
{
    /// <summary>
    /// Real-time on-screen telemetry display HUD (OnGUI).
    /// Displays chassis speed, engine RPM, current gear, suspension travel,
    /// normal loads, tire slip ratios, slip angles, and contact forces.
    /// Toggle visibility with F1 key.
    /// </summary>
    [RequireComponent(typeof(VehicleController))]
    public class VehicleTelemetryHUD : MonoBehaviour
    {
        [Header("Display Options")]
        [Tooltip("Whether to draw telemetry HUD on screen.")]
        public bool ShowHUD = true;

        [Tooltip("Keyboard shortcut key to toggle HUD visibility.")]
        public Key ToggleKey = Key.F1;

        [Header("Position & Dimensions")]
        public Rect WindowRect = new Rect(20, 20, 720, 480);

        private VehicleController _controller;
        private GUIStyle _boxStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _greenStyle;
        private GUIStyle _redStyle;
        private GUIStyle _yellowStyle;

        private readonly StringBuilder _sb = new StringBuilder(2048);
        private static readonly string[] WheelNames = { "FL (Front-Left)", "FR (Front-Right)", "RL (Rear-Left)", "RR (Rear-Right)" };

        private void Awake()
        {
            _controller = GetComponent<VehicleController>();
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current[ToggleKey].wasPressedThisFrame)
            {
                ShowHUD = !ShowHUD;
            }
        }

        private void OnGUI()
        {
            if (!ShowHUD || _controller == null)
                return;

            InitStyles();

            GUILayout.BeginArea(WindowRect, GUIContent.none, _boxStyle);
            {
                VehicleState state = _controller.State;
                VehicleInput input = _controller.Input;
                Rigidbody rb = _controller.Rigidbody;

                float speedMs = rb != null ? rb.linearVelocity.magnitude : 0f;
                float speedKmh = speedMs * 3.6f;

                // Header
                GUILayout.Label($"<b>VALYR VEHICLE TELEMETRY</b> (Press [F1] to toggle)", _headerStyle);
                GUILayout.Space(4);

                // 1. General vehicle parameters
                string gearText = state.CurrentGear == -1 ? "R" : (state.CurrentGear == 0 ? "N" : state.CurrentGear.ToString());
                GUILayout.Label($"<b>Speed:</b> {speedKmh:F1} km/h ({speedMs:F1} m/s) | <b>Engine:</b> {state.EngineRpm:F0} RPM | <b>Gear:</b> {gearText} | <b>Clutch:</b> {state.ClutchEngagement * 100f:F0}%", _labelStyle);

                // Driver Inputs
                GUILayout.Label($"<b>Input:</b> Throttle: [{GetBar(input.Throttle)}] {input.Throttle * 100f:F0}% | Brake: [{GetBar(input.Brake)}] {input.Brake * 100f:F0}% | Steer: {input.Steer * 100f:+0;-0;0}% | E-Brake: {(input.Handbrake > 0.5f ? "ON" : "OFF")}", _labelStyle);

                GUILayout.Space(8);
                GUILayout.Label("<b>WHEEL KINEMATICS & SUSPENSION STATE:</b>", _labelStyle);

                // 2. 4-Wheels telemetry cards
                GUILayout.BeginHorizontal();
                {
                    for (int i = 0; i < 4; i++)
                    {
                        WheelState ws = state.Wheels[i];
                        float radius = _controller.ChassisConfig.WheelRadii[i];
                        float wheelLinearSpeed = ws.AngularVelocity * radius;
                        float wheelRpm = (ws.AngularVelocity * 60f) / (2f * Mathf.PI);

                        GUILayout.BeginVertical("box", GUILayout.Width(170));
                        {
                            GUILayout.Label($"<b>{WheelNames[i]}</b>", _headerStyle);

                            // Ground Contact
                            if (ws.HasContact)
                            {
                                GUILayout.Label("Status: <b>[GROUNDED]</b>", _greenStyle);
                            }
                            else
                            {
                                GUILayout.Label("Status: <b>[AIRBORNE]</b>", _redStyle);
                            }

                            // Suspension
                            float compCm = ws.SuspensionCompression * 100f;
                            float restCm = (_controller.Suspension is VALYR.Modules.Suspension.RaycastSuspensionModule raySusp ? raySusp.FrontAxle.RestLength : 0.35f) * 100f;
                            GUILayout.Label($"Travel: {compCm:F1} cm / {restCm:F1} cm", _labelStyle);
                            GUILayout.Label($"Damper Vel: {ws.SuspensionVelocity:+0.00;-0.00;0.00} m/s", _labelStyle);
                            GUILayout.Label($"Load Fz: <b>{ws.NormalLoad:F0} N</b>", _labelStyle);

                            // Kinematics
                            GUILayout.Space(4);
                            GUILayout.Label($"Steer: {ws.SteerAngle * Mathf.Rad2Deg:+0.0;-0.0;0.0}°", _labelStyle);
                            GUILayout.Label($"Wheel RPM: {wheelRpm:F0} RPM", _labelStyle);
                            GUILayout.Label($"Wheel Speed: {wheelLinearSpeed * 3.6f:F1} km/h", _labelStyle);
                            GUILayout.Label($"Omega: {ws.AngularVelocity:F1} rad/s", _labelStyle);

                            // Grip & Slip
                            GUILayout.Space(4);
                            GUIStyle slipStyle = (Mathf.Abs(ws.SlipRatio) > 0.25f || Mathf.Abs(ws.SlipAngle) > 0.2f) ? _yellowStyle : _labelStyle;
                            GUILayout.Label($"Slip Ratio: {ws.SlipRatio:+0.00;-0.00;0.00}", slipStyle);
                            GUILayout.Label($"Slip Angle: {ws.SlipAngle * Mathf.Rad2Deg:+0.0;-0.0;0.0}°", slipStyle);

                            Vector3 carForward = _controller.transform.forward;
                            Vector3 carUp = _controller.transform.up;
                            Vector3 wheelForward = Quaternion.AngleAxis(ws.SteerAngle * Mathf.Rad2Deg, carUp) * carForward;
                            Vector3 wheelRight = Vector3.Cross(carUp, wheelForward);
                            float fxLocal = Vector3.Dot(ws.TireForce, wheelForward);
                            float fyLocal = Vector3.Dot(ws.TireForce, wheelRight);

                            GUILayout.Label($"Traction Fx: {fxLocal:+0;-0;0} N", _labelStyle);
                            GUILayout.Label($"Lateral Fy: {fyLocal:+0;-0;0} N", _labelStyle);
                            GUILayout.Label($"Skid Intensity: {ws.SkidIntensity * 100f:F0}%", ws.SkidIntensity > 0.3f ? _yellowStyle : _labelStyle);
                        }
                        GUILayout.EndVertical();
                    }
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndArea();
        }

        private static string GetBar(float value, int totalSegments = 10)
        {
            int filled = Mathf.RoundToInt(Mathf.Clamp01(value) * totalSegments);
            return new string('=', filled) + new string('-', totalSegments - filled);
        }

        private void InitStyles()
        {
            if (_boxStyle != null)
                return;

            _boxStyle = new GUIStyle(GUI.skin.box);
            Texture2D darkBg = new Texture2D(1, 1);
            darkBg.SetPixel(0, 0, new Color(0.05f, 0.05f, 0.08f, 0.88f));
            darkBg.Apply();
            _boxStyle.normal.background = darkBg;

            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _headerStyle.normal.textColor = new Color(0.3f, 0.85f, 1.0f);

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11
            };
            _labelStyle.normal.textColor = Color.white;

            _greenStyle = new GUIStyle(_labelStyle);
            _greenStyle.normal.textColor = new Color(0.3f, 1.0f, 0.4f);

            _redStyle = new GUIStyle(_labelStyle);
            _redStyle.normal.textColor = new Color(1.0f, 0.35f, 0.35f);

            _yellowStyle = new GUIStyle(_labelStyle);
            _yellowStyle.normal.textColor = new Color(1.0f, 0.85f, 0.2f);
        }
    }
}
