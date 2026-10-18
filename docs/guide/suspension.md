# Suspension Dynamics & Preload

VALYR's suspension module implements an intuitive, physical architecture based on physical damper lengths from the anchor point down to the wheel hub axis.

---

## 1. Physical Damper Lengths

```text
[ Suspension Anchor ] --- Mounted at Top of Wheel Arch / Chassis
          │
          │  <-- Arch Clearance: MinLength >= WheelRadius (Zero Clipping)
          ▼
    [ MinLength ]     --- Hard Bump Stop Limit (Arch Ceiling Protection)
          │
          │  <-- Bump Travel (Compression under load / impact)
          ▼
   [ TargetLength ]   --- Equilibrium Ride Height (Nominal Rest Position)
          │
          │  <-- Rebound Travel (Extension / Droop)
          ▼
    [ MaxLength ]     --- Maximum Extended Limit (Airborne / Droop)
```

### Parameters in Inspector

* **`MinLength`**:
  * Hard bump stop limit at maximum suspension compression.
  * **Zero Arch Clipping Guarantee**: The code strictly enforces `safeMinLength = Mathf.Max(settings.MinLength, wheelRadius)`. Because the anchor is located at the top of the wheel arch, keeping `MinLength >= WheelRadius` mathematically guarantees the tire never clips into the fender arch or engine bay.
* **`TargetLength`**:
  * Desired ride height where the vehicle naturally rests at equilibrium.
  * At this length, the spring force **automatically balances the static corner weight** of the vehicle:
    ```text
    F_static = m_corner * g
    ```
  * When stationary, suspension compression offset is exactly zero, keeping the vehicle perfectly level without sagging into the bump stops.
* **`MaxLength`**:
  * Maximum extended damper travel when the wheel leaves the ground (droop limit).

---

## 2. Spring & Damper Mechanics

### Hooke's Law with Static Preload

```text
F_spring = F_static + SpringStiffness * (TargetLength - L)
```

### Asymmetric Shock Absorbers
Automotive dampers provide different resistance during bump (compression) vs rebound (extension):
* **`DamperCompressionRate`**: Controls body dive and landing impact absorption.
* **`DamperReboundRate`**: Typically 1.3 – 1.5× higher than compression rate to eliminate chassis oscillation (pogo-stick bouncing).

### Progressive Polyurethane Bump Stop
Approaching `MinLength` (within `BumpStopBuffer`), a progressive non-linear cushion engages:

```text
F_bump = k_bump * penetration * (1 + 3 * progressiveFactor)
```

* Polyurethane dampening dissipates kinetic energy to prevent violent bounce-backs.
* If compressed to `safeMinLength`, downward velocity is arrested:

```text
F_arrest = (m_corner * max(0, v_down)) / dt
```

---

## 3. Scene View Gizmos

When the vehicle is selected in Unity:
* ⚪ **White Sphere**: Suspension Anchor mount.
* 🔴 **Red Sphere**: Wheel center at `MinLength` (Bump stop limit).
* 🔵 **Cyan Line**: Bump travel.
* 🟢 **Green Wire Sphere**: Wheel center at `TargetLength` (Nominal ride height).
* 🟡 **Yellow Line & Yellow Wire Sphere**: Rebound travel and wheel center at `MaxLength` (Droop).
* 🟡 *(Faint Yellow Line)*: Lower tire perimeter reach at full droop.
