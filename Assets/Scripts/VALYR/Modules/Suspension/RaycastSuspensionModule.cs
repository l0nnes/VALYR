using System;
using UnityEngine;
using VALYR.Core;
using VALYR.Core.Interfaces;
using VALYR.Core.Modules;

namespace VALYR.Modules.Suspension
{
    /// <summary>
    /// Физические параметры подвески для одной оси (передняя / задняя).
    /// </summary>
    [Serializable]
    public struct SuspensionAxleSettings
    {
        [Tooltip("Длина подвески в свободном ненагруженном состоянии (метры)")]
        public float RestLength;

        [Tooltip("Максимальный допустимый ход сжатия подвески (метры)")]
        public float MaxTravel;

        [Tooltip("Жесткость пружины (Ньютоны / метр)")]
        public float SpringStiffness;

        [Tooltip("Коэффициент демпфирования при ходе сжатия (Bump Damping, Н*с/м)")]
        public float DamperCompressionRate;

        [Tooltip("Коэффициент демпфирования при ходе отбоя (Rebound Damping, Н*с/м)")]
        public float DamperReboundRate;

        [Tooltip("Относительный порог срабатывания отбойника (0.0..1.0 от MaxTravel, обычно 0.75..0.85)")]
        [Range(0.5f, 0.95f)]
        public float BumpStopThresholdRatio;

        [Tooltip("Квадратичная жесткость полиуретанового отбойника (Н / м^2)")]
        public float BumpStopStiffness;

        public static SuspensionAxleSettings DefaultFront => new()
        {
            RestLength = 0.35f,
            MaxTravel = 0.22f,
            SpringStiffness = 24000f,
            DamperCompressionRate = 2200f,
            DamperReboundRate = 3400f,
            BumpStopThresholdRatio = 0.80f,
            BumpStopStiffness = 140000f
        };

        public static SuspensionAxleSettings DefaultRear => new()
        {
            RestLength = 0.35f,
            MaxTravel = 0.22f,
            SpringStiffness = 19000f,
            DamperCompressionRate = 1800f,
            DamperReboundRate = 2800f,
            BumpStopThresholdRatio = 0.80f,
            BumpStopStiffness = 140000f
        };
    }

    /// <summary>
    /// Модуль независимой пружинно-амортизаторной подвески на основе лучевого сканирования (Raycast Suspension).
    /// Поддерживает раздельные параметры для передней и задней осей, ассиметричное демпфирование
    /// сжатия/отбоя и нелинейные отбойники (Bump Stops).
    /// </summary>
    [Serializable]
    public class RaycastSuspensionModule : VehicleModule, ISuspensionModule
    {
        [Header("Настройки подвески по осям")]
        public SuspensionAxleSettings FrontAxle = SuspensionAxleSettings.DefaultFront;
        public SuspensionAxleSettings RearAxle = SuspensionAxleSettings.DefaultRear;

        [Header("Коллизии поверхности")]
        [Tooltip("Маска слоев для поиска поверхности дороги")]
        public LayerMask SurfaceLayers = ~0;

        public RaycastSuspensionModule()
        {
            ModuleId = "Suspension.Raycast";
        }

        public void SolveSuspension(
            ref VehicleState state,
            IPhysicsBody body,
            ISurfaceProvider surfaceProvider,
            in VehicleChassisConfig chassis,
            float dt)
        {
            if (!IsEnabled)
                return;

            Vector3 bodyPos = body.Position;
            Quaternion bodyRot = body.Rotation;
            Vector3 suspDown = -(bodyRot * Vector3.up);

            for (int i = 0; i < 4; i++)
            {
                bool isFront = (i == 0 || i == 1);
                ref readonly SuspensionAxleSettings settings = ref (isFront ? ref FrontAxle : ref RearAxle);

                float radius = chassis.WheelRadii[i];
                Vector3 mountPoint = bodyPos + bodyRot * chassis.WheelOffsets[i];
                float maxRayDistance = settings.RestLength + radius;

                bool hasHit = surfaceProvider.CastRay(
                    mountPoint,
                    suspDown,
                    maxRayDistance,
                    SurfaceLayers,
                    out SurfaceHit hit);

                ref WheelState wheel = ref state.Wheels.GetRef(i);

                if (hasHit)
                {
                    float currentLength = hit.Distance - radius;
                    float compression = Mathf.Clamp(settings.RestLength - currentLength, 0f, settings.MaxTravel);

                    // Скорость сжатия подвески (+ сжатие при движении кузова вниз к земле, - отбой)
                    Vector3 mountVelocity = body.GetPointVelocity(mountPoint);
                    float suspVelocity = Vector3.Dot(mountVelocity, suspDown);

                    // 1. Сила упругости пружины Гука
                    float fSpring = settings.SpringStiffness * compression;

                    // 2. Сила амортизатора с раздельным сжатием и отбоем
                    float damperCoeff = suspVelocity >= 0f
                        ? settings.DamperCompressionRate
                        : settings.DamperReboundRate;
                    float fDamper = damperCoeff * suspVelocity;

                    // 3. Нелинейный прогрессивный отбойник (Bump Stop)
                    float bumpThreshold = settings.MaxTravel * settings.BumpStopThresholdRatio;
                    float fBump = 0f;
                    if (compression > bumpThreshold)
                    {
                        float excess = compression - bumpThreshold;
                        fBump = settings.BumpStopStiffness * excess * excess;
                    }

                    // Результирующая сила подвески (подвеска не может притягивать машину к земле)
                    float totalForce = Mathf.Max(0f, fSpring + fDamper + fBump);

                    // Приложение силы вдоль оси подвески вверх к кузову в точке крепления подвески
                    Vector3 forceVector = -suspDown * totalForce;
                    body.AddForceAtPosition(forceVector, mountPoint);

                    // Сохранение состояния колеса
                    wheel.HasContact = true;
                    wheel.ContactPoint = hit.Point;
                    wheel.ContactNormal = hit.Normal;
                    wheel.SuspensionCompression = compression;
                    wheel.SuspensionVelocity = suspVelocity;
                    wheel.NormalLoad = totalForce;
                }
                else
                {
                    // Колесо в воздухе
                    wheel.HasContact = false;
                    wheel.ContactPoint = mountPoint + suspDown * maxRayDistance;
                    wheel.ContactNormal = -suspDown;
                    wheel.SuspensionCompression = 0f;
                    wheel.SuspensionVelocity = 0f;
                    wheel.NormalLoad = 0f;
                }
            }
        }
    }
}
