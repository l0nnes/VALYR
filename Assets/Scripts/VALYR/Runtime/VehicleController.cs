using System;
using UnityEngine;
using VALYR.Core;
using VALYR.Core.Adapters;
using VALYR.Core.Interfaces;
using VALYR.Core.Math;
using VALYR.Core.Modules;
using VALYR.Modules.Assists;
using VALYR.Modules.Powertrain;
using VALYR.Modules.Steering;
using VALYR.Modules.Suspension;
using VALYR.Modules.Tires;
using VALYR.Runtime.Input;
using VALYR.Runtime.Visual;

namespace VALYR.Runtime
{
    /// <summary>
    /// Главный контроллер автомобиля для автономного (офлайн) режима симуляции.
    /// Оркестрирует модули подвески, рулевого управления, силовой установки, ассистентов и шин
    /// в строгом физическом конвейере FixedUpdate с нулевыми аллокациями в памяти.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(VehicleInputReader))]
    public class VehicleController : MonoBehaviour
    {
        [Header("Конфигурация геометрии шасси")]
        public VehicleChassisConfig ChassisConfig = new()
        {
            WheelOffsets = new WheelData4<Vector3>(
                new Vector3(-0.70f, 0.655f, 1.25f),  // FL
                new Vector3(0.70f, 0.655f, 1.25f),   // FR
                new Vector3(-0.70f, 0.655f, -1.20f), // RL
                new Vector3(0.70f, 0.655f, -1.20f)   // RR
            ),
            WheelRadii = new WheelData4<float>(0.31f, 0.31f, 0.31f, 0.31f),
            ChassisMass = 1180f
        };

        [Header("Слои дорожного покрытия")]
        [Tooltip("Слои коллайдеров дороги (исключаются машина и вспомогательные триггеры)")]
        public LayerMask GroundLayers = ~((1 << 2) | (1 << 3)); // Исключить IgnoreRaycast (2) и Car (3)

        [Header("Модули симуляции")]
        public RaycastSuspensionModule Suspension = new();
        public DirectSteeringModule Steering = new();
        public ArcadeEngineModule Engine = new();
        public SimpleTransmissionModule Transmission = new();
        public AntiRollBarModule AntiRollBar = new();
        public AbsModule Abs = new();
        public TcsModule Tcs = new();
        public BrushTireModule Tires = new();

        [Header("Текущее состояние симуляции (Debug)")]
        [SerializeField] private VehicleState _state;
        [SerializeField] private VehicleInput _input;

        [Header("Визуализация")]
        [SerializeField] private VehicleVisualView _visualView;

        // Физические адаптеры и шина данных
        private Rigidbody _rigidbody;
        private IPhysicsBody _physicsBody;
        private ISurfaceProvider _surfaceProvider;
        private VehicleInputReader _inputReader;
        private EngineOutput _lastEngineOutput;
        private WheelData4<WheelActuation> _wheelActuation;

        public VehicleState State => _state;
        public VehicleInput Input => _input;
        public Rigidbody Rigidbody => _rigidbody;

        private void Reset()
        {
            ApplyCivilianSedanPreset();
        }

        private void OnValidate()
        {
            if (_visualView == null)
                _visualView = GetComponent<VehicleVisualView>();
        }

        [ContextMenu("Apply Civilian Sedan Preset (Solaris / Logan)")]
        public void ApplyCivilianSedanPreset()
        {
            GroundLayers = ~((1 << 2) | (1 << 3));
            ChassisConfig.ChassisMass = 1180f;

            if (Suspension != null)
            {
                Suspension.SurfaceLayers = GroundLayers;
                Suspension.FrontAxle = SuspensionAxleSettings.DefaultFront;
                Suspension.RearAxle = SuspensionAxleSettings.DefaultRear;
            }

            if (Engine != null)
            {
                Engine.IdleRpm = 750f;
                Engine.RevLimiterRpm = 6500f;
                Engine.MaxRpm = 6800f;
                Engine.StallRpm = 2300f;
                Engine.MaxTorque = 155f;
                Engine.EngineBrakingTorque = 35f;
                Engine.FlywheelInertia = 0.16f;
                Engine.TorqueConverterMultiplication = 1.85f;
                Engine.TorqueCurve = new AnimationCurve(
                    new Keyframe(0f, 0.65f),
                    new Keyframe(0.25f, 0.85f),
                    new Keyframe(0.65f, 1.0f),
                    new Keyframe(0.90f, 0.92f),
                    new Keyframe(1.0f, 0.78f)
                );
            }

            if (Transmission != null)
            {
                Transmission.Drivetrain = DrivetrainType.FrontWheelDrive;
                Transmission.ReverseGearRatio = -3.487f;
                Transmission.ForwardGearRatios = new[] { 3.612f, 2.085f, 1.370f, 1.039f, 0.778f, 0.633f };
                Transmission.FinalDriveRatio = 3.943f;
                Transmission.TransmissionEfficiency = 0.90f;
                Transmission.AutomaticShift = true;
                Transmission.ShiftUpRpm = 5800f;
                Transmission.ShiftDownRpm = 1900f;
                Transmission.ShiftDuration = 0.25f;
                Transmission.MaxBrakeTorque = 1800f;
                Transmission.FrontBrakeBias = 0.72f;
                Transmission.MaxHandbrakeTorque = 2400f;
            }

            if (AntiRollBar != null)
            {
                AntiRollBar.FrontStiffness = 4500f;
                AntiRollBar.RearStiffness = 2200f;
            }

            if (Tires != null)
            {
                Tires.FrontTires = BrushTireParameters.Default;
                Tires.RearTires = BrushTireParameters.Default;
                Tires.WheelInertia = 1.1f;
            }

            if (_rigidbody != null)
            {
                _rigidbody.mass = ChassisConfig.ChassisMass;
                _rigidbody.centerOfMass = new Vector3(0f, -0.22f, 0.12f);
            }

            if (_visualView == null)
                _visualView = GetComponent<VehicleVisualView>();
        }

