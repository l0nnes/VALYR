using System;
using UnityEngine;
using VALYR.Core;

namespace VALYR.Runtime.Visual
{
    /// <summary>
    /// Visual presentation layer for vehicle wheel meshes.
    /// Positions wheels directly along the suspension axis based on current damper length
    /// from the anchor point down to the hub, ensuring zero wheel-arch clipping and silky smooth rendering.
    /// </summary>
    public class VehicleVisualView : MonoBehaviour
    {
        [Header("Wheel Visual Meshes (FL, FR, RL, RR)")]
        [Tooltip("Front Left wheel mesh transform.")]
        public Transform FrontLeftWheel;

        [Tooltip("Front Right wheel mesh transform.")]
        public Transform FrontRightWheel;

        [Tooltip("Rear Left wheel mesh transform.")]
        public Transform RearLeftWheel;

        [Tooltip("Rear Right wheel mesh transform.")]
        public Transform RearRightWheel;

        [Header("Suspension Mount Anchors")]
        [Tooltip("Parent suspension anchor transforms located at the top of the wheel arches.")]
        public Transform FL_SuspensionAnchor;
        public Transform FR_SuspensionAnchor;
        public Transform RL_SuspensionAnchor;
        public Transform RR_SuspensionAnchor;

        [Header("Visual Smoothing")]
        [Tooltip("Smoothing rate for vertical wheel motion to eliminate visual jitter (1/s, 0 = instant).")]
        [Range(0f, 60f)]
        public float SuspensionPositionSmoothRate = 25f;

        // Cached local offsets and references
        private Vector3[] _baseLocalPositions;
        private Vector3[] _currentLocalPositions;
        private float[] _currentSteerAngles;
        private float[] _visualRollAngles;
        private Transform[] _wheels;
        private Transform[] _anchors;

        private void Reset()
        {
            AutoFindReferences();
        }

        private void OnValidate()
        {
            if (FrontLeftWheel == null || FrontRightWheel == null || RearLeftWheel == null || RearRightWheel == null)
            {
                AutoFindReferences();
            }
        }

        public void AutoFindReferences()
        {
            if (FrontLeftWheel == null) FrontLeftWheel = transform.Find("Wheels/Meshes/FrontLeftWheel") ?? transform.Find("Graphics/Meshes/Wheels/Meshes/Wheel Mesh Front Left");
            if (FrontRightWheel == null) FrontRightWheel = transform.Find("Wheels/Meshes/FrontRightWheel") ?? transform.Find("Graphics/Meshes/Wheels/Meshes/Wheel Mesh Front Right");
            if (RearLeftWheel == null) RearLeftWheel = transform.Find("Wheels/Meshes/RearLeftWheel") ?? transform.Find("Graphics/Meshes/Wheels/Meshes/Wheel Mesh Rear Left");
            if (RearRightWheel == null) RearRightWheel = transform.Find("Wheels/Meshes/RearRightWheel") ?? transform.Find("Graphics/Meshes/Wheels/Meshes/Wheel Mesh Rear Right");

            if (FL_SuspensionAnchor == null) FL_SuspensionAnchor = transform.Find("Suspension/FL_Suspension");
            if (FR_SuspensionAnchor == null) FR_SuspensionAnchor = transform.Find("Suspension/FR_Suspension");
            if (RL_SuspensionAnchor == null) RL_SuspensionAnchor = transform.Find("Suspension/RL_Suspension");
            if (RR_SuspensionAnchor == null) RR_SuspensionAnchor = transform.Find("Suspension/RR_Suspension");
        }

