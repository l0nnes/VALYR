using System;
using System.Reflection;
using UnityEngine;
using PurrNet;
using PurrNet.Prediction;
using VALYR.Core;
using VALYR.Runtime;
using VALYR.Runtime.Input;

namespace VALYR.Multiplayer
{
    /// <summary>
    /// Base multiplayer adapter for VALYR vehicles over PurrNet.
    /// Manages client-side ownership authority, synchronizes simulation state with PredictedVehicle,
    /// and isolates local driver input from remote observer vehicles.
    /// </summary>
    [RequireComponent(typeof(VehicleController))]
    public class NetworkVehicle : NetworkBehaviour
    {
        private VehicleController _controller;
        private VehicleInputReader _inputReader;
        private PredictedVehicle _predictedVehicle;
        private Rigidbody _rigidbody;

        /// <summary>
        /// Explicit target player ID assigned during server spawn.
        /// Prevents host from adopting remote client vehicles as local during spawn.
        /// </summary>
        public PlayerID? AssignedPlayerId { get; set; }

        /// <summary>
        /// Fires whenever ownership authority changes (true = local player controls vehicle, false = remote observer).
        /// Cosmetic, camera, and UI scripts subscribe to this event to isolate local inputs.
        /// </summary>
        public event Action<bool> OnAuthorityChanged;

        public bool IsLocallyControlled
        {
            get
            {
                if (NetworkManager.main == null || NetworkManager.main.isOffline)
                {
                    return true;
                }

                if (!isSpawned)
                {
                    return false;
                }

                if (AssignedPlayerId.HasValue)
                {
                    return AssignedPlayerId.Value == NetworkManager.main.localPlayer;
                }

                return isOwner;
            }
        }

        /// <summary>
        /// Live vehicle state provider used by networked addons and telemetry displays.
        /// Reads directly from PredictedVehicle's current predicted/reconciled simulation state.
        /// </summary>
        public VehicleNetworkState SyncedState
        {
            get
            {
                VehicleState s = _predictedVehicle != null ? _predictedVehicle.currentState.CoreState : (_controller != null ? _controller.State : default);
                VehicleInput vi = _controller != null ? _controller.Input : default;
                if (_predictedVehicle != null && vi.Throttle == 0f && vi.Brake == 0f && vi.Handbrake == 0f)
                {
                    vi = new VehicleInput(_predictedVehicle.currentState.Throttle, _predictedVehicle.currentState.Steer, _predictedVehicle.currentState.Brake, _predictedVehicle.currentState.Handbrake);
                }
                Vector3 vel = _rigidbody != null ? _rigidbody.linearVelocity : Vector3.zero;
                float speedKmh = Vector3.Dot(vel, transform.forward) * 3.6f;

                return new VehicleNetworkState
                {
                    EngineRpm = s.EngineRpm,
                    CurrentGear = s.CurrentGear,
                    SteerAngle = s.Wheels[0].SteerAngle,
                    ForwardSpeed = speedKmh,
                    SuspLength0 = s.Wheels[0].SuspensionLength,
                    SuspLength1 = s.Wheels[1].SuspensionLength,
                    SuspLength2 = s.Wheels[2].SuspensionLength,
                    SuspLength3 = s.Wheels[3].SuspensionLength,
                    WheelOmega0 = s.Wheels[0].AngularVelocity,
                    WheelOmega1 = s.Wheels[1].AngularVelocity,
                    WheelOmega2 = s.Wheels[2].AngularVelocity,
                    WheelOmega3 = s.Wheels[3].AngularVelocity,
                    IsBraking = vi.Brake > 0.05f || (s.CurrentGear == -1 && vi.Throttle > 0.05f),
                    IsHandbraking = vi.Handbrake > 0.5f,
                    IsReversing = s.CurrentGear < 0
                };
            }
        }

        private void Awake()
        {
            EnsureComponentsCached();
        }

        private void EnsureComponentsCached()
        {
            if (_controller == null) _controller = GetComponent<VehicleController>();
            if (_inputReader == null) _inputReader = GetComponent<VehicleInputReader>();
            if (_predictedVehicle == null) _predictedVehicle = GetComponent<PredictedVehicle>();
            if (_rigidbody == null) _rigidbody = GetComponent<Rigidbody>();
        }

        private void Start()
        {
            UpdateAuthorityState();
        }

        protected override void OnSpawned()
        {
            base.OnSpawned();
            UpdateAuthorityState();
        }

        protected override void OnOwnerChanged(PlayerID? oldOwner, PlayerID? newOwner, bool asServer)
        {
            base.OnOwnerChanged(oldOwner, newOwner, asServer);
            UpdateAuthorityState();
        }

        /// <summary>
        /// Configures vehicle systems depending on whether this machine is the driver (Owner) or observer (Remote).
        /// </summary>
        public void UpdateAuthorityState()
        {
            EnsureComponentsCached();
            bool localControl = IsLocallyControlled;

            // Sync ownership with PredictedVehicle
            if (_predictedVehicle != null)
            {
                PlayerID? targetOwner = AssignedPlayerId.HasValue ? AssignedPlayerId : (isSpawned ? owner : null);
                if (targetOwner.HasValue && _predictedVehicle.owner != targetOwner)
                {
                    _predictedVehicle.owner = targetOwner;
                }
            }

            // Input reader isolation
            if (_inputReader != null)
            {
                if (!localControl)
                {
                    SetPrivateField(_inputReader, "_actionMap", null);
                }
                _inputReader.enabled = localControl;
                if (localControl)
                {
                    typeof(VehicleInputReader).GetMethod("InitializeInputActions",
                        BindingFlags.NonPublic | BindingFlags.Instance)?.Invoke(_inputReader, null);
                    var map = _inputReader.GetType().GetField("_actionMap", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(_inputReader) as UnityEngine.InputSystem.InputActionMap;
                    if (map != null && !map.enabled)
                    {
                        map.Enable();
                    }
                }
            }

            // Notify all listening external/demo components (cameras, lights, doors, HUD)
            OnAuthorityChanged?.Invoke(localControl);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target?.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            field?.SetValue(target, value);
        }
    }
}
