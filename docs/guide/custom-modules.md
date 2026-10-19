# Creating Custom Modules

VALYR is designed from the ground up to be fully modular. Every subsystem — whether it's steering, contact scanning, suspension solvers, powertrain, tires, or driver assists — is a pluggable C# class serialized directly on the `VehicleController` using Unity's `[SerializeReference]`.

You do not need to modify package code or fork the repository to create new vehicle behaviors. Any class in your project that implements a subsystem interface is automatically discovered in the Inspector.

---

## 🏗 Core Concepts & Lifecycle

All modules in VALYR inherit from the base class `VehicleModule` and implement one or more subsystem interfaces.

### 1. Module Base Class
```csharp
namespace VALYR.Core.Modules
{
    [System.Serializable]
    public abstract class VehicleModule
    {
        [HideInInspector] public string ModuleId;
        [HideInInspector] public bool IsEnabled = true;

        /// <summary>
        /// Called once during vehicle Awake() or when reapplying presets.
        /// Receives immutable geometric chassis configuration (mass, wheel positions, radii).
        /// </summary>
        public virtual void Initialize(in VehicleChassisConfig chassis) { }

        /// <summary>
        /// Called when the vehicle is reset, respawned, or teleported.
        /// </summary>
        public virtual void Reset() { }
    }
}
```

### 2. State-Driven Execution Flow
Unlike typical Unity components, modules **do not run MonoBehaviour updates**. Instead, `VehicleController` calls each module during its deterministic `SimulateStep()`:

```text
Input + VehicleState ➔ [Steering] ➔ [Contact] ➔ [Suspension] ➔ [Assists] ➔ [Powertrain] ➔ [Tires] ➔ New VehicleState
```

- **`ref VehicleState state`**: Holds all dynamic physics data (wheel rotations, RPM, slip, contact points, gear). Modules modify this state directly.
- **`IPhysicsBody body`**: An abstraction over Unity's `Rigidbody` (or `PredictedRigidbody` in multiplayer). Modules use this to query linear/angular velocity and apply physical forces (`AddForceAtPosition`, `AddRelativeTorque`).
- **`in VehicleChassisConfig chassis`**: Provides wheel anchor offsets (`WheelOffsets`), curb mass (`ChassisMass`), and wheel outer radii (`WheelRadii`).
- **`float dt`**: The physics time step (`Time.fixedDeltaTime`).

---

## 🔌 Subsystem Interfaces

VALYR provides dedicated interfaces for each step of the simulation pipeline:

| Subsystem | Interface | Description |
|---|---|---|
| **Steering** | `ISteeringModule` | Calculates steer angles for each wheel based on driver input and vehicle speed. |
| **Contact** | `IWheelContactModule` | Casts rays, shapes, or multi-point patches to find ground contact normals, penetration, and clearance. |
| **Suspension** | `ISuspensionModule` | Computes spring compression, damping, and normal load ($F_z$), applying suspension forces to the body. |
| **Powertrain (Engine)** | `IEngineModule` | Simulates engine RPM, torque curves, and power delivery to the transmission. |
| **Powertrain (Gearbox)** | `ITransmissionModule` | Handles gear ratios, shift timing, clutch locking, and distributes wheel torque (`WheelActuation`). |
| **Tires** | `ITireModule` | Calculates longitudinal/lateral tire slip and generates friction traction forces on the surface. |
| **Chassis Assist** | `IChassisAssistModule` | Applies whole-body forces/torques (anti-roll bars, downforce, air control). |
| **Wheel Assist** | `IWheelAssistModule` | Modulates drive torque and brake pressure before tire solving (ABS, TCS, torque vectoring). |

---

## 🛠 Step-by-Step Tutorial: Creating a Custom Module

Let's build a practical, production-ready module: an **Aerodynamic Downforce & Drag Module** (`IChassisAssistModule`). It pushes the chassis downward into the ground proportionally to the square of its speed ($F_{down} \propto v^2$) to increase high-speed grip, while applying aerodynamic air drag.

### Step 1: Write the Module Class

Create a new C# script anywhere in your project (e.g. `Assets/Scripts/Vehicles/AerodynamicsModule.cs`):

```csharp
using System;
using UnityEngine;
using VALYR.Core;
using VALYR.Core.Interfaces;
using VALYR.Core.Modules;

/// <summary>
/// Aerodynamic downforce and air drag assist module.
/// Fully stateless: evaluates forces deterministically each physics tick
/// without retaining internal mutable fields, making it 100% rollback-safe.
/// </summary>
[Serializable]
public class AerodynamicsModule : VehicleModule, IChassisAssistModule
{
    [Header("Downforce")]
    [Tooltip("Downforce coefficient (N / (m/s)^2). Downward force increases with speed squared.")]
    [Range(0.5f, 15.0f)]
    public float DownforceCoefficient = 3.5f;

    [Tooltip("Maximum downforce clamp in Newtons to prevent excessive suspension compression.")]
    public float MaxDownforce = 20000f;

    [Header("Aerodynamic Drag")]
    [Tooltip("Air resistance drag coefficient (N / (m/s)^2) opposing vehicle velocity.")]
    [Range(0.1f, 3.0f)]
    public float DragCoefficient = 0.45f;

    public AerodynamicsModule()
    {
        ModuleId = "Assists.Aerodynamics";
    }

    public void ApplyChassisAssists(
        ref VehicleState state,
        IPhysicsBody body,
        in VehicleChassisConfig chassis,
        float dt)
    {
        if (!IsEnabled || body == null)
            return;

        Vector3 velocity = body.LinearVelocity;
        float speedSq = velocity.sqrMagnitude;

        // Skip micro-movements at standstill
        if (speedSq < 1.0f)
            return;

        // 1. Aerodynamic Downforce: directed downward along local chassis roof-to-floor axis
        Vector3 downDirection = -(body.Rotation * Vector3.up);
        float downforceMagnitude = Mathf.Min(DownforceCoefficient * speedSq, MaxDownforce);
        body.AddForce(downDirection * downforceMagnitude, ForceMode.Force);

        // 2. Air Drag: directed directly opposite to vehicle world velocity
        Vector3 dragDirection = -velocity.normalized;
        float dragMagnitude = DragCoefficient * speedSq;
        body.AddForce(dragDirection * dragMagnitude, ForceMode.Force);
    }
}
```

