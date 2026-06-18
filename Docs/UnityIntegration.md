# Unity Integration

The Unity layer is the first host implementation for VALYR. Its job is to connect the engine-independent simulation core to Unity's scene, physics, assets, and editor workflow.

Unity is not the source of the vehicle model. It is the runtime environment that currently drives and visualizes it.

## Main Classes

### VehicleController

Location: `Assets/Scripts/Unity/VehicleController.cs`

`VehicleController` is the MonoBehaviour entry point for a vehicle in a Unity scene.

It is responsible for:

- reading a `VehiclePreset`;
- creating `VehicleConfig`;
- creating `VehicleCore`;
- collecting wheel hard points from scene transforms;
- creating the Unity host;
- applying mass, center of mass, and inertia tensor settings to the `Rigidbody`;
- running the simulation in the fixed update;
- synchronizing visual wheel transforms from simulation state.

The controller should stay thin. Its purpose is orchestration, not vehicle physics.

### UnityVehicleHost

Location: `Assets/Scripts/Unity/UnityVehicleHost.cs`

`UnityVehicleHost` implements `IVehicleHost` using Unity APIs.

It wraps:

- `Rigidbody` state;
- Unity transform data;
- `Physics.Raycast`;
- point velocity queries;
- force application through `AddForceAtPosition`;
- surface velocity for moving colliders;
- wheel sweep support through a hidden convex `MeshCollider`.

This class is the main portability reference. A host for another engine would follow the same shape.

### VehiclePreset

Location: `Assets/Scripts/Unity/Data/VehiclePreset.cs`

`VehiclePreset` is the Unity-authored vehicle definition.

It contains:

- body mass;
- center of mass;
- inertia tensor scale;
- physics substep count;
- axle definitions;
- module entries;
- references to module config assets.

At runtime it creates a pure `VehicleConfig` for the core.

### Generated Configs

Location: `Assets/Scripts/Unity/GeneratedConfigs`

Core modules use nested `ModuleConfig` classes for tunable settings. Unity does not expose that pattern cleanly in the Inspector, so the project generates ScriptableObject wrappers for module configs.

The generator lives at:

`Assets/Editor/ModuleConfigGenerator.cs`

The Unity menu item is:

`VALYR -> Generate Config Assets`

Generated wrappers implement `IModuleConfigAsset`, which lets `VehiclePreset` pass the correct config object into the matching core module.

## Asset Workflow

The typical Unity data path is:

1. A `VehiclePreset` asset describes the vehicle.
2. Each module entry points to a core module type.
3. Each module may reference a generated config asset.
4. `VehiclePreset.CreateConfig()` clones module logic and applies config data.
5. `VehicleController` passes the resulting `VehicleConfig` into `VehicleCore`.

This keeps Unity asset authoring separate from the simulation runtime.

## Scene Boundary

The core does not know about scene objects. The only scene-derived data passed into the core during initialization is the wheel hard point array.

Hard points represent suspension attachment positions in the vehicle's local space. Visual wheel objects remain Unity-side and are synchronized after the simulation updates.

## Telemetry

`VehicleDebugger` provides runtime telemetry for development. It reads the core context and displays body, powertrain, suspension, wheel, tire, and contact information.

The debugger is intentionally outside the core. It is a Unity development tool, not part of the simulation model.

## Unity Version

The current project version is:

`6000.4.0f1`

The project also uses Unity packages such as Input System, URP, Cinemachine, ProBuilder, and Unity Test Framework. The vehicle core itself is not designed around those packages; they belong to the Unity project environment.

## Integration Notes

The Unity implementation should remain replaceable.

When adding features, prefer this split:

- simulation logic in `Assets/Scripts/Core`;
- Unity object references and editor tooling in `Assets/Scripts/Unity` or `Assets/Editor`;
- generated Inspector wrappers in `Assets/Scripts/Unity/GeneratedConfigs`;
- sample assets in `Assets/CarSettings` and `Assets/Scenes`.

This keeps VALYR usable as a simulation framework rather than only as a Unity scene script.
