using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PurrNet;

namespace VALYR.Multiplayer
{
    /// <summary>
    /// Synchronizes vehicle interior driver controls (steering wheel, pedals, handbrake)
    /// and the digital dashboard cluster over the network.
    /// Eliminates proxy conflict where local animators reset the steering wheel to neutral.
    /// </summary>
    [RequireComponent(typeof(NetworkVehicle))]
    public class NetworkVehicleInterior : NetworkBehaviour
    {
        private NetworkVehicle _netVehicle;
        private Rigidbody _rigidbody;
        private Component _interiorAnimator;
        private Component _dashboardDisplay;

        // Interior controls transforms and parameters
        private Transform _steeringWheel;
        private float _maxSteeringRotation = 450f;
        private Transform _acceleratorPedal;
        private float _accelMaxAngle = 15f;
        private Transform _brakePedal;
        private float _brakeMaxAngle = 12f;
        private Transform _handbrakeLever;
        private float _handbrakeMaxAngle = 25f;
        private float _smoothingSpeed = 18f;

        private Quaternion _baseSteeringRot = Quaternion.identity;
        private Quaternion _baseAccelRot = Quaternion.identity;
        private Quaternion _baseBrakeRot = Quaternion.identity;
        private Quaternion _baseHandbrakeRot = Quaternion.identity;

        // Dashboard UI references
        private TMP_Text _gearText;
        private TMP_Text _speedText;
        private TMP_Text _rpmText;
        private Slider _rpmSlider;

        private static readonly Color NormalColor = new(0f, 0.98f, 1f, 1f);      // Cyan
        private static readonly Color WarningColor = new(1f, 0.75f, 0f, 1f);     // Amber
        private static readonly Color RedlineColor = new(1f, 0.22f, 0.22f, 1f);  // Red
        private static readonly Color ReverseColor = new(1f, 0.55f, 0.15f, 1f);  // Orange
        private static readonly Color NeutralColor = new(0.85f, 0.85f, 0.85f, 1f);

        // Reflection cache for VALYR.Runtime.Visual.VehicleInteriorAnimator
        private static readonly Type InteriorAnimatorType =
            Type.GetType("VALYR.Runtime.Visual.VehicleInteriorAnimator, Assembly-CSharp");
        private static readonly FieldInfo SteeringWheelField = InteriorAnimatorType?.GetField("SteeringWheel");
        private static readonly FieldInfo MaxSteeringRotationField = InteriorAnimatorType?.GetField("MaxSteeringRotation");
        private static readonly FieldInfo AcceleratorPedalField = InteriorAnimatorType?.GetField("AcceleratorPedal");
        private static readonly FieldInfo AccelMaxAngleField = InteriorAnimatorType?.GetField("AcceleratorMaxAngle");
        private static readonly FieldInfo BrakePedalField = InteriorAnimatorType?.GetField("BrakePedal");
        private static readonly FieldInfo BrakeMaxAngleField = InteriorAnimatorType?.GetField("BrakeMaxAngle");
        private static readonly FieldInfo HandbrakeLeverField = InteriorAnimatorType?.GetField("HandbrakeLever");
        private static readonly FieldInfo HandbrakeMaxAngleField = InteriorAnimatorType?.GetField("HandbrakeMaxAngle");
        private static readonly FieldInfo SmoothingSpeedField = InteriorAnimatorType?.GetField("SmoothingSpeed");

