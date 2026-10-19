using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VALYR.Core;
using VALYR.Modules.Powertrain;
using VALYR.Runtime;

namespace VALYR.Local.Truck
{
    /// <summary>
    /// Updates the interior digital dashboard display cluster in real-time.
    /// Displays current engaged gear (P, R, N, D, Auto-Hold), speed in km/h,
    /// engine/motor RPM, and tachometer bar gauge.
    /// </summary>
    [DisallowMultipleComponent]
    public class TruckDashboardDisplay : MonoBehaviour
    {
        [Header("Vehicle Reference")]
        [SerializeField] private VehicleController _controller;

        [Header("Dashboard UI Elements")]
        [Tooltip("TextMeshPro displaying current engaged transmission gear.")]
        [SerializeField] private TMP_Text _gearText;

        [Tooltip("TextMeshPro displaying current vehicle forward speed.")]
        [SerializeField] private TMP_Text _speedText;

        [Tooltip("TextMeshPro displaying engine / motor RPM.")]
        [SerializeField] private TMP_Text _rpmText;

        [Tooltip("Slider representing tachometer / RPM bar gauge.")]
        [SerializeField] private Slider _rpmSlider;

        [Tooltip("Optional label displaying 'KM/H'.")]
        [SerializeField] private TMP_Text _speedUnitLabel;

        [Tooltip("Optional label displaying 'RPM'.")]
        [SerializeField] private TMP_Text _rpmUnitLabel;

        [Header("RPM Gauge Tuning")]
        [Tooltip("Idle engine RPM.")]
        [SerializeField] private float _idleRpm = 800f;

        [Tooltip("Warning yellow RPM threshold.")]
        [SerializeField] private float _warningRpm = 5500f;

        [Tooltip("Maximum / redline RPM limit.")]
        [SerializeField] private float _redlineRpm = 6800f;

        [Header("Display Color Scheme")]
        [SerializeField] private Color _normalColor = new Color(0f, 0.98f, 1f, 1f);      // Cyan
        [SerializeField] private Color _warningColor = new Color(1f, 0.75f, 0.0f, 1f);   // Amber
        [SerializeField] private Color _redlineColor = new Color(1f, 0.22f, 0.22f, 1f);  // Red
        [SerializeField] private Color _autoHoldColor = new Color(0.1f, 1f, 0.45f, 1f); // Lime Green
        [SerializeField] private Color _reverseColor = new Color(1f, 0.55f, 0.15f, 1f);  // Orange
        [SerializeField] private Color _neutralColor = new Color(0.85f, 0.85f, 0.85f, 1f); // White/Gray

        private void Awake()
        {
            if (_controller == null)
            {
                _controller = GetComponent<VehicleController>() ?? GetComponentInParent<VehicleController>();
            }

            AutoBindReferences();
        }

        /// <summary>
        /// Automatically discovers and links Canvas children if not assigned in Inspector.
        /// </summary>
        public void AutoBindReferences()
        {
            var canvas = transform.Find("Canvas");
            if (canvas == null)
                return;

            if (_gearText == null)
            {
                var t = canvas.Find("Current Gear (TMP) - Dashboard");
                if (t != null) _gearText = t.GetComponent<TMP_Text>();
            }

            if (_speedText == null)
            {
                var t = canvas.Find("Current Speed (TMP) - Dashboard");
                if (t != null) _speedText = t.GetComponent<TMP_Text>();
            }

            if (_rpmText == null)
            {
                var t = canvas.Find("Current RPM (TMP) - Dashboard");
                if (t != null) _rpmText = t.GetComponent<TMP_Text>();
            }

            if (_rpmSlider == null)
            {
                var t = canvas.Find("Acceleration Slider");
                if (t != null) _rpmSlider = t.GetComponent<Slider>();
            }

            if (_speedUnitLabel == null)
            {
                var t = canvas.Find("Speed Unit (TMP)");
                if (t != null) _speedUnitLabel = t.GetComponent<TMP_Text>();
            }

            if (_rpmUnitLabel == null)
            {
                var t = canvas.Find("RPM Unit (TMP)");
                if (t != null) _rpmUnitLabel = t.GetComponent<TMP_Text>();
            }
        }

        private void Update()
        {
            if (_controller == null)
                return;

            UpdateSpeed();
            UpdateGear();
            UpdateRpm();
        }

        private void UpdateSpeed()
        {
            if (_speedText == null) return;

            float speedKmh = 0f;
            if (_controller.Rigidbody != null)
            {
                Vector3 vel = _controller.Rigidbody.linearVelocity;
                float forwardSpeed = Vector3.Dot(vel, _controller.transform.forward);
                speedKmh = Mathf.Abs(forwardSpeed) * 3.6f;
            }

            _speedText.text = Mathf.RoundToInt(speedKmh).ToString();
        }

        private void UpdateGear()
        {
            if (_gearText == null) return;

            int gear = _controller.State.CurrentGear;
            bool isAutoHold = (_controller.Transmission is SimpleTransmissionModule tm) && tm.IsAutoHoldActive;

            if (isAutoHold)
            {
                _gearText.text = "HOLD";
                _gearText.color = _autoHoldColor;
            }
            else if (gear == -1)
            {
                _gearText.text = "R";
                _gearText.color = _reverseColor;
            }
            else if (gear == 0)
            {
                _gearText.text = "N";
                _gearText.color = _neutralColor;
            }
            else
            {
                _gearText.text = "D";
                _gearText.color = _normalColor;
            }
        }

        private void UpdateRpm()
        {
            float rpm = _controller.State.EngineRpm;

            // Update numerical text
            if (_rpmText != null)
            {
                _rpmText.text = Mathf.RoundToInt(rpm).ToString();

                if (rpm >= _redlineRpm)
                    _rpmText.color = _redlineColor;
                else if (rpm >= _warningRpm)
                    _rpmText.color = _warningColor;
                else
                    _rpmText.color = _normalColor;
            }

            // Update tachometer bar slider
            if (_rpmSlider != null)
            {
                float targetRatio = Mathf.Clamp01((rpm - _idleRpm) / Mathf.Max(100f, _redlineRpm - _idleRpm));
                _rpmSlider.value = Mathf.Lerp(_rpmSlider.value, targetRatio, Time.deltaTime * 20f);
            }
        }
    }
}
