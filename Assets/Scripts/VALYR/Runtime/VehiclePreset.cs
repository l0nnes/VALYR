using System;
using System.Collections.Generic;
using UnityEngine;
using VALYR.Core;
using VALYR.Core.Modules;
using VALYR.Modules.Assists;
using VALYR.Modules.Powertrain;
using VALYR.Modules.Steering;
using VALYR.Modules.Suspension;
using VALYR.Modules.Tires;

namespace VALYR.Runtime
{
    /// <summary>
    /// ScriptableObject asset storing a reusable vehicle configuration and module profile.
    /// Designers can create multiple presets (e.g. Sedan, Sport, Offroad) in the Project browser
    /// and apply them to any VehicleController.
    /// </summary>
    [CreateAssetMenu(fileName = "NewVehiclePreset", menuName = "VALYR/Vehicle Preset", order = 10)]
    public class VehiclePreset : ScriptableObject
    {
        [Header("Chassis & Mass")]
        [Tooltip("Curb mass of the vehicle chassis in kg.")]
        [Min(100f)]
        public float ChassisMass = 1180f;

        [Tooltip("Custom center of mass offset relative to vehicle origin.")]
        public Vector3 CenterOfMass = new Vector3(0f, -0.22f, 0.12f);

        [Tooltip("Unloaded outer rolling radius of the wheels in meters.")]
        [Range(0.15f, 0.70f)]
        public float WheelRadius = 0.31f;

        [Tooltip("Road surface collision layer mask.")]
        public LayerMask GroundLayers = ~((1 << 2) | (1 << 3));

        [Header("Modules")]
        [SerializeReference] public ISuspensionModule Suspension = new RaycastSuspensionModule();
        [SerializeReference] public ISteeringModule Steering = new DirectSteeringModule();
        [SerializeReference] public IEngineModule Engine = new ArcadeEngineModule();
        [SerializeReference] public ITransmissionModule Transmission = new SimpleTransmissionModule();
        [SerializeReference] public ITireModule Tires = new BrushTireModule();

        [SerializeReference] public List<VehicleModule> Assists = new()
        {
            new AntiRollBarModule(),
            new AbsModule(),
            new TcsModule()
        };

        /// <summary>
        /// Applies this preset's settings and clones all modules onto the target VehicleController.
        /// </summary>
        public void ApplyTo(VehicleController controller)
        {
            if (controller == null)
                return;

            controller.ChassisMass = ChassisMass;
            controller.CenterOfMass = CenterOfMass;
            controller.WheelRadius = WheelRadius;
            controller.GroundLayers = GroundLayers;

            controller.Suspension = CloneModule(Suspension);
            controller.Steering = CloneModule(Steering);
            controller.Engine = CloneModule(Engine);
            controller.Transmission = CloneModule(Transmission);
            controller.Tires = CloneModule(Tires);

            controller.Assists = new List<VehicleModule>();
            if (Assists != null)
            {
                for (int i = 0; i < Assists.Count; i++)
                {
                    if (Assists[i] != null)
                        controller.Assists.Add(CloneModule(Assists[i]));
                }
            }

            controller.UpdateChassisGeometry();
        }

        /// <summary>
        /// Captures the target VehicleController's parameters and clones its modules into this preset asset.
        /// </summary>
        public void CaptureFrom(VehicleController controller)
        {
            if (controller == null)
                return;

            ChassisMass = controller.ChassisMass;
            CenterOfMass = controller.CenterOfMass;
            WheelRadius = controller.WheelRadius;
            GroundLayers = controller.GroundLayers;

            Suspension = CloneModule(controller.Suspension);
            Steering = CloneModule(controller.Steering);
            Engine = CloneModule(controller.Engine);
            Transmission = CloneModule(controller.Transmission);
            Tires = CloneModule(controller.Tires);

            Assists = new List<VehicleModule>();
            if (controller.Assists != null)
            {
                for (int i = 0; i < controller.Assists.Count; i++)
                {
                    if (controller.Assists[i] != null)
                        Assists.Add(CloneModule(controller.Assists[i]));
                }
            }
        }

        private static T CloneModule<T>(T source) where T : class
        {
            if (source == null)
                return null;

            string json = JsonUtility.ToJson(source);
            return (T)JsonUtility.FromJson(json, source.GetType());
        }
    }
}
