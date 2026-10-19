# Multiplayer Addon Guide

The **VALYR Multiplayer Addon** is a modular networking layer built on top of [PurrNet](https://purrnet.dev) and [PurrDiction](https://github.com/PurrNet/PurrDiction) for zero-latency, server-authoritative client-side prediction and rollback physics.

---

## 📦 Package Architecture

VALYR uses a unified monorepo architecture where the multiplayer layer is decoupled into its own standalone Unity package (`com.valyr.multiplayer`):

```text
Packages/
  ├── com.valyr.core          <-- Pure vehicle simulation (zero networking dependencies)
  └── com.valyr.multiplayer   <-- Network replication, prediction & rollback (PurrNet & PurrDiction)
```

### Installation via UPM

To install the Multiplayer Addon in your project:
1. In the Unity Package Manager, click **+ ➔ Add package from git URL...**
2. Enter:
   ```text
   https://github.com/l0nnes/VALYR.git#upm/multiplayer
   ```
3. Unity will resolve the package along with its dependency on `com.valyr.core`. Ensure you also have [PurrNet](https://purrnet.dev) and [PurrDiction](https://github.com/PurrNet/PurrDiction) installed in your project.

---

## 🧩 Assembly Separation

VALYR enforces strict assembly definitions to ensure zero leakage of multiplayer dependencies into the single-player Core:

```text
VALYR.Core             <-- Pure C# structs, interfaces, math
   ▲
   ├── VALYR.Modules   <-- Suspension, tires, powertrain, assists
   │      ▲
   │      └── VALYR.Runtime     <-- VehicleController, VisualView, Cameras, Input
   │             ▲
   │             └── VALYR.Multiplayer  <-- NetworkVehicle, PredictedVehicle, Lobby (PurrNet)
   └── VALYR.Editor    <-- Custom inspectors, presets, telemetry editors
```

* **`VALYR.Runtime`**: Zero network references. Runs completely standalone in single-player or offline builds.
* **`VALYR.Multiplayer`**: References `VALYR.Core`, `VALYR.Modules`, `VALYR.Runtime`, `PurrNet.Runtime`, and `PurrNet.Prediction`.

---

## 🚀 Key Multiplayer Systems

### 1. Client-Side Prediction (`PredictedVehicle`)
* Driven by **PurrDiction** tick cycle (`PerformTick`, `CopyState`, `SetState`).
* Local client inputs (`VehicleInputState`) are sampled at fixed network tick rates (e.g., 50 Hz / 60 Hz) and sent to the server.
* The local simulation predicts vehicle dynamics ahead of network round-trips.
* Upon receiving server reconciliation packets, PurrDiction silently rolls back and replays inputs to eliminate visible latency.

### 2. Physical Kinematic Proxy (`PredictedRigidbodyPhysicsBody`)
* Remote peer vehicles utilize a **Kinematic Proxy** physics model to prevent PhysX penetration bugs and multi-body twitching.
* Contact impulses between dynamic vehicles are transferred via `VehicleRigidbodySettings` impulse redistribution, giving authentic collision impact feedback without jitter.

### 3. Modular Vehicle Addons
* **`NetworkVehicleDoors`**: Replicates driver and passenger door openings, tailgate latches, and animations across network observers.
* **`NetworkVehicleLights`**: Synchronizes low beams, high beams, turn indicators, hazards, and brake lights with delta compression.
* **`NetworkVehicleInterior`**: Drives steering wheel rotation, pedal actuation, and gear shift lever animations on all remote observation cameras.

### 4. Zero-Latency Visual Smoothing
* **Cinemachine ManualUpdate**: `VehicleCameraController` invokes `CinemachineBrain.ManualUpdate()` directly within `LateUpdate` after vehicle physics coordinates resolve, eliminating 1-frame camera lag.
* **Continuous Wheel Roll**: Visual wheel angles integrate angular velocities per-frame to eliminate discrete 50 Hz physics stepping stutter.
