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
    /// Master vehicle simulation controller for offline and standalone simulation.
    /// Orchestrates suspension, steering, powertrain, driver assists, and brush tire modules
    /// in a deterministic, zero-allocation FixedUpdate physics pipeline.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(VehicleInputReader))]
    public class VehicleController : MonoBehaviour
    {
        [Header("Chassis Geometry & Mass")]
        [Tooltip("Geometry configuration including wheel positions, rolling radii, and vehicle mass.")]
        public VehicleChassisConfig ChassisConfig = VehicleChassisConfig.Default;

        [Header("Road Surface Layers")]
        [Tooltip("Collision layer mask for road surfaces (excludes vehicle layer and triggers).")]
        public LayerMask GroundLayers = ~((1 << 2) | (1 << 3)); // Exclude IgnoreRaycast (2) and Car (3)

        [Header("Subsystem Enable Toggles")]
        [Tooltip("Enable raycast suspension physics.")]
        public bool EnableSuspension = true;

        [Tooltip("Enable steering angle kinematics.")]
        public bool EnableSteering = true;

        [Tooltip("Enable engine and transmission powertrain simulation.")]
        public bool EnablePowertrain = true;

        [Tooltip("Enable anti-roll bar chassis stabilization.")]
        public bool EnableAntiRollBar = true;

        [Tooltip("Enable Anti-lock Braking System (ABS).")]
        public bool EnableABS = true;

        [Tooltip("Enable Traction Control System (TCS).")]
        public bool EnableTCS = true;

        [Header("Simulation Subsystems")]
        public RaycastSuspensionModule Suspension = new();
        public DirectSteeringModule Steering = new();
        public ArcadeEngineModule Engine = new();
        public SimpleTransmissionModule Transmission = new();
        public AntiRollBarModule AntiRollBar = new();
        public AbsModule Abs = new();
        public TcsModule Tcs = new();
        public BrushTireModule Tires = new();

        [Header("Live State Debug")]
        [SerializeField] private VehicleState _state;
        [SerializeField] private VehicleInput _input;

        [Header("Visual Presentation")]
        [SerializeField] private VehicleVisualView _visualView;

        // Physics adapters and data buffers
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

            SyncModuleEnableStates();
        }

        /// <summary>
        /// Synchronizes inspector toggle booleans with internal module IsEnabled flags.
        /// </summary>
        public void SyncModuleEnableStates()
        {
            if (Suspension != null) Suspension.IsEnabled = EnableSuspension;
            if (Steering != null) Steering.IsEnabled = EnableSteering;
            if (Engine != null) Engine.IsEnabled = EnablePowertrain;
            if (Transmission != null) Transmission.IsEnabled = EnablePowertrain;
            if (AntiRollBar != null) AntiRollBar.IsEnabled = EnableAntiRollBar;
            if (Abs != null) Abs.IsEnabled = EnableABS;
            if (Tcs != null) Tcs.IsEnabled = EnableTCS;
        }

        [ContextMenu("Presets/Apply Civilian Sedan (Solaris / Logan)")]
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

            if (Steering != null)
            {
                Steering.MaxSteerAngleDeg = 36f;
                Steering.SteeringResponseRate = 16f;
                Steering.EnableSpeedSensitivity = true;
                Steering.MinSteerAngleAtHighSpeedDeg = 12f;
                Steering.HighSpeedThreshold = 40f;
                Steering.UseAckermann = true;
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

            if (Abs != null)
            {
                Abs.SlipThreshold = -0.18f;
                Abs.MinSpeedThreshold = 2.5f;
                Abs.PressureReleaseRatio = 0.35f;
            }

            if (Tcs != null)
            {
                Tcs.SlipThreshold = 0.20f;
                Tcs.TorqueReductionRatio = 0.35f;
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

            SyncModuleEnableStates();
        }

        [ContextMenu("Presets/Apply Sport Coupe (RWD V8)")]
        public void ApplySportCoupePreset()
        {
            GroundLayers = ~((1 << 2) | (1 << 3));
            ChassisConfig.ChassisMass = 1450f;

            if (Suspension != null)
            {
                Suspension.SurfaceLayers = GroundLayers;
                Suspension.FrontAxle = new SuspensionAxleSettings
                {
                    RestLength = 0.30f,
                    MaxTravel = 0.18f,
                    SpringStiffness = 38000f,
                    DamperCompressionRate = 3200f,
                    DamperReboundRate = 4800f,
                    BumpStopThresholdRatio = 0.80f,
                    BumpStopStiffness = 160000f
                };
                Suspension.RearAxle = new SuspensionAxleSettings
                {
                    RestLength = 0.30f,
                    MaxTravel = 0.18f,
                    SpringStiffness = 34000f,
                    DamperCompressionRate = 2800f,
                    DamperReboundRate = 4200f,
                    BumpStopThresholdRatio = 0.80f,
                    BumpStopStiffness = 160000f
                };
            }

            if (Steering != null)
            {
                Steering.MaxSteerAngleDeg = 34f;
                Steering.SteeringResponseRate = 22f;
                Steering.EnableSpeedSensitivity = true;
                Steering.MinSteerAngleAtHighSpeedDeg = 10f;
                Steering.HighSpeedThreshold = 45f;
                Steering.UseAckermann = true;
            }

            if (Engine != null)
            {
                Engine.IdleRpm = 850f;
                Engine.RevLimiterRpm = 7400f;
                Engine.MaxRpm = 7800f;
                Engine.StallRpm = 2800f;
                Engine.MaxTorque = 480f;
                Engine.EngineBrakingTorque = 55f;
                Engine.FlywheelInertia = 0.22f;
                Engine.TorqueConverterMultiplication = 1.6f;
                Engine.TorqueCurve = new AnimationCurve(
                    new Keyframe(0f, 0.70f),
                    new Keyframe(0.30f, 0.88f),
                    new Keyframe(0.70f, 1.0f),
                    new Keyframe(0.92f, 0.95f),
                    new Keyframe(1.0f, 0.85f)
                );
            }

            if (Transmission != null)
            {
                Transmission.Drivetrain = DrivetrainType.RearWheelDrive;
                Transmission.ReverseGearRatio = -3.20f;
                Transmission.ForwardGearRatios = new[] { 3.25f, 2.10f, 1.50f, 1.15f, 0.90f, 0.72f };
                Transmission.FinalDriveRatio = 3.73f;
                Transmission.TransmissionEfficiency = 0.92f;
                Transmission.AutomaticShift = true;
                Transmission.ShiftUpRpm = 6800f;
                Transmission.ShiftDownRpm = 2400f;
                Transmission.ShiftDuration = 0.18f;
                Transmission.MaxBrakeTorque = 3000f;
                Transmission.FrontBrakeBias = 0.65f;
                Transmission.MaxHandbrakeTorque = 3500f;
            }

            if (AntiRollBar != null)
            {
                AntiRollBar.FrontStiffness = 7000f;
                AntiRollBar.RearStiffness = 5500f;
            }

            if (Tires != null)
            {
                Tires.FrontTires = new BrushTireParameters
                {
                    LongitudinalStiffness = 30000f,
                    LateralStiffness = 26000f,
                    FrictionCoefficientX = 1.15f,
                    FrictionCoefficientY = 1.10f,
                    LowSpeedThreshold = 0.5f
                };
                Tires.RearTires = new BrushTireParameters
                {
                    LongitudinalStiffness = 32000f,
                    LateralStiffness = 28000f,
                    FrictionCoefficientX = 1.18f,
                    FrictionCoefficientY = 1.12f,
                    LowSpeedThreshold = 0.5f
                };
                Tires.WheelInertia = 1.3f;
            }

            if (_rigidbody != null)
            {
                _rigidbody.mass = ChassisConfig.ChassisMass;
                _rigidbody.centerOfMass = new Vector3(0f, -0.28f, -0.05f);
            }

            SyncModuleEnableStates();
        }

        [ContextMenu("Presets/Apply Off-Road SUV (AWD 4x4)")]
        public void ApplyOffroadPreset()
        {
            GroundLayers = ~((1 << 2) | (1 << 3));
            ChassisConfig.ChassisMass = 1950f;

            if (Suspension != null)
            {
                Suspension.SurfaceLayers = GroundLayers;
                Suspension.FrontAxle = new SuspensionAxleSettings
                {
                    RestLength = 0.44f,
                    MaxTravel = 0.32f,
                    SpringStiffness = 22000f,
                    DamperCompressionRate = 2200f,
                    DamperReboundRate = 3200f,
                    BumpStopThresholdRatio = 0.85f,
                    BumpStopStiffness = 100000f
                };
                Suspension.RearAxle = new SuspensionAxleSettings
                {
                    RestLength = 0.44f,
                    MaxTravel = 0.32f,
                    SpringStiffness = 24000f,
                    DamperCompressionRate = 2400f,
                    DamperReboundRate = 3400f,
                    BumpStopThresholdRatio = 0.85f,
                    BumpStopStiffness = 100000f
                };
            }

            if (Steering != null)
            {
                Steering.MaxSteerAngleDeg = 32f;
                Steering.SteeringResponseRate = 12f;
                Steering.EnableSpeedSensitivity = true;
                Steering.MinSteerAngleAtHighSpeedDeg = 14f;
                Steering.HighSpeedThreshold = 35f;
                Steering.UseAckermann = true;
            }

            if (Engine != null)
            {
                Engine.IdleRpm = 700f;
                Engine.RevLimiterRpm = 5800f;
                Engine.MaxRpm = 6200f;
                Engine.StallRpm = 2000f;
                Engine.MaxTorque = 420f;
                Engine.EngineBrakingTorque = 60f;
                Engine.FlywheelInertia = 0.30f;
                Engine.TorqueConverterMultiplication = 2.1f;
                Engine.TorqueCurve = new AnimationCurve(
                    new Keyframe(0f, 0.75f),
                    new Keyframe(0.20f, 0.95f),
                    new Keyframe(0.50f, 1.0f),
                    new Keyframe(0.85f, 0.88f),
                    new Keyframe(1.0f, 0.75f)
                );
            }

            if (Transmission != null)
            {
                Transmission.Drivetrain = DrivetrainType.AllWheelDrive;
                Transmission.ReverseGearRatio = -4.0f;
                Transmission.ForwardGearRatios = new[] { 4.20f, 2.45f, 1.60f, 1.15f, 0.85f, 0.68f };
                Transmission.FinalDriveRatio = 4.10f;
                Transmission.TransmissionEfficiency = 0.88f;
                Transmission.AutomaticShift = true;
                Transmission.ShiftUpRpm = 5200f;
                Transmission.ShiftDownRpm = 1700f;
                Transmission.ShiftDuration = 0.30f;
                Transmission.MaxBrakeTorque = 2600f;
                Transmission.FrontBrakeBias = 0.60f;
                Transmission.MaxHandbrakeTorque = 3000f;
            }

            if (AntiRollBar != null)
            {
                AntiRollBar.FrontStiffness = 2500f;
                AntiRollBar.RearStiffness = 1800f;
            }

            if (Tires != null)
            {
                Tires.FrontTires = new BrushTireParameters
                {
                    LongitudinalStiffness = 20000f,
                    LateralStiffness = 16000f,
                    FrictionCoefficientX = 1.05f,
                    FrictionCoefficientY = 1.00f,
                    LowSpeedThreshold = 0.6f
                };
                Tires.RearTires = Tires.FrontTires;
                Tires.WheelInertia = 1.8f;
            }

            if (_rigidbody != null)
            {
                _rigidbody.mass = ChassisConfig.ChassisMass;
                _rigidbody.centerOfMass = new Vector3(0f, -0.15f, 0.05f);
            }

            SyncModuleEnableStates();
        }

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _inputReader = GetComponent<VehicleInputReader>();

            if (_rigidbody != null)
            {
                _rigidbody.mass = ChassisConfig.ChassisMass;
                _rigidbody.centerOfMass = new Vector3(0f, -0.22f, 0.12f);
            }

            if (_visualView == null)
                _visualView = GetComponent<VehicleVisualView>();

            // Physical adapters
            _physicsBody = new UnityRigidbodyPhysicsBody(_rigidbody);
            _surfaceProvider = new UnityPhysicsSurfaceProvider();

            // Synchronize layer mask with suspension
            if (Suspension != null)
                Suspension.SurfaceLayers = GroundLayers;

            SyncModuleEnableStates();

            // Initialize all modules with chassis geometry
            Suspension?.Initialize(in ChassisConfig);
            Steering?.Initialize(in ChassisConfig);
            Engine?.Initialize(in ChassisConfig);
            Transmission?.Initialize(in ChassisConfig);
            AntiRollBar?.Initialize(in ChassisConfig);
            Abs?.Initialize(in ChassisConfig);
            Tcs?.Initialize(in ChassisConfig);
            Tires?.Initialize(in ChassisConfig);

            // Initial powertrain state
            _state = default;
            _state.EngineRpm = Engine != null ? Engine.IdleRpm : 800f;
            _state.CurrentGear = 1;
            _state.ClutchEngagement = 1f;

            _wheelActuation = default;
            _wheelActuation.Reset();
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;

            // 1. Read driver or AI input
            _input = _inputReader != null ? _inputReader.ReadInput(dt) : default;

            // 2. Execute deterministic physical simulation step
            SimulateStep(in _input, ref _state, dt);
        }

        private void Update()
        {
            // Update wheel mesh visual positions and rotations
            if (_visualView != null)
            {
                _visualView.UpdateWheelsView(in _state, Time.deltaTime);
            }
        }

        /// <summary>
        /// Executes a single discrete vehicle physics simulation step.
        /// Modular and guarded by enable flags for live designer testing and network prediction.
        /// </summary>
        public void SimulateStep(in VehicleInput input, ref VehicleState state, float dt)
        {
            state.Tick++;

            // 1. Steering kinematics (updates front wheel steer angles)
            if (EnableSteering && Steering != null && Steering.IsEnabled)
            {
                Steering.UpdateSteering(in input, ref state, _physicsBody, in ChassisConfig, dt);
            }

            // 2. Suspension rays and spring-damper solver (computes normal loads)
            if (EnableSuspension && Suspension != null && Suspension.IsEnabled)
            {
                Suspension.SolveSuspension(ref state, _physicsBody, _surfaceProvider, in ChassisConfig, dt);
            }

            // 3. Chassis anti-roll bar stabilization
            if (EnableAntiRollBar && AntiRollBar != null && AntiRollBar.IsEnabled)
            {
                AntiRollBar.ApplyChassisAssists(ref state, _physicsBody, in ChassisConfig, dt);
            }

            // 4. Powertrain (Engine & Transmission)
            _wheelActuation.Reset();

            if (EnablePowertrain && Transmission != null && Transmission.IsEnabled && Engine != null && Engine.IsEnabled)
            {
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
            }

            // 5. Electronic wheel assists (ABS & TCS limit torque buffers)
            if (EnableABS && Abs != null && Abs.IsEnabled)
            {
                Abs.ApplyAssists(in input, in state, ref _wheelActuation, _physicsBody, in ChassisConfig, dt);
            }

            if (EnableTCS && Tcs != null && Tcs.IsEnabled)
            {
                Tcs.ApplyAssists(in input, in state, ref _wheelActuation, _physicsBody, in ChassisConfig, dt);
            }

            // 6. Tires and rotational dynamics (Brush tire model + WheelDynamics solver)
            if (Tires != null && Tires.IsEnabled)
            {
                Tires.SolveTires(ref state, in _wheelActuation, _physicsBody, in ChassisConfig, dt);
            }
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
                float restLength = Suspension != null ? Suspension.FrontAxle.RestLength : 0.35f;

                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(mountPoint, mountPoint + suspDown * (restLength + radius));

                if (Application.isPlaying)
                {
                    WheelState ws = _state.Wheels[i];
                    if (ws.HasContact)
                    {
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