### Step 2: Assign the Module in the Inspector

1. Select your vehicle GameObject containing `VehicleController`.
2. Expand the **Driver Assists & Chassis Extensions** foldout.
3. Click the **"Add Assist Module..."** button.
4. Select `Aerodynamics Module` from the dropdown.
5. Tune `DownforceCoefficient` and `DragCoefficient` live in the Inspector.

---

## 🏎 Example 2: Custom Arcade Steering Module

Here is how you implement an arcade steering module (`ISteeringModule`) with speed-sensitive steering reduction:

```csharp
using System;
using UnityEngine;
using VALYR.Core;
using VALYR.Core.Interfaces;
using VALYR.Core.Modules;

[Serializable]
public class ArcadeSteeringModule : VehicleModule, ISteeringModule
{
    [Tooltip("Maximum steer angle in degrees at low speeds.")]
    [Range(15f, 45f)]
    public float MaxSteerAngle = 35f;

    [Tooltip("Minimum steer angle in degrees at top speed.")]
    [Range(5f, 25f)]
    public float HighSpeedSteerAngle = 12f;

    [Tooltip("Speed (m/s) at which steering sensitivity reaches minimum.")]
    public float SpeedAtMinSteer = 50f; // ~180 km/h

    [Tooltip("Steering rack response speed (degrees per second).")]
    public float SteerSpeed = 120f;

    public void UpdateSteering(
        in VehicleInput input,
        ref VehicleState state,
        IPhysicsBody body,
        in VehicleChassisConfig chassis,
        float dt)
    {
        if (!IsEnabled)
            return;

        float forwardSpeed = body != null ? Vector3.Dot(body.Velocity, body.Rotation * Vector3.forward) : 0f;
        float speedFactor = Mathf.Clamp01(Mathf.Abs(forwardSpeed) / SpeedAtMinSteer);
        float activeMaxAngle = Mathf.Lerp(MaxSteerAngle, HighSpeedSteerAngle, speedFactor);

        float targetSteer = input.Steer * activeMaxAngle;

        // Front Left (index 0) and Front Right (index 1)
        for (int i = 0; i < 2; i++)
        {
            float currentAngle = state.Wheels[i].SteerAngle;
            float newAngle = Mathf.MoveTowards(currentAngle, targetSteer, SteerSpeed * dt);

            // Mutate wheel steer angle in the state
            state.Wheels[i].SteerAngle = newAngle;
        }

        // Rear wheels remain unsteered
        state.Wheels[2].SteerAngle = 0f;
        state.Wheels[3].SteerAngle = 0f;
    }
}
```

Now on your `VehicleController`, look at the **Steering Subsystem** dropdown. Your `Arcade Steering Module` will appear alongside `Direct Steering Module`!

---

## ⚡ Fast Script Templates (Context Menu)

VALYR includes built-in Unity template generators. Right-click anywhere in your Project window:
- **Assets ➔ Create ➔ VALYR ➔ Custom Module Script** (general assist/chassis extension)
- **Assets ➔ Create ➔ VALYR ➔ Subsystems ➔ Custom Suspension Module**
- **Assets ➔ Create ➔ VALYR ➔ Subsystems ➔ Custom Steering Module**
- **Assets ➔ Create ➔ VALYR ➔ Subsystems ➔ Custom Tire Module**

This creates a new script, enters inline naming mode, and generates clean boilerplate with all required interfaces.

---

## 📦 Assembly Definitions (`.asmdef`) Setup

If your project organizes code into Assembly Definition files (`.asmdef`):

1. Open your assembly definition asset in the Inspector.
2. In **Assembly Definition References**, simply add:
   - `VALYR.Core`
   - `VALYR.Runtime` (if referencing `VehicleController`)
3. `VehicleControllerEditor` automatically scans all project assemblies referencing `VALYR.Core` using Unity's native `TypeCache`. Any module you write in your own assemblies (e.g. `Game.Vehicles.asmdef` or standard `Assembly-CSharp`) is immediately discovered in the Inspector without requiring any custom naming prefixes.

---

## 🛡 Best Practices for Multiplayer & Prediction

If you plan to use the **Multiplayer Addon** with client-side prediction and rollback reconciliation:

1. **Keep Subsystems Stateless**:
   - Store **all dynamic physics values** inside `ref VehicleState` or calculate them deterministically per-tick.
   - Never store tick-dependent physics state in private C# fields (e.g. `private float _lastWheelSpeed`). During network rollback, private fields are not rolled back, causing desynchronization.
2. **Zero GC Allocations in Tick Loops**:
   - Avoid creating `new` heap objects, closures, or LINQ queries inside your module methods (`SolveSuspension`, `UpdateSteering`, etc.).
   - Use `ref` and `in` parameter passing.
3. **Use `IPhysicsBody` for All Forces**:
   - Always apply forces via `body.AddForceAtPosition()` or `body.AddForce()` rather than accessing `Rigidbody` directly. In multiplayer mode, `IPhysicsBody` maps to `PredictedRigidbody` which tracks forces across prediction ticks.
