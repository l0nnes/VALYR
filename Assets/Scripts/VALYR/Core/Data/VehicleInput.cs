using System;
using UnityEngine;

namespace VALYR.Core
{
    /// <summary>
    /// Неизменяемая структура ввода игрока/ИИ на один фиксированный тик симуляции.
    /// Blittable-структура, готовая для прямой сериализации и PurrDiction IPredictedData.
    /// </summary>
    [Serializable]
    public struct VehicleInput
    {
        [Range(0f, 1f)]
        public float Throttle;

        [Range(-1f, 1f)]
        public float Steer;

        [Range(0f, 1f)]
        public float Brake;

        [Range(0f, 1f)]
        public float Handbrake;

        public VehicleInput(float throttle, float steer, float brake, float handbrake)
        {
            Throttle = Mathf.Clamp01(throttle);
            Steer = Mathf.Clamp(steer, -1f, 1f);
            Brake = Mathf.Clamp01(brake);
            Handbrake = Mathf.Clamp01(handbrake);
        }

        public void Sanitize()
        {
            Throttle = Mathf.Clamp01(Throttle);
            Steer = Mathf.Clamp(Steer, -1f, 1f);
            Brake = Mathf.Clamp01(Brake);
            Handbrake = Mathf.Clamp01(Handbrake);
        }

        public void Dispose() { }
    }
}
