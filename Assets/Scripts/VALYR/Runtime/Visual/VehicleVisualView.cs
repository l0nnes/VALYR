using System;
using UnityEngine;
using VALYR.Core;

namespace VALYR.Runtime.Visual
{
    /// <summary>
    /// Visual presentation layer for vehicle wheel meshes and suspension travel.
    /// Executed in Update/LateUpdate for silky smooth rendering without physics jitter:
    /// - Displaces wheel meshes vertically by suspension compression
    /// - Yaws wheels around vertical axis by SteerAngle
    /// - Rolls wheels around hub axis by RotationAngle
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
        [Tooltip("Optional parent suspension anchor transforms.")]
        public Transform FL_SuspensionAnchor;
        public Transform FR_SuspensionAnchor;
        public Transform RL_SuspensionAnchor;
        public Transform RR_SuspensionAnchor;

        [Header("Visual Tuning")]
        [Tooltip("Suspension rest length in meters (distance from mount point to wheel hub axis).")]
        [Min(0.05f)]
        public float SuspensionRestLength = 0.345f;

        [Tooltip("Smoothing rate for vertical wheel motion to eliminate visual jitter (1/s, 0 = instant).")]
        [Range(0f, 60f)]
        public float SuspensionPositionSmoothRate = 25f;

        // Cached local offsets
        private Vector3[] _baseLocalPositions;
        private Vector3[] _currentLocalPositions;
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
            if (FrontLeftWheel == null) FrontLeftWheel = transform.Find("Wheels/Meshes/FrontLeftWheel");
            if (FrontRightWheel == null) FrontRightWheel = transform.Find("Wheels/Meshes/FrontRightWheel");
            if (RearLeftWheel == null) RearLeftWheel = transform.Find("Wheels/Meshes/RearLeftWheel");
            if (RearRightWheel == null) RearRightWheel = transform.Find("Wheels/Meshes/RearRightWheel");

            if (FL_SuspensionAnchor == null) FL_SuspensionAnchor = transform.Find("Suspension/FL_Suspension");
            if (FR_SuspensionAnchor == null) FR_SuspensionAnchor = transform.Find("Suspension/FR_Suspension");
            if (RL_SuspensionAnchor == null) RL_SuspensionAnchor = transform.Find("Suspension/RL_Suspension");
            if (RR_SuspensionAnchor == null) RR_SuspensionAnchor = transform.Find("Suspension/RR_Suspension");
            SuspensionRestLength = 0.345f;
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
            for (int i = 0; i < 4; i++)
            {
                Transform wheelTransform = _wheels[i];
                if (wheelTransform == null)
                    continue;

                WheelState wheelState = state.Wheels[i];

                // 1. Vertical suspension position
                float compression = wheelState.SuspensionCompression;
                Vector3 basePos = _baseLocalPositions[i];

                if (_anchors[i] != null)
                {
                    Vector3 anchorPos = wheelTransform.parent.InverseTransformPoint(_anchors[i].position);
                    float targetY = anchorPos.y - (SuspensionRestLength - compression);
                    basePos = new Vector3(anchorPos.x, targetY, anchorPos.z);
                }
                else
                {
                    basePos.y = _baseLocalPositions[i].y + compression;
                }

                if (SuspensionPositionSmoothRate > 0.01f && dt > 0f)
                {
                    _currentLocalPositions[i] = Vector3.Lerp(
                        _currentLocalPositions[i],
                        basePos,
                        1f - Mathf.Exp(-SuspensionPositionSmoothRate * dt));
                }
                else
                {
                    _currentLocalPositions[i] = basePos;
                }

                wheelTransform.localPosition = _currentLocalPositions[i];

                // 2. Wheel rotation (Steer around Y + Roll around X)
                float steerDeg = wheelState.SteerAngle * Mathf.Rad2Deg;
                float rollDeg = wheelState.RotationAngle * Mathf.Rad2Deg;

                Quaternion steerRotation = Quaternion.Euler(0f, steerDeg, 0f);
                Quaternion rollRotation = Quaternion.Euler(rollDeg, 0f, 0f);

                wheelTransform.localRotation = steerRotation * rollRotation;
            }
        }
    }
}
