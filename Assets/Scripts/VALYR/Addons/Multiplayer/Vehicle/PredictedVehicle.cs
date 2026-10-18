using System;
using System.Reflection;
using UnityEngine;
using PurrNet;
using PurrNet.Prediction;
using VALYR.Core;
using VALYR.Runtime;
using VALYR.Runtime.Input;
using VALYR.Runtime.Visual;

namespace VALYR.Multiplayer
{
    /// <summary>
    /// PurrDiction predicted identity for VALYR vehicles.
    /// Handles deterministic tick-based simulation, input streaming, server verification,
    /// and rollback reconciliation. Replaces ad-hoc RPC impulses with true client-side prediction.
    /// </summary>
    [RequireComponent(typeof(PredictedRigidbody))]
    [RequireComponent(typeof(VehicleController))]
    public class PredictedVehicle : PredictedIdentity<VehiclePredictedInput, VehiclePredictedState>
    {
        private static readonly FieldInfo PhysicsBodyField =
            typeof(VehicleController).GetField("_physicsBody", BindingFlags.NonPublic | BindingFlags.Instance);

        private VehicleController _controller;
        private VehicleInputReader _inputReader;
        private PredictedRigidbody _predictedRigidbody;
        private VehicleVisualView _visualView;
        private NetworkVehicle _networkVehicle;

        public VehicleController Controller => _controller;
        public PredictedRigidbody PredictedRigidbody => _predictedRigidbody;

        private void Awake()
        {
            _controller = GetComponent<VehicleController>();
            _inputReader = GetComponent<VehicleInputReader>();
            _predictedRigidbody = GetComponent<PredictedRigidbody>();
            _visualView = GetComponent<VehicleVisualView>();
            _networkVehicle = GetComponent<NetworkVehicle>();

            // Route all modular subsystem forces (tires, suspension, assists) through PredictedRigidbody
            if (_controller != null && _predictedRigidbody != null)
            {
                var bodyAdapter = new PredictedRigidbodyPhysicsBody(_predictedRigidbody);
                PhysicsBodyField?.SetValue(_controller, bodyAdapter);

                // Disable controller's default MonoBehaviour FixedUpdate so simulation runs exclusively inside PurrDiction Simulate ticks
                _controller.enabled = false;
            }
        }

        protected override void LateAwake()
        {
            base.LateAwake();

            // Default initial state
            if (_controller != null)
            {
                ref var state = ref currentState;
                state.CoreState.EngineRpm = 800f;
                state.CoreState.CurrentGear = 1;
                state.CoreState.ClutchEngagement = 1f;
            }
        }

        /// <summary>
        /// Gathers player intent for this simulation tick on the controlling client.
        /// </summary>
        protected override void GetFinalInput(ref VehiclePredictedInput input)
        {
            // Only read hardware input if this vehicle is owned by the local player
            if (_inputReader != null && isController)
            {
                float dt = predictionManager != null ? predictionManager.tickDelta : Time.fixedDeltaTime;
                var vi = _inputReader.ReadInput(dt);
                input = new VehiclePredictedInput(vi.Throttle, vi.Steer, vi.Brake, vi.Handbrake);
            }
            else
            {
                input = default;
            }
        }

        /// <summary>
        /// Executes a single discrete vehicle physics simulation step within PurrDiction's timeline.
        /// Runs forward on server and predicting clients, and reruns repeatedly during rollback replays.
        /// </summary>
        protected override void Simulate(VehiclePredictedInput input, ref VehiclePredictedState state, float delta)
        {
            if (_controller == null)
                return;

            var vi = new VehicleInput(input.Throttle, input.Steer, input.Brake, input.Handbrake);
            _controller.SimulateStep(in vi, ref state.CoreState, delta);
        }

        private void Update()
        {
            // Present smooth wheel visuals from current corrected predicted state
            if (_visualView != null)
            {
                _visualView.UpdateWheelsView(in currentState.CoreState, Time.deltaTime);
            }
        }
    }
}