        // Reflection cache for VALYR.Local.Truck.TruckDashboardDisplay
        private static readonly Type DashboardDisplayType =
            Type.GetType("VALYR.Local.Truck.TruckDashboardDisplay, Assembly-CSharp");
        private static readonly FieldInfo GearTextField =
            DashboardDisplayType?.GetField("_gearText", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo SpeedTextField =
            DashboardDisplayType?.GetField("_speedText", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo RpmTextField =
            DashboardDisplayType?.GetField("_rpmText", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo RpmSliderField =
            DashboardDisplayType?.GetField("_rpmSlider", BindingFlags.NonPublic | BindingFlags.Instance);

        private void Awake()
        {
            _netVehicle = GetComponent<NetworkVehicle>();
            _rigidbody = GetComponent<Rigidbody>();

            if (InteriorAnimatorType != null)
                _interiorAnimator = GetComponent(InteriorAnimatorType);
            if (DashboardDisplayType != null)
                _dashboardDisplay = GetComponent(DashboardDisplayType);

            CacheInteriorElements();
            CacheDashboardElements();
        }

        private void CacheInteriorElements()
        {
            if (_interiorAnimator == null)
            {
                // Fallback auto-discovery if VehicleInteriorAnimator component is missing
                _steeringWheel = transform.Find("Meshes/Interior/Interior Steering Wheel") ??
                                 transform.Find("Interior Steering Wheel");
                if (_steeringWheel != null) _baseSteeringRot = _steeringWheel.localRotation;
                return;
            }

            _steeringWheel = SteeringWheelField?.GetValue(_interiorAnimator) as Transform;
            if (_steeringWheel == null)
            {
                _steeringWheel = transform.Find("Meshes/Interior/Interior Steering Wheel");
            }
            if (_steeringWheel != null) _baseSteeringRot = _steeringWheel.localRotation;

            _acceleratorPedal = AcceleratorPedalField?.GetValue(_interiorAnimator) as Transform;
            if (_acceleratorPedal != null) _baseAccelRot = _acceleratorPedal.localRotation;

            _brakePedal = BrakePedalField?.GetValue(_interiorAnimator) as Transform;
            if (_brakePedal != null) _baseBrakeRot = _brakePedal.localRotation;

            _handbrakeLever = HandbrakeLeverField?.GetValue(_interiorAnimator) as Transform;
            if (_handbrakeLever != null) _baseHandbrakeRot = _handbrakeLever.localRotation;

            if (MaxSteeringRotationField != null)
            {
                object val = MaxSteeringRotationField.GetValue(_interiorAnimator);
                if (val != null) _maxSteeringRotation = (float)val;
            }

            if (AccelMaxAngleField != null)
            {
                object val = AccelMaxAngleField.GetValue(_interiorAnimator);
                if (val != null) _accelMaxAngle = (float)val;
            }

            if (BrakeMaxAngleField != null)
            {
                object val = BrakeMaxAngleField.GetValue(_interiorAnimator);
                if (val != null) _brakeMaxAngle = (float)val;
            }

            if (HandbrakeMaxAngleField != null)
            {
                object val = HandbrakeMaxAngleField.GetValue(_interiorAnimator);
                if (val != null) _handbrakeMaxAngle = (float)val;
            }

            if (SmoothingSpeedField != null)
            {
                object val = SmoothingSpeedField.GetValue(_interiorAnimator);
                if (val != null) _smoothingSpeed = (float)val;
            }
        }

        private void CacheDashboardElements()
        {
            if (_dashboardDisplay == null) return;

            _gearText = GearTextField?.GetValue(_dashboardDisplay) as TMP_Text;
            _speedText = SpeedTextField?.GetValue(_dashboardDisplay) as TMP_Text;
            _rpmText = RpmTextField?.GetValue(_dashboardDisplay) as TMP_Text;
            _rpmSlider = RpmSliderField?.GetValue(_dashboardDisplay) as Slider;
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

        public void UpdateAuthorityState()
        {
            bool isLocal = _netVehicle != null && _netVehicle.IsLocallyControlled;

            if (_interiorAnimator is MonoBehaviour animMb)
            {
                // Disable on proxy so its Update() won't pull the steering wheel back to neutral
                animMb.enabled = isLocal;
            }

            if (_dashboardDisplay is MonoBehaviour dashMb)
            {
                // Disable on proxy so its Update() won't read zero state from disabled VehicleController
                dashMb.enabled = isLocal;
            }
        }

        private void Update()
        {
            bool isLocal = _netVehicle != null && _netVehicle.IsLocallyControlled;
            if (isLocal) return;

            // Remote proxy: animate steering wheel, pedals, and dashboard from synced state
            float dt = Time.deltaTime;
            VehicleNetworkState net = _netVehicle.SyncedState;

            // 1. Interior Steering Wheel Rotation (turns around local Z matching front wheels)
            if (_steeringWheel != null)
            {
                // Max front wheel steer angle is ~35° (0.61 rad)
                float steerNormalized = Mathf.Clamp(net.SteerAngle / 0.61f, -1f, 1f);
                Quaternion targetSteering = _baseSteeringRot * Quaternion.Euler(0f, 0f, -steerNormalized * _maxSteeringRotation);
                _steeringWheel.localRotation = Quaternion.Slerp(_steeringWheel.localRotation, targetSteering, 1f - Mathf.Exp(-_smoothingSpeed * dt));
            }

            // 2. Accelerator Pedal
            if (_acceleratorPedal != null)
            {
                float throttle = (net.EngineRpm > 1000f) ? Mathf.Clamp01((net.EngineRpm - 800f) / 3500f) : 0f;
                Quaternion targetAccel = _baseAccelRot * Quaternion.Euler(throttle * _accelMaxAngle, 0f, 0f);
                _acceleratorPedal.localRotation = Quaternion.Slerp(_acceleratorPedal.localRotation, targetAccel, 1f - Mathf.Exp(-_smoothingSpeed * dt));
            }

            // 3. Brake Pedal
            if (_brakePedal != null)
            {
                float brake = net.IsBraking ? 1f : 0f;
                Quaternion targetBrake = _baseBrakeRot * Quaternion.Euler(brake * _brakeMaxAngle, 0f, 0f);
                _brakePedal.localRotation = Quaternion.Slerp(_brakePedal.localRotation, targetBrake, 1f - Mathf.Exp(-_smoothingSpeed * dt));
            }

            // 4. Handbrake Lever
            if (_handbrakeLever != null)
            {
                float handbrake = net.IsHandbraking ? 1f : 0f;
                Quaternion targetHandbrake = _baseHandbrakeRot * Quaternion.Euler(-handbrake * _handbrakeMaxAngle, 0f, 0f);
                _handbrakeLever.localRotation = Quaternion.Slerp(_handbrakeLever.localRotation, targetHandbrake, 1f - Mathf.Exp(-_smoothingSpeed * dt));
            }

            // 5. Digital Dashboard Cluster
            UpdateProxyDashboard(in net, dt);
        }

        private void UpdateProxyDashboard(in VehicleNetworkState net, float dt)
        {
            // Speed (km/h)
            if (_speedText != null)
            {
                float speedKmh = Mathf.Abs(net.ForwardSpeed) * 3.6f;
                if (_rigidbody != null && _rigidbody.linearVelocity.sqrMagnitude > 0.01f)
                {
                    speedKmh = Mathf.Abs(Vector3.Dot(_rigidbody.linearVelocity, transform.forward)) * 3.6f;
                }
                _speedText.text = Mathf.RoundToInt(speedKmh).ToString();
            }

            // Transmission Gear Display
            if (_gearText != null)
            {
                if (net.CurrentGear == -1)
                {
                    _gearText.text = "R";
                    _gearText.color = ReverseColor;
                }
                else if (net.CurrentGear == 0)
                {
                    _gearText.text = "N";
                    _gearText.color = NeutralColor;
                }
                else
                {
                    _gearText.text = "D";
                    _gearText.color = NormalColor;
                }
            }

            // Engine RPM Text & Color
            if (_rpmText != null)
            {
                _rpmText.text = Mathf.RoundToInt(net.EngineRpm).ToString();
                if (net.EngineRpm >= 6800f)
                    _rpmText.color = RedlineColor;
                else if (net.EngineRpm >= 5500f)
                    _rpmText.color = WarningColor;
                else
                    _rpmText.color = NormalColor;
            }

            // RPM Gauge Bar Slider
            if (_rpmSlider != null)
            {
                float targetRatio = Mathf.Clamp01((net.EngineRpm - 800f) / 6000f);
                _rpmSlider.value = Mathf.Lerp(_rpmSlider.value, targetRatio, dt * 20f);
            }
        }
    }
}
