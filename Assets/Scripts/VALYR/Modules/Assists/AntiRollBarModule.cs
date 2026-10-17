using System;
using UnityEngine;
using VALYR.Core;
using VALYR.Core.Interfaces;
using VALYR.Core.Modules;

namespace VALYR.Modules.Assists
{
    /// <summary>
    /// Модуль стабилизаторов поперечной устойчивости (Anti-Roll Bar / Sway Bar).
    /// Устраняет крен кузова в поворотах путем переноса нормальной нагрузки между
    /// левым и правым колесами каждой оси, противодействуя крену шасси.
    /// </summary>
    [Serializable]
    public class AntiRollBarModule : VehicleModule, IChassisAssistModule
    {
        [Header("Жесткость стабилизаторов")]
        [Tooltip("Жесткость переднего стабилизатора поперечной устойчивости (Ньютоны / метр)")]
        public float FrontStiffness = 4500f;

        [Tooltip("Жесткость заднего стабилизатора поперечной устойчивости (Ньютоны / метр)")]
        public float RearStiffness = 2200f;

        public AntiRollBarModule()
        {
            ModuleId = "Assists.AntiRollBar";
        }

        public void ApplyChassisAssists(
            ref VehicleState state,
            IPhysicsBody body,
            in VehicleChassisConfig chassis,
            float dt)
        {
            if (!IsEnabled)
                return;

            Vector3 bodyUp = body.Rotation * Vector3.up;

            // 1. Передний стабилизатор (FL = 0, FR = 1)
            ApplyAxleAntiRoll(
                ref state.Wheels.GetRef(0),
                ref state.Wheels.GetRef(1),
                FrontStiffness,
                body,
                bodyUp);

            // 2. Задний стабилизатор (RL = 2, RR = 3)
            ApplyAxleAntiRoll(
                ref state.Wheels.GetRef(2),
                ref state.Wheels.GetRef(3),
                RearStiffness,
                body,
                bodyUp);
        }

        private static void ApplyAxleAntiRoll(
            ref WheelState leftWheel,
            ref WheelState rightWheel,
            float stiffness,
            IPhysicsBody body,
            Vector3 bodyUp)
        {
            if (stiffness <= 0f)
                return;

            float leftComp = leftWheel.HasContact ? leftWheel.SuspensionCompression : 0f;
            float rightComp = rightWheel.HasContact ? rightWheel.SuspensionCompression : 0f;

            float diff = leftComp - rightComp;
            if (Mathf.Abs(diff) < 0.001f)
                return;

            float deltaForce = diff * stiffness;

            // Корректировка нормальной нагрузки на колеса:
            // Более сжатое колесо получает большую нагрузку, разгруженное — меньшую
            if (leftWheel.HasContact)
            {
                leftWheel.NormalLoad = Mathf.Max(0f, leftWheel.NormalLoad + deltaForce);
                body.AddForceAtPosition(bodyUp * deltaForce, leftWheel.ContactPoint);
            }

            if (rightWheel.HasContact)
            {
                rightWheel.NormalLoad = Mathf.Max(0f, rightWheel.NormalLoad - deltaForce);
                body.AddForceAtPosition(-bodyUp * deltaForce, rightWheel.ContactPoint);
            }
        }
    }
}
