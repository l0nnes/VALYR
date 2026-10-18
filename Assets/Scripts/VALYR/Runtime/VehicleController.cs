using System;
using System.Collections.Generic;
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
    /// Four wheel suspension anchor points defined by Transform references in the vehicle hierarchy.
    /// Drag suspension or wheel anchor transforms directly into these slots in the Inspector.
    /// </summary>
    [Serializable]
    public struct WheelMounts
    {
        [Tooltip("Front-Left wheel anchor Transform.")]
        public Transform FrontLeft;

        [Tooltip("Front-Right wheel anchor Transform.")]
        public Transform FrontRight;

        [Tooltip("Rear-Left wheel anchor Transform.")]
        public Transform RearLeft;

        [Tooltip("Rear-Right wheel anchor Transform.")]
        public Transform RearRight;

        public Transform this[int index] => index switch
        {
            0 => FrontLeft,
            1 => FrontRight,
            2 => RearLeft,
            3 => RearRight,
            _ => null
        };
    }

    /// <summary>
    /// Modular vehicle controller.
    /// Subsystems are assigned dynamically per category.
    /// Wheel offsets are determined directly by dragging hierarchy Transforms.
    /// Configuration presets are managed via ScriptableObject assets.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(VehicleInputReader))]
    public class VehicleController : MonoBehaviour
    {
        [Header("Vehicle Preset")]
        [Tooltip("Optional ScriptableObject preset asset holding vehicle and module configurations.")]
        public VehiclePreset Preset;

        [Header("Chassis & Wheel Mounts")]
        [Tooltip("Transforms in vehicle hierarchy defining the 4 wheel anchor positions.")]
        public WheelMounts WheelMounts;

        [Tooltip("Unloaded outer rolling radius of the wheels in meters.")]
        [Range(0.15f, 0.70f)]
        public float WheelRadius = 0.31f;

        [Tooltip("Curb mass of the vehicle chassis in kg.")]
        [Min(100f)]
        public float ChassisMass = 1180f;

        [Tooltip("Custom center of mass offset relative to vehicle origin.")]
        public Vector3 CenterOfMass = new Vector3(0f, -0.22f, 0.12f);

        [Tooltip("Road surface collision layer mask.")]
        public LayerMask GroundLayers = ~((1 << 2) | (1 << 3)); // Exclude IgnoreRaycast (2) and Car (3)

        [Header("Modular Subsystems")]
        [SerializeReference] public ISuspensionModule Suspension = new RaycastSuspensionModule();
        [SerializeReference] public ISteeringModule Steering = new DirectSteeringModule();
        [SerializeReference] public IEngineModule Engine = new ArcadeEngineModule();
        [SerializeReference] public ITransmissionModule Transmission = new SimpleTransmissionModule();
        [SerializeReference] public ITireModule Tires = new BrushTireModule();

        [Header("Driver Assists & Chassis Extensions")]
        [SerializeReference] public List<VehicleModule> Assists = new()
        {
            new AntiRollBarModule(),
            new AbsModule(),
            new TcsModule()
        };

        [Header("Live State Debug")]
        [SerializeField] private VehicleState _state;
        [SerializeField] private VehicleInput _input;

        [Header("Visual Presentation")]
        [SerializeField] private VehicleVisualView _visualView;

        // Hidden evaluated chassis geometry passed to modules
        [HideInInspector]
        public VehicleChassisConfig ChassisConfig = VehicleChassisConfig.Default;

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
            AutoFindWheelMounts();
            UpdateChassisGeometry();

            if (_visualView == null)
                _visualView = GetComponent<VehicleVisualView>();
        }

        private void OnValidate()
        {
            if (_visualView == null)
                _visualView = GetComponent<VehicleVisualView>();

            if (WheelMounts.FrontLeft == null || WheelMounts.FrontRight == null ||
                WheelMounts.RearLeft == null || WheelMounts.RearRight == null)
            {
                AutoFindWheelMounts();
            }

            UpdateChassisGeometry();
        }

        /// <summary>
        /// Attempts to automatically detect suspension anchor transforms in vehicle hierarchy.
        /// </summary>
        public void AutoFindWheelMounts()
        {
            if (WheelMounts.FrontLeft == null) WheelMounts.FrontLeft = transform.Find("Suspension/FL_Suspension");
            if (WheelMounts.FrontRight == null) WheelMounts.FrontRight = transform.Find("Suspension/FR_Suspension");
            if (WheelMounts.RearLeft == null) WheelMounts.RearLeft = transform.Find("Suspension/RL_Suspension");
            if (WheelMounts.RearRight == null) WheelMounts.RearRight = transform.Find("Suspension/RR_Suspension");
        }

        /// <summary>
        /// Recalculates chassis offsets from the dragged WheelMount Transforms.
        /// </summary>
        public void UpdateChassisGeometry()
        {
            ChassisConfig.ChassisMass = ChassisMass;
            ChassisConfig.WheelRadii = new WheelData4<float>(WheelRadius, WheelRadius, WheelRadius, WheelRadius);

            for (int i = 0; i < 4; i++)
            {
                Transform mount = WheelMounts[i];
                if (mount != null)
                {
                    ChassisConfig.WheelOffsets[i] = transform.InverseTransformPoint(mount.position);
                }
            }

            if (Suspension is RaycastSuspensionModule raySusp)
            {
                raySusp.SurfaceLayers = GroundLayers;
            }

            if (_rigidbody != null)
            {
                _rigidbody.mass = ChassisMass;
                _rigidbody.centerOfMass = CenterOfMass;
            }
        }

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _inputReader = GetComponent<VehicleInputReader>();

            AutoFindWheelMounts();
            UpdateChassisGeometry();

            if (_rigidbody != null)
            {
                _rigidbody.mass = ChassisMass;
                _rigidbody.centerOfMass = CenterOfMass;
            }

            if (_visualView == null)
                _visualView = GetComponent<VehicleVisualView>();

            // Physics adapters
            _physicsBody = new UnityRigidbodyPhysicsBody(_rigidbody);
            _surfaceProvider = new UnityPhysicsSurfaceProvider();

            // Initialize all assigned modules
            (Suspension as VehicleModule)?.Initialize(in ChassisConfig);
            (Steering as VehicleModule)?.Initialize(in ChassisConfig);
            (Engine as VehicleModule)?.Initialize(in ChassisConfig);
            (Transmission as VehicleModule)?.Initialize(in ChassisConfig);
            (Tires as VehicleModule)?.Initialize(in ChassisConfig);

            if (Assists != null)
            {
                for (int i = 0; i < Assists.Count; i++)
                {
                    Assists[i]?.Initialize(in ChassisConfig);
                }
            }

            // Initial powertrain state
            _state = default;
            _state.EngineRpm = (Engine is ArcadeEngineModule arcEngine) ? arcEngine.IdleRpm : 800f;
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
        /// Modular and executes only assigned subsystems.
        /// </summary>
        public void SimulateStep(in VehicleInput input, ref VehicleState state, float dt)
        {
            state.Tick++;

            // 1. Steering kinematics (updates front wheel steer angles)
            if (Steering != null)
            {
                Steering.UpdateSteering(in input, ref state, _physicsBody, in ChassisConfig, dt);
            }

            // 2. Suspension rays and spring-damper solver (computes normal loads)
            if (Suspension != null)
            {
                Suspension.SolveSuspension(ref state, _physicsBody, _surfaceProvider, in ChassisConfig, dt);
            }

            // 3. Chassis assists (e.g. Anti-Roll Bar transfers load)
            if (Assists != null)
            {
                for (int i = 0; i < Assists.Count; i++)
                {
                    if (Assists[i] is IChassisAssistModule chassisAssist)
                    {
                        chassisAssist.ApplyChassisAssists(ref state, _physicsBody, in ChassisConfig, dt);
                    }
                }
            }

            // 4. Powertrain (Engine & Transmission)
            _wheelActuation.Reset();

            if (Transmission != null && Engine != null)
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

            // 5. Wheel assists (e.g. ABS & TCS modulate actuation torque limits)
            if (Assists != null)
            {
                for (int i = 0; i < Assists.Count; i++)
                {
                    if (Assists[i] is IWheelAssistModule wheelAssist)
                    {
                        wheelAssist.ApplyAssists(in input, in state, ref _wheelActuation, _physicsBody, in ChassisConfig, dt);
                    }
                }
            }

            // 6. Tires and rotational dynamics (Brush tire model + WheelDynamics solver)
            if (Tires != null)
            {
                Tires.SolveTires(ref state, in _wheelActuation, _physicsBody, in ChassisConfig, dt);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 bodyPos = transform.position;
            Quaternion bodyRot = transform.rotation;
            Vector3 suspDown = -(bodyRot * Vector3.up);

            for (int i = 0; i < 4; i++)
            {
                Vector3 mountPoint = bodyPos + bodyRot * ChassisConfig.WheelOffsets[i];
                float radius = ChassisConfig.WheelRadii[i];
                float restLength = 0.35f;

                if (Suspension is RaycastSuspensionModule raySusp)
                {
                    restLength = (i == 0 || i == 1) ? raySusp.FrontAxle.RestLength : raySusp.RearAxle.RestLength;
                }

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
