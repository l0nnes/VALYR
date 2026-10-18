# Brush Tires & Chassis Assists

Tire dynamics in VALYR are modeled using the analytical **Brush Tire Model** (`BrushTireModel`), providing realistic combined slip behavior without the instability of simplified friction tables.

---

## 1. Brush Tire Model Dynamics

The Brush Model calculates combined longitudinal slip (`sx`) and lateral slip (`sy`):

```text
sigma = sqrt(sx² + sy²)
```

### Adhesion vs Sliding Zones
* **Adhesion Region** (`sigma < sigma_crit`):
  Tread bristles deform elastically, generating linear grip proportional to cornering / longitudinal stiffness.
* **Sliding Region** (`sigma >= sigma_crit`):
  Tread elements slide across the surface, governed by dynamic sliding friction and normal load (`Fz`).

### Weight Transfer Reaction
Because normal load `Fz` is supplied dynamically by the suspension and ARB modules, weight transfer under heavy braking (nose dive) or cornering (body roll) naturally increases grip on loaded tires while causing unloaded tires to break traction progressively.

---

## 2. Driver Assists & Chassis Extensions

VALYR provides standalone, modular assists implementing `IChassisAssistModule` or `IWheelAssistModule`:

### Anti-Roll Bar (`AntiRollBarModule`)
Transfers vertical load across axles based on suspension compression differences:

```text
F_arb = k_arb * (deltaL_left - deltaL_right)
```

Controls body roll in sharp turns without stiffening overall ride compliance.

### Anti-Lock Braking System (`AbsModule`)
Monitors longitudinal wheel slip ratio `sx` under braking. When wheel lockup is detected, braking torque is modulated in high-frequency pulses to prevent skid and maintain steerability.

### Traction Control System (`TcsModule`)
Monitors excess drive wheel spin during acceleration. Rapidly attenuates engine drive torque to eliminate uncontrollable wheelspin and maintain optimal forward propulsion.
