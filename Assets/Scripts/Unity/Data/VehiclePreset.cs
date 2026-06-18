using System;
using System.Collections.Generic;
using Core;
using Core.Interfaces;
using Unity.Attributes;
using Unity.Interfaces;
using UnityEngine;

namespace Unity.Data
{
    [CreateAssetMenu(fileName = "VehiclePreset", menuName = "Vehicle/Preset")]
    public class VehiclePreset : ScriptableObject
    {
        public float bodyMass = 1200f;
        public Vector3 centerOfMass = new(0f, -0.35f, 0f);
        public Vector3 inertiaTensorScale = new(1.15f, 1.25f, 1.05f);
        [Range(1, 10)] public int physicsSubSteps = 4;
        public List<ModuleEntry> modules = new();
        public List<AxleSetup> axles = new();

        private void OnValidate()
        {
            foreach (var m in modules) m.Validate();
        }

        public VehicleConfig CreateConfig()
        {
            var config = new VehicleConfig
            {
                subSteps = physicsSubSteps,
                mass = bodyMass,
                centerOfMass = centerOfMass,
                inertiaTensorScale = inertiaTensorScale,
                axles = new AxleConfig[axles.Count],
                modules = new IVehicleModule[modules.Count]
            };

            for (var i = 0; i < axles.Count; i++) config.axles[i] = axles[i].ToCore();

            for (var i = 0; i < modules.Count; i++)
            {
                var entry = modules[i];
                if (entry.moduleLogic == null) continue;

                var json = JsonUtility.ToJson(entry.moduleLogic);
                var moduleInstance = (IVehicleModule)JsonUtility.FromJson(json, entry.moduleLogic.GetType());

                if (entry.configAsset is IModuleConfigAsset configInterface)
                {
                    if (configInterface.GetTargetModuleType() == moduleInstance.GetType())
                        moduleInstance.SetConfiguration(configInterface.GetConfig());
                    else
                        Debug.LogError($"[VehiclePreset] Type Mismatch! Module '{moduleInstance.GetType().Name}' " +
                                       $"cannot accept config for '{configInterface.GetTargetModuleType()?.Name}'. Using defaults.");
                }
                else if (entry.configAsset != null)
                {
                    Debug.LogWarning(
                        $"[VehiclePreset] Assigned config asset '{entry.configAsset.name}' does not implement IModuleConfigAsset. Regenerate configs.");
                }

                config.modules[i] = moduleInstance;
            }

            return config;
        }

        [Serializable]
        public class ModuleEntry
        {
            [HideInInspector] public string name;
            [HideInInspector] public VehicleModulePhase phase;

            [SerializeReference] [SubclassSelector]
            public IVehicleModule moduleLogic;

            public ScriptableObject configAsset;

            public void Validate()
            {
                if (moduleLogic != null)
                {
                    phase = moduleLogic.Phase;
                    name = $"{phase} ({moduleLogic.GetType().Name})";
                }
                else
                {
                    name = "Empty Module";
                }
            }
        }

        [Serializable]
        public struct AxleSetup
        {
            public string name;
            public bool isPowered;
            public SteeringMode steeringMode;

            [Header("Wheel Configuration")] public float radius;
            public float width;
            public float mass;

            public AxleConfig ToCore()
            {
                return new AxleConfig
                {
                    isPowered = isPowered,
                    steeringMode = steeringMode,
                    wheel = new WheelConfig
                    {
                        radius = radius > 0 ? radius : 0.35f,
                        width = width > 0 ? width : 0.25f,
                        mass = mass > 0 ? mass : 15f,
                        inertia = (mass > 0 ? mass : 15f) * (radius > 0 ? radius : 0.35f) *
                                  (radius > 0 ? radius : 0.35f)
                    }
                };
            }
        }
    }
}