# Architecture

VALYR is built around a clear separation between vehicle simulation and game-engine integration.

The core idea is that a car should not be defined as a Unity behavior. Unity should host the car, provide physics queries, and receive forces, but the simulation itself should live in a reusable layer.

## Layers

### Core

Location: `Assets/Scripts/Core`

The core contains the simulation model:

- `VehicleCore` runs the vehicle update;
- `VehicleConfig` describes static vehicle setup;
- `VehicleState` contains runtime state for the body, engine, transmission, clutch, axles, wheels, suspension, contacts, and tires;
- `VehicleContext` is the shared state passed through modules;
- `IVehicleModule` defines simulation modules;
- `IVehicleHost` defines the boundary between the simulation and a host engine;
- `Vec3`, `Quat`, and math helpers keep the core independent from Unity types;
- `Core/Modules` contains the current vehicle behavior modules.

The core must not reference `UnityEngine`.

### Host

A host is the engine-specific layer that implements `IVehicleHost`.

The host gives the core access to the outside world:

- timestep;
- rigidbody transform;
- linear and angular velocity;
- point velocity;
- collision queries;
- surface velocity;
- force application;
- input state.

Unity currently provides the only host implementation through `UnityVehicleHost`.

## Simulation Flow

`VehicleCore` owns a `VehicleContext` and two module lists:

- macro modules, updated once per physics tick;
- tire-phase modules, updated using substeps.

The high-level update flow is:

1. Read `DeltaTime` from the host.
2. Read input from the host.
3. Recalculate body state from the host.
4. Run non-tire modules in phase order.
5. Run tire modules in substeps.
6. Apply tire and normal forces through the host.

Substeps are configured through `VehicleConfig.subSteps`. They are currently used for tire behavior, where instability is most likely to appear.

## Module Phases

Modules declare their execution point with `VehicleModulePhase`.

| Phase | Responsibility |
| --- | --- |
| `InputModifiers` | Driving assists and input shaping |
| `GroundDetection` | Wheel contact detection |
| `Suspension` | Springs, dampers, and suspension length |
| `AntiRollBar` | Cross-axle normal force adjustment |
| `Steering` | Wheel steering angles |
| `Engine` | Engine torque and RPM state |
| `Transmission` | Gearbox, clutch, and output torque |
| `Differential` | Torque distribution to axles and wheels |
| `Tire` | Tire slip, friction forces, and wheel angular state |

This phase order is part of the architecture. For example, suspension depends on contact, tires depend on drive and brake torque, and drivetrain behavior depends on transmission output.

## Shared Context

Modules communicate through `VehicleContext` rather than through direct references to each other.

That context contains:

- vehicle configuration;
- host reference;
- body state;
- input state;
- engine state;
- clutch state;
- transmission state;
- axle contexts;
- wheel contexts.

This makes the simulation easy to inspect and keeps module dependencies visible through state and phase order.

## Host Contract

`IVehicleHost` is the portability boundary:

```csharp
public interface IVehicleHost
{
    float DeltaTime { get; }
    InputState GetInput();
    Vec3 GetPosition();
    Quat GetRotation();
    Vec3 GetLinearVelocity();
    Vec3 GetAngularVelocity();
    Vec3 GetPointVelocity(Vec3 worldPoint);
    Vec3 GetLocalPointVelocity(Vec3 worldPoint);
    bool Raycast(Vec3 origin, Vec3 direction, float maxDistance, out RaycastResult result);
    bool SweepWheel(int wheelIndex, Vec3 origin, Quat rotation, Vec3 direction, float distance, float radius, float width, out RaycastResult result);
    void ApplyForce(Vec3 force, Vec3 position);
}
```

Porting VALYR to another engine mostly means implementing this interface and converting between that engine's math and physics types and the core's `Vec3` and `Quat`.

## Current Modules

`DrivingAssistsModule` modifies throttle and brake behavior for ABS, TCS, and ESP-style assistance.

`WheelContactModule` samples wheel contact using a ray array across the wheel width and tire arc.

`SuspensionModule` calculates spring and damper forces and maintains suspension travel state.

`AntiRollBarModule` adjusts normal force between left and right wheels on the same axle.

`SteeringModule` calculates steering angle and optional Ackermann correction.

`EngineModule` models RPM, torque, idle correction, friction, and rev limiting.

`GearboxModule` handles gear selection, automatic shifting, clutch behavior, output torque, auto-hold, and brake-to-reverse behavior.

`DrivetrainModule` distributes torque across drive layouts and differential modes.

`TireModule` computes longitudinal and lateral slip, combined friction, rolling resistance, brake torque, handbrake behavior, and visual wheel angular velocity.

## Extension Rules

Keep new simulation behavior in `Core`.

Use `IVehicleHost` when a module needs information from the engine world.

Use `ModuleConfig` for tunable parameters.

Do not let module behavior depend on Unity scene objects.

Prefer explicit state in `VehicleContext` over hidden cross-module references.

If two systems depend on each other, make the dependency clear through `VehicleModulePhase` or through named context state.
