using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using VALYR.Core;

namespace VALYR.Runtime.UI
{
    /// <summary>
    /// Экранная панель телеметрии физики автомобиля в реальном времени (OnGUI HUD).
    /// Выводит параметры шасси, подвески, оборотов мотора, сил шин и проскальзывания каждого колеса.
    /// Переключается клавишей F1.
    /// </summary>
    [RequireComponent(typeof(VehicleController))]
    public class VehicleTelemetryHUD : MonoBehaviour
    {
        [Header("Отображение")]
        [Tooltip("Показывать ли телеметрию на экране")]
        public bool ShowHUD = true;

        [Tooltip("Клавиша включения/выключения HUD (F1)")]
        public Key ToggleKey = Key.F1;

        [Header("Позиция и стиль")]
        public Rect WindowRect = new Rect(20, 20, 720, 480);

        private VehicleController _controller;
        private GUIStyle _boxStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _greenStyle;
        private GUIStyle _redStyle;
        private GUIStyle _yellowStyle;

        private readonly StringBuilder _sb = new StringBuilder(2048);
        private static readonly string[] WheelNames = { "FL (Перед-Лев)", "FR (Перед-Прав)", "RL (Зад-Лев)", "RR (Зад-Прав)" };

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

                // Заголовок
                GUILayout.Label($"<b>VALYR VEHICLE TELEMETRY</b> (Нажмите [F1] чтобы скрыть)", _headerStyle);
                GUILayout.Space(4);

                // 1. Общие параметры автомобиля
                string gearText = state.CurrentGear == -1 ? "R" : (state.CurrentGear == 0 ? "N" : state.CurrentGear.ToString());
                GUILayout.Label($"<b>Скорость:</b> {speedKmh:F1} км/ч ({speedMs:F1} м/с) | <b>Обороты:</b> {state.EngineRpm:F0} RPM | <b>Передача:</b> {gearText} | <b>Сцепление:</b> {state.ClutchEngagement * 100f:F0}%", _labelStyle);

                // Полосы ввода
                GUILayout.Label($"<b>Ввод:</b> Газ: [{GetBar(input.Throttle)}] {input.Throttle * 100f:F0}% | Тормоз: [{GetBar(input.Brake)}] {input.Brake * 100f:F0}% | Руль: {input.Steer * 100f:+0;-0;0}% | Ручник: {(input.Handbrake > 0.5f ? "ВКЛ" : "ВЫКЛ")}", _labelStyle);

                GUILayout.Space(8);
                GUILayout.Label("<b>СОСТОЯНИЕ КОЛЕС И ПОДВЕСКИ:</b>", _labelStyle);

                // 2. Таблица параметров по 4 колесам
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

                            // Контакт
                            if (ws.HasContact)
                            {
                                GUILayout.Label("Статус: <b>[КОНТАКТ]</b>", _greenStyle);
                            }
                            else
                            {
                                GUILayout.Label("Статус: <b>[В ВОЗДУХЕ]</b>", _redStyle);
                            }

                            // Подвеска
                            float compCm = ws.SuspensionCompression * 100f;
                            float restCm = _controller.Suspension.FrontAxle.RestLength * 100f;
                            GUILayout.Label($"Сжатие: {compCm:F1} см / {restCm:F1} см", _labelStyle);
                            GUILayout.Label($"Скор. сжатия: {ws.SuspensionVelocity:+0.00;-0.00;0.00} м/с", _labelStyle);
                            GUILayout.Label($"Нагрузка Fz: <b>{ws.NormalLoad:F0} Н</b>", _labelStyle);

                            // Кинематика колеса
                            GUILayout.Space(4);
                            GUILayout.Label($"Угол руля: {ws.SteerAngle * Mathf.Rad2Deg:+0.0;-0.0;0.0}°", _labelStyle);
                            GUILayout.Label($"Обороты: {wheelRpm:F0} RPM", _labelStyle);
                            GUILayout.Label($"Скорость: {wheelLinearSpeed * 3.6f:F1} км/ч", _labelStyle);
                            GUILayout.Label($"Omega: {ws.AngularVelocity:F1} рад/с", _labelStyle);

                            // Сцепление и скольжение
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

                            GUILayout.Label($"Сила Fx (тяга): {fxLocal:+0;-0;0} Н", _labelStyle);
                            GUILayout.Label($"Сила Fy (бок): {fyLocal:+0;-0;0} Н", _labelStyle);
                            GUILayout.Label($"Skid: {ws.SkidIntensity * 100f:F0}%", ws.SkidIntensity > 0.3f ? _yellowStyle : _labelStyle);
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
