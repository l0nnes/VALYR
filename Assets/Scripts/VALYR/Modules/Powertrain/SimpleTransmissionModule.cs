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
        public DrivetrainType Drivetrain = DrivetrainType.AllWheelDrive;

        [Tooltip("Доля момента на переднюю ось для AWD (0.4 = 40% перед, 60% зад)")]
        [Range(0f, 1f)]
        public float FrontTorqueSplitAwd = 0.40f;

        [Header("Передаточные числа")]
        [Tooltip("Передаточное число заднего хода (отрицательное число)")]
        public float ReverseGearRatio = -3.50f;

        [Tooltip("Передаточные числа передач вперед (1-я, 2-я, ... N-я)")]
        public float[] ForwardGearRatios = { 3.82f, 2.36f, 1.69f, 1.31f, 1.00f, 0.79f };

        [Tooltip("Главная передача редуктора (Final Drive Ratio)")]
        public float FinalDriveRatio = 3.90f;

        [Tooltip("Механический КПД трансмиссии (0.85 .. 0.96)")]
        [Range(0.7f, 1f)]
        public float TransmissionEfficiency = 0.92f;

        [Header("Автоматическое переключение")]
        public bool AutomaticShift = true;

        [Tooltip("Обороты двигателя для переключения вверх (RPM)")]
        public float ShiftUpRpm = 6200f;

        [Tooltip("Обороты двигателя для переключения вниз (RPM)")]
        public float ShiftDownRpm = 2500f;

        [Tooltip("Длительность разрыва потока мощности при переключении (секунды)")]
        public float ShiftDuration = 0.20f;

        [Header("Тормозная система")]
        [Tooltip("Максимальный рабочий тормозной момент педали (Нм)")]
        public float MaxBrakeTorque = 2500f;

        [Tooltip("Баланс тормозных сил (0.65 = 65% перед, 35% зад)")]
        [Range(0.4f, 0.9f)]
        public float FrontBrakeBias = 0.65f;

        [Tooltip("Максимальный момент стояночного тормоза на заднюю ось (Нм)")]
        public float MaxHandbrakeTorque = 3500f;

        private const float RadPerSecToRpm = 60f / (2f * Mathf.PI);

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
                UpdateAutoShift(in input, ref state);
            }

            // 3. Вычисление эффективного передаточного числа
            float currentRatio = GetGearRatio(state.CurrentGear);
            float totalRatio = currentRatio * FinalDriveRatio;

            // 4. Расчет суммарного крутящего момента на колесах
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

            actuation.GetRef(0).DriveTorque += torquePerFrontWheel; // FL
            actuation.GetRef(1).DriveTorque += torquePerFrontWheel; // FR
            actuation.GetRef(2).DriveTorque += torquePerRearWheel;  // RL
            actuation.GetRef(3).DriveTorque += torquePerRearWheel;  // RR

            // 5. Распределение тормозных усилий
            float brakeDemand = Mathf.Clamp01(input.Brake);
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

        private void UpdateAutoShift(in VehicleInput input, ref VehicleState state)
        {
            float rpm = state.EngineRpm;
            int gear = state.CurrentGear;

            // Переход на заднюю передачу при торможении с околонулевой скоростью
            float avgSpeed = (Mathf.Abs(state.Wheels[0].AngularVelocity) + Mathf.Abs(state.Wheels[1].AngularVelocity)) * 0.5f;
            if (gear >= 1 && avgSpeed < 1.0f && input.Brake > 0.5f && input.Throttle < 0.1f)
            {
                state.CurrentGear = -1;
                state.ShiftTimer = ShiftDuration;
                return;
            }

            // Переход из задней передачи в 1-ю вперед при нажатии газа
            if (gear == -1 && avgSpeed < 1.0f && input.Throttle > 0.3f && input.Brake < 0.1f)
            {
                state.CurrentGear = 1;
                state.ShiftTimer = ShiftDuration;
                return;
            }

            // Переключение вперед (1 -> 2 -> ... -> N)
            if (gear >= 1 && ForwardGearRatios != null && gear < ForwardGearRatios.Length)
            {
                if (rpm > ShiftUpRpm)
                {
                    state.CurrentGear = gear + 1;
                    state.ShiftTimer = ShiftDuration;
                    return;
                }
            }

            // Переключение вниз (N -> ... -> 2 -> 1)
            if (gear > 1)
            {
                if (rpm < ShiftDownRpm)
                {
                    state.CurrentGear = gear - 1;
                    state.ShiftTimer = ShiftDuration;
                }
            }
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
