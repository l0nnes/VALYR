# Multiplayer Addon Guide

The **VALYR Multiplayer Addon** is a modular networking layer built on top of [PurrNet](https://purrnet.dev) and [PurrDiction](https://github.com/PurrNet/PurrDiction) for zero-latency, server-authoritative client-side prediction and rollback physics.

---

## 🌿 Repository & Branch Strategy

The project maintains a clean **single-repository, two-branch** architecture:

```mermaid
gitGraph
   commit id: "Core: Suspension & Tires"
   commit id: "Core: Powertrain & Camera"
   branch feat/multiplayer-addon
   checkout feat/multiplayer-addon
   commit id: "Multiplayer: PurrNet Lobby"
   commit id: "Multiplayer: PurrDiction Prediction"
   checkout master
   commit id: "Core: Wheel Visual Smoothing"
   checkout feat/multiplayer-addon
   merge master id: "Rebase on master"
   commit id: "Multiplayer: Kinematic Proxy"
```

| Branch | Contents | Dependencies | Responsibility |
|---|---|---|---|
| `master` | **VALYR Core** | Unity Input System, Cinemachine | Standalone single-player vehicle simulation, physics solvers, cameras, audio/visual views, and telemetry. |
| `feat/multiplayer-addon` | **Core + Multiplayer Addon** | Core + PurrNet, PurrDiction, Multiplayer Playmode | Full network replication, client-side prediction, rollback reconciliation, lobby management, and remote proxy collision. |

---

## 🔄 Rebase Workflow

Whenever changes, features, or fixes are added to the **Core** on `master`, sync them into the multiplayer addon branch using Git rebase:

```bash
# 1. Ensure master is up-to-date
git checkout master
git pull origin master

# 2. Switch to the multiplayer addon branch
git checkout feat/multiplayer-addon

# 3. Rebase your multiplayer commits on top of the latest Core
git rebase master

# 4. Push the rebased branch to remote
git push origin feat/multiplayer-addon --force-with-lease
```

> [!TIP]
> **Clean History**: By keeping Core modifications strictly on `master` and rebasing `feat/multiplayer-addon`, all multiplayer commits remain isolated at the tip of the git graph. When the addon is eventually separated into an independent UPM package in the future, the migration will be straightforward.

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
