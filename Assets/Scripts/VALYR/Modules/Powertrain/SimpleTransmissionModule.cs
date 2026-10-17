using System;
using UnityEngine;
using VALYR.Core;
using VALYR.Core.Modules;

namespace VALYR.Modules.Powertrain
{
    public enum DrivetrainType
    {
        AllWheelDrive = 0,
        RearWheelDrive = 1,
        FrontWheelDrive = 2
    }

    /// <summary>
    /// Модуль коробки передач и трансмиссии (Simple Transmission Module).
    /// Поддерживает режимы привода AWD/RWD/FWD, автоматическое переключение передач
    /// с защитой от частых переключений (anti-hunting), гидротрансформатор / сцепление,
    /// а также тормозную систему с распределением усилий (Brake Bias) и ручной тормоз.
    /// </summary>
    [Serializable]
    public class SimpleTransmissionModule : VehicleModule, ITransmissionModule
    {
        [Header("Тип привода")]
        public DrivetrainType Drivetrain = DrivetrainType.FrontWheelDrive;

        [Tooltip("Доля момента на переднюю ось для AWD (0.4 = 40% перед, 60% зад)")]
        [Range(0f, 1f)]
        public float FrontTorqueSplitAwd = 0.50f;

        [Header("Передаточные числа (Solaris 6-АКПП A6GF1)")]
        [Tooltip("Передаточное число заднего хода (отрицательное число)")]
        public float ReverseGearRatio = -3.487f;

        [Tooltip("Передаточные числа передач вперед (1-я, 2-я, ... 6-я)")]
        public float[] ForwardGearRatios = { 3.612f, 2.085f, 1.370f, 1.039f, 0.778f, 0.633f };

        [Tooltip("Главная передача редуктора (Final Drive Ratio)")]
        public float FinalDriveRatio = 3.943f;

        [Tooltip("Механический КПД трансмиссии (0.85 .. 0.96)")]
        [Range(0.7f, 1f)]
        public float TransmissionEfficiency = 0.90f;

        [Header("Автоматическое переключение")]
        public bool AutomaticShift = true;

        [Tooltip("Обороты двигателя для переключения вверх (RPM)")]
        public float ShiftUpRpm = 5800f;

        [Tooltip("Обороты двигателя для переключения вниз (RPM)")]
        public float ShiftDownRpm = 1900f;

        [Tooltip("Длительность разрыва потока мощности при переключении (секунды)")]
        public float ShiftDuration = 0.25f;

        [Header("Тормозная система")]
        [Tooltip("Максимальный рабочий тормозной момент педали (Нм)")]
        public float MaxBrakeTorque = 1800f;

        [Tooltip("Баланс тормозных сил (0.72 = 72% перед, 28% зад)")]
        [Range(0.4f, 0.9f)]
        public float FrontBrakeBias = 0.72f;

        [Tooltip("Максимальный момент стояночного тормоза на заднюю ось (Нм)")]
        public float MaxHandbrakeTorque = 2400f;

        private const float RadPerSecToRpm = 60f / (2f * Mathf.PI);
        private float _reverseHoldTimer;

        public SimpleTransmissionModule()
        {
            ModuleId = "Powertrain.Transmission";
        }

