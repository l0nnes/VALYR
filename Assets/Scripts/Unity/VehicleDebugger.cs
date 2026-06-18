using Core.Contexts;
using Core.Math;
using UnityEngine;

namespace Unity.DebugTools
{
    [RequireComponent(typeof(VehicleController))]
    public class VehicleDebugger : MonoBehaviour
    {
        [Header("Settings")] [SerializeField] private KeyCode toggleKey = KeyCode.F3;
        [SerializeField] private bool showOnStart = true;
        [SerializeField] private float refreshRate = 0.05f;

        private VehicleController _controller;
        private bool _isVisible;
        private float _nextUpdate;
        private GUIStyle _style;
        private Rect _windowRect = new(20, 20, 750, 0);

        private void Awake()
        {
            _controller = GetComponent<VehicleController>();
            _isVisible = showOnStart;
        }

        private void Update()
        {
            if (UnityEngine.Input.GetKeyDown(toggleKey)) _isVisible = !_isVisible;
        }

        private void OnGUI()
        {
            if (!_isVisible || _controller.Core == null) return;

            if (_style == null)
                _style = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.UpperLeft,
                    fontSize = 12,
                    richText = true,
                    padding = new RectOffset(15, 15, 15, 15)
                };

            if (Time.time >= _nextUpdate) _nextUpdate = Time.time + refreshRate;

            _windowRect = GUILayout.Window(991, _windowRect, DrawWindow, "Vehicle Telemetry (F3)", _style);
        }

        private void DrawWindow(int id)
        {
            var ctx = _controller.Core.Context;

            GUILayout.BeginVertical();

            // Заголовок (Скорость и Инпуты)
            DrawHeader(ctx);
            GUILayout.Space(10);

            // Основной контент в две колонки
            GUILayout.BeginHorizontal();

            // Левая колонка: Двигатель, КПП и трансмиссия
            GUILayout.BeginVertical(GUILayout.Width(350));
            DrawPowertrain(ctx);
            GUILayout.Space(10);
            DrawGeneral(ctx);
            GUILayout.EndVertical();

            GUILayout.Space(20); // Разделитель

            // Правая колонка: Колеса и Подвеска
            GUILayout.BeginVertical(GUILayout.Width(350));
            DrawWheels(ctx);
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUI.DragWindow();
        }

        private void DrawHeader(VehicleContext ctx)
        {
            var vel = ctx.body.linearVelocity.Magnitude() * 3.6f;
            GUILayout.BeginHorizontal("box");
            GUILayout.Label($"<size=16><b>SPEED: {vel:F0} km/h</b></size>", GUILayout.Width(150));
            GUILayout.Label($"<b>Thr:</b> {ctx.input.throttle:F2}");
            GUILayout.Label($"<b>Brk:</b> {ctx.input.brake:F2}");
            GUILayout.Label($"<b>Str:</b> {ctx.input.steering:F2}");
            GUILayout.EndHorizontal();
        }

        private void DrawPowertrain(VehicleContext ctx)
        {
            var engine = ctx.engine;
            var transmission = ctx.transmission;

            GUILayout.BeginVertical("box");
            GUILayout.Label("<b><size=14>POWERTRAIN</size></b>");
            GUILayout.Space(5);

            var engineState = engine.isRunning ? "<color=green>ON</color>" : "<color=red>OFF</color>";
            if (engine.inCutoff) engineState = "<color=yellow>CUTOFF</color>";
            GUILayout.Label($"<b>Status:</b> {engineState}");

            var rpmPercent = MathUtil.Clamp01(engine.rpm / 7500f);
            var filled = (int)(rpmPercent * 30);
            var barColor = engine.rpm > 6000 ? "red" : "white";
            var barStr = new string('█', filled).PadRight(30, '░');
            GUILayout.Label($"<b>RPM:</b> <color={barColor}>[{barStr}]</color> {engine.rpm:F0}");

            GUILayout.Label($"<b>Engine Torque:</b> {engine.torque:F0} Nm");
            GUILayout.Label($"<b>Engine Power:</b> {engine.powerKw:F0} kW");
            GUILayout.Label($"<b>Load Torque:</b> {engine.loadTorque:F0} Nm");

            GUILayout.Space(10);

            var gearName =
                transmission.currentGear switch { 0 => "R", 1 => "N", _ => (transmission.currentGear - 1).ToString() };
            var mode = transmission.isAutomatic ? "AUTO" : "MANUAL";
            var shifting = transmission.isShifting ? " <color=yellow>SHIFT</color>" : "";
            var hold = transmission.isAutoHoldActive ? " <color=yellow>HOLD</color>" : "";
            var reverse = transmission.isBrakeReverseActive ? " <color=orange>BRAKE->R</color>" : "";
            GUILayout.Label($"<b>GEARBOX:</b> {mode}{shifting}");
            if (hold.Length > 0 || reverse.Length > 0) GUILayout.Label($"<b>Assist:</b>{hold}{reverse}");
            GUILayout.Label(
                $"<b>GEAR:</b>[ <size=14><color=cyan>{gearName}</color></size> ] (Ratio: {transmission.currentGearRatio:F2})");

            var clutchFilled = (int)(ctx.clutch.engagement * 20);
            var clutchBar = new string('█', clutchFilled).PadRight(20, '░');
            GUILayout.Label($"<b>Clutch:</b> [{clutchBar}] {ctx.clutch.engagement:P0}");
            GUILayout.Label($"<b>Output Torque:</b> {transmission.outputTorque:F0} Nm");
            GUILayout.Label($"<b>Input Shaft:</b> {transmission.inputShaftRpm:F0} rpm");

            GUILayout.EndVertical();
        }

        private void DrawGeneral(VehicleContext ctx)
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label("<b><size=14>CHASSIS</size></b>");
            GUILayout.Label($"<b>Mass:</b> {ctx.config.mass} kg");
            GUILayout.Label($"<b>LinVel:</b> {ctx.body.linearVelocity.Magnitude():F2} m/s");
            GUILayout.Label($"<b>AngVel:</b> {ctx.body.angularVelocity.Magnitude():F2} rad/s");
            GUILayout.EndVertical();
        }

        private void DrawWheels(VehicleContext ctx)
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label("<b><size=14>WHEELS & SUSPENSION</size></b>");
            GUILayout.Space(5);

            foreach (var wheel in ctx.wheels)
            {
                var t = wheel.state.tire;
                var s = wheel.state.suspension;
                var h = wheel.state.hit;

                var wName = $"Axle {wheel.axle.index} {(wheel.isLeft ? "L" : "R")}";
                var gnd = h.isGrounded ? "<color=green>GND</color>" : "<color=red>AIR</color>";

                GUILayout.Label($"<b>[{wName}]</b> - {gnd}");

                if (h.isGrounded)
                {
                    GUILayout.Label($"   Susp: {s.compressionRatio:P0} | {s.force:F0} N");
                    GUILayout.Label($"   Slip (R/A): {t.slipRatio:F2} / {t.slipAngle:F1}°");
                    GUILayout.Label($"   Force (L/L): {t.longForce:F0} N / {t.latForce:F0} N");
                    GUILayout.Label($"   Torque (Dr/Br): {wheel.state.driveTorque:F0} / {wheel.state.brakeTorque:F0}");
                }

                GUILayout.Space(5);
            }

            GUILayout.EndVertical();
        }
    }
}