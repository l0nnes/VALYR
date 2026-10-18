# Quick Start Guide

Welcome to **VALYR**. This guide walks you through setting up a complete simulated vehicle from scratch in Unity.

---

## 1. Prerequisites & Installation

* **Unity Version**: Unity 6000.0+ (Unity 6+) recommended.
* **Input System**: `com.unity.inputsystem` enabled.
* **Dependencies**: Built on standard Unity physics (PhysX).

### Adding to your project
Clone or add as a git submodule to your Unity project's `Assets` or `Packages` folder:

```bash
git clone https://github.com/l0nnes/VALYR.git Assets/VALYR
```

---

## 2. Vehicle Hierarchy Setup

A standard VALYR vehicle hierarchy consists of:

```text
Erebus (GameObject with Rigidbody, VehicleController, VehicleInputReader, VehicleVisualView)
 ├── Meshes/ (Visual model, chassis, lights)
 ├── Wheels/
 │    └── Meshes/
 │         ├── FrontLeftWheel
 │         ├── FrontRightWheel
 │         ├── RearLeftWheel
 │         └── RearRightWheel
 ├── Suspension/ (Suspension anchor transforms located at top of arch)
 │    ├── FL_Suspension
 │    ├── FR_Suspension
 │    ├── RL_Suspension
 │    └── RR_Suspension
 └── Colliders/ (Chassis BoxCollider or Compound Colliders)
```

> [!IMPORTANT]
> **Suspension Anchors (`FL_Suspension`, etc.)**:
> Place these Transform objects directly at the **ceiling of the wheel arches** inside your vehicle body.
> All suspension travel (`MinLength`, `TargetLength`, `MaxLength`) is measured physically downward from these anchors to the wheel hub center!

---

## 3. Configuring the Vehicle Controller

1. Select your vehicle root GameObject in the Scene hierarchy.
2. In the Inspector, you will see the customized **VALYR Vehicle Controller**:
   * **Vehicle Preset**: Assign `Assets/Settings/Sedan_Baseline.asset` and click **"Apply Preset to Vehicle"**.
   * **Wheel Mounts**: Drag your 4 suspension anchor transforms (`FL_Suspension`, `FR_Suspension`, `RL_Suspension`, `RR_Suspension`) into the corresponding slots. Or click **Auto Find**.
   * **Chassis & Mass**:
     * `WheelRadius`: `0.31m`
     * `ChassisMass`: `1180 kg`
     * `CenterOfMass`: `(0, -0.22, 0.12)`
     * `GroundLayers`: Select your ground/road layers (excludes vehicle colliders).

---

## 4. Visual View & Developer Telemetry

Add or inspect [`VehicleVisualView`](file:///E:/Unity/Projects/VALYR/Assets/Scripts/VALYR/Runtime/Visual/VehicleVisualView.cs):
* Assign the 4 visual wheel transforms and 4 suspension anchors.
* The visual layer automatically keeps the tire contact patch flush with the road, applies steering rotation around Y, roll rotation around X, and guarantees the wheel **never penetrates the fender arch**!

Press **Play** in Unity, and press **[F1]** to toggle the real-time telemetry HUD displaying:
* Speed, Engine RPM, Gear, Clutch engagement
* Per-wheel damper lengths, loads ($F_z$), slip ratios, slip angles, and skid intensity.