        [Header("Авто-применение пресета")]
        [Tooltip("Автоматически применить эталонные настройки гражданского седана (Solaris / Logan)")]
        public bool AutoApplyCivilianPreset = true;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _inputReader = GetComponent<VehicleInputReader>();

            if (AutoApplyCivilianPreset)
            {
                ApplyCivilianSedanPreset();
            }

            if (_rigidbody != null)
            {
                _rigidbody.mass = ChassisConfig.ChassisMass;
                _rigidbody.centerOfMass = new Vector3(0f, -0.22f, 0.12f);
            }

            if (_visualView == null)
                _visualView = GetComponent<VehicleVisualView>();

            // Адаптеры к физическому движку
            _physicsBody = new UnityRigidbodyPhysicsBody(_rigidbody);
            _surfaceProvider = new UnityPhysicsSurfaceProvider();

            // Передача маски слоев поверхности в подвеску
            Suspension.SurfaceLayers = GroundLayers;

            // Инициализация модулей геометрией шасси
            Suspension.Initialize(in ChassisConfig);
            Steering.Initialize(in ChassisConfig);
            Engine.Initialize(in ChassisConfig);
            Transmission.Initialize(in ChassisConfig);
            AntiRollBar.Initialize(in ChassisConfig);
            Abs.Initialize(in ChassisConfig);
            Tcs.Initialize(in ChassisConfig);
            Tires.Initialize(in ChassisConfig);

            // Начальное состояние
            _state = default;
            _state.EngineRpm = Engine.IdleRpm;
            _state.CurrentGear = 1;
            _state.ClutchEngagement = 1f;

            _wheelActuation = default;
            _wheelActuation.Reset();
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;

            // 1. Опрос ввода
            _input = _inputReader != null ? _inputReader.ReadInput(dt) : default;

            // 2. Шаг физической симуляции
            SimulateStep(in _input, ref _state, dt);
        }

        private void Update()
        {
            // Обновление визуала колес в Update/LateUpdate для плавного 60+ FPS рендеринга
            if (_visualView != null)
            {
                _visualView.UpdateWheelsView(in _state, Time.deltaTime);
            }
        }

        /// <summary>
        /// Выполняет один чистый шаг симуляции физики автомобиля.
        /// Архитектурно идентичен методу Simulate() в сетевом PredictedIdentity для Client-Side Prediction.
        /// </summary>
        public void SimulateStep(in VehicleInput input, ref VehicleState state, float dt)
        {
            state.Tick++;

            // 1. Рулевое управление (обновляет SteerAngle передних колес)
            Steering.UpdateSteering(in input, ref state, _physicsBody, in ChassisConfig, dt);

            // 2. Подвеска (raycast, пружины, демпферы, bump stops, заполняет NormalLoad)
            Suspension.SolveSuspension(ref state, _physicsBody, _surfaceProvider, in ChassisConfig, dt);

            // 3. Стабилизация поперечной устойчивости (Anti-Roll Bar переносит NormalLoad)
            AntiRollBar.ApplyChassisAssists(ref state, _physicsBody, in ChassisConfig, dt);

            // 4. Силовая установка и трансмиссия
            _wheelActuation.Reset();

            Transmission.UpdateTransmission(
                in input,
                in _lastEngineOutput,
                ref state,
                ref _wheelActuation,
                in ChassisConfig,
                dt,
                out var feedback);

            Engine.UpdateEngine(
                in input,
                in feedback,
                ref state,
                dt,
                out _lastEngineOutput);

            // 5. Электронные помощники колес (ABS и TCS модулируют лимиты моментов)
            Abs.ApplyAssists(in input, in state, ref _wheelActuation, _physicsBody, in ChassisConfig, dt);
            Tcs.ApplyAssists(in input, in state, ref _wheelActuation, _physicsBody, in ChassisConfig, dt);

            // 6. Шины и кинематика колес (Brush Tire Model + WheelDynamics интегратор)
            Tires.SolveTires(ref state, in _wheelActuation, _physicsBody, in ChassisConfig, dt);
        }

        private void OnDrawGizmosSelected()
        {
            if (_physicsBody == null)
                return;

            Vector3 bodyPos = transform.position;
            Quaternion bodyRot = transform.rotation;
            Vector3 suspDown = -(bodyRot * Vector3.up);

            for (int i = 0; i < 4; i++)
            {
                Vector3 mountPoint = bodyPos + bodyRot * ChassisConfig.WheelOffsets[i];
                float radius = ChassisConfig.WheelRadii[i];
                float restLength = Suspension.FrontAxle.RestLength;

                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(mountPoint, mountPoint + suspDown * (restLength + radius));

                if (Application.isPlaying)
                {
                    WheelState ws = _state.Wheels[i];
                    if (ws.HasContact)
                    {
                        // Точка контакта и сила
                        Gizmos.color = Color.green;
                        Gizmos.DrawSphere(ws.ContactPoint, 0.05f);
                        Gizmos.color = Color.red;
                        Gizmos.DrawRay(ws.ContactPoint, ws.TireForce * 0.001f);
                    }
                }
            }
        }
    }
}
