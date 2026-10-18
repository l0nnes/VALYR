# Presets & Editor Workflow

VALYR empowers game designers to tune, duplicate, and share vehicle setups without modifying code.

---

## 1. ScriptableObject Presets (`VehiclePreset`)

A `VehiclePreset` stores the complete configuration of a vehicle:
* Mass, Center of Mass offset, Wheel Radius
* Ground layer masks
* Assigned subsystems: `WheelContact`, `Suspension`, `Steering`, `Engine`, `Transmission`, `Tires`, and `Assists`.

### Creating a Preset
1. Right-click in the Project window -> **Create -> VALYR -> Vehicle Preset**.
2. Name it (e.g. `Sport_Coupe`, `Offroad_4x4`, `Sedan_Baseline`).
3. Tune properties in the Inspector.

### Applying & Capturing in Inspector
On any GameObject with `VehicleController`:
* **"Apply Preset to Vehicle"**: Overwrites vehicle parameters with the assigned preset.
* **"Save Vehicle to Preset"**: Captures the current live setup on the vehicle back into the ScriptableObject asset.

---

## 2. Drag & Drop Wheel Mounts

VALYR does not require typing numeric coordinate offsets for wheels. 
Simply drag your suspension or wheel anchor transforms directly into the 4 slots:
* `FrontLeft`
* `FrontRight`
* `RearLeft`
* `RearRight`

The controller automatically recalculates local offsets and maintains them relative to the vehicle center of mass.