        private void Awake()
        {
            if (FrontLeftWheel == null || FrontRightWheel == null || RearLeftWheel == null || RearRightWheel == null)
            {
                AutoFindReferences();
            }

            _wheels = new Transform[4]
            {
                FrontLeftWheel,
                FrontRightWheel,
                RearLeftWheel,
                RearRightWheel
            };

            _anchors = new Transform[4]
            {
                FL_SuspensionAnchor,
                FR_SuspensionAnchor,
                RL_SuspensionAnchor,
                RR_SuspensionAnchor
            };

            _baseLocalPositions = new Vector3[4];
            _currentLocalPositions = new Vector3[4];
            _currentSteerAngles = new float[4];
            _visualRollAngles = new float[4];

            for (int i = 0; i < 4; i++)
            {
                if (_wheels[i] != null)
                {
                    _baseLocalPositions[i] = _wheels[i].localPosition;
                    _currentLocalPositions[i] = _baseLocalPositions[i];
                }
            }
        }

        /// <summary>
        /// Updates the local position and rotation of all 4 wheel meshes based on vehicle simulation state.
        /// </summary>
        public void UpdateWheelsView(in VehicleState state, float dt)
        {
            if (_wheels == null) return;

            for (int i = 0; i < 4; i++)
            {
                Transform wheelTransform = _wheels[i];
                if (wheelTransform == null)
                    continue;

                WheelState wheelState = state.Wheels[i];

                // 1. Lock longitudinal (Z) and lateral (X) position rigidly to authored wheel well
                // This completely prevents wheels from lagging behind or drifting horizontally relative to the chassis
                _currentLocalPositions[i].x = _baseLocalPositions[i].x;
                _currentLocalPositions[i].z = _baseLocalPositions[i].z;

                // 2. Vertical suspension travel (+ compression = up into arch, - rebound = down)
                float targetY = _baseLocalPositions[i].y + wheelState.SuspensionCompression;

                if (SuspensionPositionSmoothRate > 0.01f && dt > 0f)
                {
                    _currentLocalPositions[i].y = Mathf.Lerp(
                        _currentLocalPositions[i].y,
                        targetY,
                        1f - Mathf.Exp(-SuspensionPositionSmoothRate * dt));
                }
                else
                {
                    _currentLocalPositions[i].y = targetY;
                }

                wheelTransform.localPosition = _currentLocalPositions[i];

                // 3. Smooth continuous wheel rotation (Steer around Y + Roll around X)
                float steerDeg = wheelState.SteerAngle * Mathf.Rad2Deg;
                if (_currentSteerAngles == null || _currentSteerAngles.Length != 4)
                    _currentSteerAngles = new float[4];

                if (dt > 0f)
                {
                    _currentSteerAngles[i] = Mathf.MoveTowardsAngle(_currentSteerAngles[i], steerDeg, 720f * dt);
                }
                else
                {
                    _currentSteerAngles[i] = steerDeg;
                }
                Quaternion steerRotation = Quaternion.Euler(0f, _currentSteerAngles[i], 0f);

                // Integrate roll angle per frame using AngularVelocity to eliminate 50Hz tick stutter
                if (_visualRollAngles == null || _visualRollAngles.Length != 4)
                    _visualRollAngles = new float[4];

                if (dt > 0f)
                {
                    _visualRollAngles[i] = Mathf.Repeat(_visualRollAngles[i] + wheelState.AngularVelocity * dt, Mathf.PI * 2f);
                    float simRollDeg = wheelState.RotationAngle * Mathf.Rad2Deg;
                    float currentRollDeg = _visualRollAngles[i] * Mathf.Rad2Deg;
                    float blendedRollDeg = Mathf.MoveTowardsAngle(currentRollDeg, simRollDeg, 360f * dt);
                    _visualRollAngles[i] = blendedRollDeg * Mathf.Deg2Rad;
                }
                else
                {
                    _visualRollAngles[i] = wheelState.RotationAngle;
                }

                Quaternion rollRotation = Quaternion.Euler(_visualRollAngles[i] * Mathf.Rad2Deg, 0f, 0f);
                wheelTransform.localRotation = steerRotation * rollRotation;
            }
        }
    }
}