        public void UpdateTransmission(
            in VehicleInput input,
            in EngineOutput engineOutput,
            ref VehicleState state,
            ref WheelData4<WheelActuation> actuation,
            in VehicleChassisConfig chassis,
            float dt,
            out TransmissionFeedback feedback)
        {
            if (!IsEnabled)
            {
                feedback = TransmissionFeedback.Free;
                return;
            }

            // Инициализация нейтрали/1-й передачи при старте
            if (state.CurrentGear == 0)
            {
                state.CurrentGear = 1;
                state.ClutchEngagement = 1f;
            }

            // 1. Таймер переключения передачи
            if (state.ShiftTimer > 0f)
            {
                state.ShiftTimer -= dt;
                if (state.ShiftTimer <= 0f)
                {
                    state.ShiftTimer = 0f;
                    state.ClutchEngagement = 1f;
                }
                else
                {
                    state.ClutchEngagement = 0.1f;
                }
            }
            else
            {
                state.ClutchEngagement = 1f;
            }

            // 2. Логика автоматического переключения передач
            if (AutomaticShift && state.ShiftTimer <= 0f)
            {
                UpdateAutoShift(in input, ref state, in chassis, dt);
            }

            // 3. Вычисление эффективного передаточного числа
            float currentRatio = GetGearRatio(state.CurrentGear);
            float totalRatio = currentRatio * FinalDriveRatio;

            // 4. Определение тяги и торможения в зависимости от режима (D или R)
            float driveDemand;
            float brakeDemand;

            if (state.CurrentGear == -1)
            {
                // Задний ход: тормоз (S) управляет реверсивной тягой, газ (W) тормозит
                driveDemand = Mathf.Clamp01(input.Brake);
                brakeDemand = Mathf.Clamp01(input.Throttle);
            }
            else
            {
                // Передний ход: газ (W) управляет тягой вперед, тормоз (S) тормозит
                driveDemand = Mathf.Clamp01(input.Throttle);
                brakeDemand = Mathf.Clamp01(input.Brake);
            }

            float netDriveTorque = engineOutput.OutputTorque * totalRatio * TransmissionEfficiency * state.ClutchEngagement;

            // Распределение крутящего момента по осям (FL, FR, RL, RR)
            float frontRatio;
            float rearRatio;

            switch (Drivetrain)
            {
                case DrivetrainType.FrontWheelDrive:
                    frontRatio = 1.0f;
                    rearRatio = 0.0f;
                    break;
                case DrivetrainType.RearWheelDrive:
                    frontRatio = 0.0f;
                    rearRatio = 1.0f;
                    break;
                case DrivetrainType.AllWheelDrive:
                default:
                    frontRatio = FrontTorqueSplitAwd;
                    rearRatio = 1.0f - FrontTorqueSplitAwd;
                    break;
            }

            float torquePerFrontWheel = (netDriveTorque * frontRatio) * 0.5f;
            float torquePerRearWheel = (netDriveTorque * rearRatio) * 0.5f;

            // Защита от гипер-раскрутки вывешенных колес в воздухе (не быстрее максимальных оборотов передачи)
            float maxWheelOmega = Mathf.Abs(totalRatio) > 0.01f
                ? (7200f / RadPerSecToRpm) / Mathf.Abs(totalRatio)
                : 200f;

            actuation.GetRef(0).DriveTorque += (Mathf.Abs(state.Wheels[0].AngularVelocity) < maxWheelOmega) ? torquePerFrontWheel : 0f; // FL
            actuation.GetRef(1).DriveTorque += (Mathf.Abs(state.Wheels[1].AngularVelocity) < maxWheelOmega) ? torquePerFrontWheel : 0f; // FR
            actuation.GetRef(2).DriveTorque += (Mathf.Abs(state.Wheels[2].AngularVelocity) < maxWheelOmega) ? torquePerRearWheel : 0f;  // RL
            actuation.GetRef(3).DriveTorque += (Mathf.Abs(state.Wheels[3].AngularVelocity) < maxWheelOmega) ? torquePerRearWheel : 0f;  // RR

            // 5. Распределение тормозных усилий
            float totalBrakeTorque = brakeDemand * MaxBrakeTorque;

            float frontBrakeTorque = (totalBrakeTorque * FrontBrakeBias) * 0.5f;
            float rearBrakeTorque = (totalBrakeTorque * (1f - FrontBrakeBias)) * 0.5f;

            actuation.GetRef(0).BrakeTorque += frontBrakeTorque;
            actuation.GetRef(1).BrakeTorque += frontBrakeTorque;
            actuation.GetRef(2).BrakeTorque += rearBrakeTorque;
            actuation.GetRef(3).BrakeTorque += rearBrakeTorque;

            // Стояночный тормоз на заднюю ось
            float handbrakeDemand = Mathf.Clamp01(input.Handbrake);
            float handbrakeTorquePerRearWheel = (handbrakeDemand * MaxHandbrakeTorque) * 0.5f;

            actuation.GetRef(2).HandbrakeTorque += handbrakeTorquePerRearWheel;
            actuation.GetRef(3).HandbrakeTorque += handbrakeTorquePerRearWheel;

            // 6. Формирование обратной связи для двигателя
            float drivenWheelsSpeed = GetDrivenWheelsAverageAngularVelocity(in state);
            float drivetrainRpm = Mathf.Abs(drivenWheelsSpeed * totalRatio) * RadPerSecToRpm;

            feedback = new TransmissionFeedback
            {
                DrivetrainRpm = drivetrainRpm,
                LoadTorque = 0f,
                ClutchEngagement = state.ClutchEngagement,
                IsDisengaged = (state.CurrentGear == 0 || state.ShiftTimer > 0f)
            };
        }

        private float GetGearRatio(int gear)
        {
            if (gear == -1)
                return ReverseGearRatio;
            if (gear <= 0 || ForwardGearRatios == null || ForwardGearRatios.Length == 0)
                return 0f;

            int index = Mathf.Clamp(gear - 1, 0, ForwardGearRatios.Length - 1);
            return ForwardGearRatios[index];
        }

