# VALYR

VALYR is a modular vehicle simulation framework built around an engine-agnostic core and a Unity implementation.

The project is currently developed inside Unity, but the simulation code is intentionally separated from Unity-specific APIs. The long-term goal is to make vehicle behavior portable across game engines instead of baking the entire model into one runtime, one physics wrapper, or one scene setup.

## Why This Exists

Most vehicle systems in games start as engine scripts: a rigidbody, a few raycasts, some torque values, and a growing pile of special cases. That works for a prototype, but it becomes hard to reason about once the car needs real drivetrain behavior, tunable tires, assists, debugging tools, and different host environments.

VALYR tries to solve that by splitting the problem into two parts:

- a pure simulation core that owns vehicle state, modules, math, and update order;
- a host layer that connects the core to a game engine, physics body, input source, and collision queries.

Unity is the first host. It is not meant to be the only one.

## What VALYR Models

VALYR is focused on physically motivated vehicle behavior rather than a single arcade controller. It is not a full motorsport-grade simulator, but it is built as a system that can grow in that direction.

Current simulation areas include:

- soft wheel contact sampling;
- suspension compression, damping, and bump stops;
- anti-roll bar force distribution;
- steering with Ackermann support;
- engine torque, idle behavior, friction, and rev limiting;
- manual and automatic gearbox behavior;
- clutch engagement and driveline torque transfer;
- FWD, RWD, AWD, and custom axle layouts;
- open, limited-slip, and locked differential behavior;
- longitudinal and lateral tire slip;
- combined tire friction;
- ABS, TCS, and ESP-style driving assists;
- runtime telemetry for inspecting the vehicle state.

## Design Goals

VALYR is built around a few constraints that shape the codebase.

**Engine independence.** The core must not depend on `UnityEngine`. Engine-specific work belongs in an adapter.

**Modular simulation.** A vehicle is assembled from modules with explicit phases: contact, suspension, steering, powertrain, differential, tires, and assists.

**Inspectable state.** The simulation keeps vehicle, axle, wheel, tire, engine, clutch, and transmission state in a shared context so debugging and tuning are practical.

**Configurable behavior.** Module settings are represented as serializable config objects. Unity exposes them through generated ScriptableObject wrappers.

**Real-time pragmatism.** The system favors stable, tunable real-time behavior over pretending to be a perfect physics paper.

## Project Status

VALYR is in active development.

The architecture is already split into a reusable core and a Unity host. The current vehicle model runs, can be configured through Unity assets, and exposes useful telemetry. At the same time, several parts are still experimental and need more validation: tire tuning, contact behavior on difficult geometry, high-speed stability, and automated tests for the core.

Treat the project as a vehicle simulation foundation, not a finished drop-in asset.

## Repository Layout

```text
Assets/
  Scripts/
    Core/                 Engine-agnostic vehicle simulation
      Modules/            Engine, gearbox, tire, suspension, assists, etc.
      Contexts/           Runtime vehicle, axle, and wheel context
      Interfaces/         Host and module contracts
      Math/               Core vector, quaternion, and curve utilities
    Unity/                Unity host implementation
      Data/               VehiclePreset and Unity-facing data assets
      GeneratedConfigs/   ScriptableObject wrappers for module configs
      Input/              Unity Input System generated code
      Editor/             Unity editor tooling
  CarSettings/            Example vehicle preset and module config assets
  Scenes/                 Sample Unity scene
Docs/
  Architecture.md         Core design and module flow
  UnityIntegration.md     How the Unity host connects to the core
```

## Core Architecture

The central class is `VehicleCore`. It owns a `VehicleContext`, sorts modules by `VehicleModulePhase`, and runs the simulation step.

The core talks to the outside world through `IVehicleHost`. A host provides:

- fixed timestep;
- current body transform and velocities;
- point velocity queries;
- raycasts and wheel sweeps;
- input state;
- force application.

In Unity, this contract is implemented by `UnityVehicleHost`, which wraps a `Rigidbody` and Unity Physics. A different engine would need its own host implementation, but the simulation modules should remain the same.

For more detail, see [Docs/Architecture.md](Docs/Architecture.md).

## Unity Implementation

The Unity layer is responsible for scene integration, asset authoring, visual wheel synchronization, and editor tooling.

`VehicleController` creates the core from a `VehiclePreset`, passes hard points into the simulation, applies Unity rigidbody settings, and runs `VehicleCore.Simulate()` during the fixed update.

`VehiclePreset` is the Unity-facing vehicle definition. It contains body properties, axle definitions, and the list of simulation modules used by the vehicle. Module configs are generated as ScriptableObject assets so they can be edited in the Inspector while staying compatible with the engine-independent core.

The current project version is Unity `6000.4.0f1`.

For Unity-specific notes, see [Docs/UnityIntegration.md](Docs/UnityIntegration.md).

## Extending VALYR

New behavior should usually be added as a core module:

1. Implement `IVehicleModule`.
2. Choose the correct `VehicleModulePhase`.
3. Read and write state through `VehicleContext`.
4. Put tunable values in a `ModuleConfig`.
5. Regenerate Unity config wrappers if the module needs Inspector-editable settings.

The important rule is simple: simulation logic belongs in `Core`; engine plumbing belongs in the host layer.

## Current Limitations

- The Unity host is the only implemented host.
- The project does not yet include a mature automated test suite for the simulation core.
- Tire and contact behavior are still being tuned.
- The engine model is intentionally simplified.
- The current sample setup is useful for development, not a polished vehicle package.

## License

No license has been added yet.
