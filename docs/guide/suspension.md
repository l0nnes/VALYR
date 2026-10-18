# Suspension Dynamics & Preload

VALYR's suspension module implements an intuitive, physical architecture based on physical damper lengths from the anchor point down to the wheel hub axis.

---

## 1. Physical Damper Lengths

```
[ Suspension Anchor (FL_Suspension) ] --- Top of Wheel Arch
               │
               │  <-- Arch clearance gap: MinLength - WheelRadius >= 2 cm
               ▼
[ MinLength (0.33m) ]  --- Hard Bump Stop Limit (Arch Protection)
               │
               │  <-- Bump Travel (5 cm)
               ▼
[ TargetLength (0.38m) ] --- Equilibrium Ride Height (Nominal Rest Position)
               │
               │  <-- Rebound Travel (17 cm)
               ▼
[ MaxLength (0.55m) ]  --- Droop Limit in Air (Airborne)
```

### Parameters in Inspector

* **`MinLength`** (Default: `0.33m`):
  * Hard bump stop limit at maximum suspension compression.
  * **Zero Arch Clipping Guarantee**: The code strictly enforces `safeMinLength = Mathf.Max(settings.MinLength, wheelRadius)`. Because the anchor is located at the top of the wheel arch, setting `MinLength = 0.33m` with a wheel radius of `0.31m` guarantees a minimum clearance of 2 cm between the top of the tire and the fender arch. The tire **never clips into the hood or fenders**.
* **`TargetLength`** (Default: `0.38m`):
  * Target ride height where the car naturally rests.
  * At this length, the spring force **automatically balances the static corner weight** of the vehicle:
    $$F_{\text{static}} = m_{\text{corner}} \cdot g$$
  * When stationary, suspension compression is exactly $0$, keeping the vehicle perfectly level without sagging into the bump stops.
* **`MaxLength`** (Default: `0.55m`):
  * Maximum extended damper travel when the wheel leaves the ground (airborne / droop).

---

## 2. Spring & Damper Mechanics

### Hooke's Law with Static Preload
$$F_{\text{spring}} = F_{\text{static}} + k \cdot (\text{TargetLength} - L)$$

Where $k$ is `SpringStiffness` (e.g. 32000 N/m front, 28000 N/m rear).

### Asymmetric Shock Absorbers
Automotive dampers provide different resistance during bump (compression) vs rebound (extension):
* `DamperCompressionRate` (e.g. 3200 Ns/m): Controls body dive and landing impact absorption.
* `DamperReboundRate` (e.g. 4500 Ns/m): Typically 1.3 - 1.5x compression rate to eliminate body oscillation (pogo-stick bouncing).

### Progressive Polyurethane Bump Stop
Approaching `MinLength` (within `BumpStopBuffer = 0.03m`), a progressive non-linear cushion engages:
$$F_{\text{bump}} = k_{\text{bump}} \cdot p \cdot (1 + 3 \alpha)$$
* Polyurethane dampening dissipates kinetic energy to prevent violent bounce-backs.
* If compressed to `safeMinLength`, downward velocity is arrested:
  $$F_{\text{arrest}} = \frac{m_{\text{corner}} \cdot \max(0, v_{\text{down}})}{\Delta t}$$

---

## 3. Scene View Gizmos

When the vehicle is selected in Unity:
* ⚪ **White Sphere**: Suspension Anchor mount.
* 🔴 **Red Sphere**: Wheel center at `MinLength` (Bump stop limit).
* 🔵 **Cyan Line**: Bump travel.
* 🟢 **Green Wire Sphere**: Wheel center at `TargetLength` (Nominal ride height).
* 🟡 **Yellow Line & Yellow Wire Sphere**: Rebound travel and wheel center at `MaxLength` (Droop).
* 🟡 *(Faint Yellow Line)*: Lower tire perimeter reach at full droop.