        private void UpdateAutoShift(in VehicleInput input, ref VehicleState state, in VehicleChassisConfig chassis, float dt)
        {
            float rpm = state.EngineRpm;
            int gear = state.CurrentGear;

            float vehicleGroundSpeed = GetVehicleGroundSpeed(in state, in chassis);

            // Переход на заднюю передачу при осознанном удержании тормоза на полной остановке
            if (gear >= 1 && vehicleGroundSpeed < 0.4f && input.Brake > 0.6f && input.Throttle < 0.05f)
            {
                _reverseHoldTimer += dt;
                if (_reverseHoldTimer > 0.35f)
                {
                    state.CurrentGear = -1;
                    state.ShiftTimer = ShiftDuration;
                    _reverseHoldTimer = 0f;
                    return;
                }
            }
            else
            {
                _reverseHoldTimer = 0f;
            }

            // Переход из задней передачи в 1-ю вперед при нажатии газа на замедленной машине
            if (gear == -1 && input.Throttle > 0.2f && vehicleGroundSpeed < 0.5f)
            {
                state.CurrentGear = 1;
                state.ShiftTimer = ShiftDuration;
                return;
            }

            // Переключение вперед (1 -> 2 -> ... -> N)
            // Защита от переключения на повышенную передачу при пробуксовке на месте: требует реальной линейной скорости
            if (gear >= 1 && ForwardGearRatios != null && gear < ForwardGearRatios.Length)
            {
                float minSpeedForUpshift = gear * 3.5f; // 1->2: ~12 км/ч, 2->3: ~25 км/ч...
                if (rpm > ShiftUpRpm && vehicleGroundSpeed > minSpeedForUpshift)
                {
                    state.CurrentGear = gear + 1;
                    state.ShiftTimer = ShiftDuration;
                    return;
                }
            }

            // Переключение вниз (N -> ... -> 2 -> 1)
            if (gear > 1)
            {
                float minSpeedForCurrentGear = (gear - 1) * 2.8f;
                if (rpm < ShiftDownRpm || vehicleGroundSpeed < minSpeedForCurrentGear)
                {
                    state.CurrentGear = gear - 1;
                    state.ShiftTimer = ShiftDuration;
                }
            }
        }

        private float GetVehicleGroundSpeed(in VehicleState state, in VehicleChassisConfig chassis)
        {
            if (Drivetrain == DrivetrainType.FrontWheelDrive)
            {
                // Для переднего привода задние колеса свободно катятся и точно показывают скорость автомобиля
                return (Mathf.Abs(state.Wheels[2].AngularVelocity) * chassis.WheelRadii[2] +
                        Mathf.Abs(state.Wheels[3].AngularVelocity) * chassis.WheelRadii[3]) * 0.5f;
            }
            if (Drivetrain == DrivetrainType.RearWheelDrive)
            {
                // Для заднего привода передние колеса свободно катятся
                return (Mathf.Abs(state.Wheels[0].AngularVelocity) * chassis.WheelRadii[0] +
                        Mathf.Abs(state.Wheels[1].AngularVelocity) * chassis.WheelRadii[1]) * 0.5f;
            }

            // Для AWD берем наименьшую скорость по осям (ось с меньшим срывом)
            float frontSpeed = (Mathf.Abs(state.Wheels[0].AngularVelocity) * chassis.WheelRadii[0] +
                                Mathf.Abs(state.Wheels[1].AngularVelocity) * chassis.WheelRadii[1]) * 0.5f;
            float rearSpeed = (Mathf.Abs(state.Wheels[2].AngularVelocity) * chassis.WheelRadii[2] +
                               Mathf.Abs(state.Wheels[3].AngularVelocity) * chassis.WheelRadii[3]) * 0.5f;
            return Mathf.Min(frontSpeed, rearSpeed);
        }

        private float GetDrivenWheelsAverageAngularVelocity(in VehicleState state)
        {
            switch (Drivetrain)
            {
                case DrivetrainType.FrontWheelDrive:
                    return (state.Wheels[0].AngularVelocity + state.Wheels[1].AngularVelocity) * 0.5f;
                case DrivetrainType.RearWheelDrive:
                    return (state.Wheels[2].AngularVelocity + state.Wheels[3].AngularVelocity) * 0.5f;
                case DrivetrainType.AllWheelDrive:
                default:
                    return (state.Wheels[0].AngularVelocity + state.Wheels[1].AngularVelocity +
                            state.Wheels[2].AngularVelocity + state.Wheels[3].AngularVelocity) * 0.25f;
            }
        }
    }
}
