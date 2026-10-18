# Brush Tires & Chassis Assists

Tire dynamics in VALYR are modeled using the analytical **Brush Tire Model** ([`BrushTireModel.cs`](file:///E:/Unity/Projects/VALYR/Assets/Scripts/VALYR/Core/Math/BrushTireModel.cs)), providing realistic combined slip behavior without the instability of simplified friction tables.

---

## 1. Brush Tire Model Dynamics

The Brush Model calculates combined longitudinal slip ($s_x$) and lateral slip ($s_y$):

$$\sigma = \sqrt{s_x^2 + s_y^2}$$

### Adhesion vs Sliding Zones
* **Adhesion Region** ($\sigma < \sigma_{\text{crit}}$):
  Tread bristles deform elastically, generating linear grip proportional to stiffness ($C_x$, $C_y$).
* **Sliding Region** ($\sigma \ge \sigma_{\text{crit}}$):
  Tread elements slide across asphalt, governed by dynamic friction ($\mu_{\text{slide}}$) and normal load ($F_z$).

### Weight Transfer Reaction
Because normal load $F_z$ is supplied dynamically by the suspension and ARB modules, weight transfer under heavy braking (nose dive) or cornering (body roll) naturally increases grip on loaded tires while causing unloaded tires to break traction progressively.

---

## 2. Driver Assists & Chassis Extensions

VALYR provides standalone, modular assists implementing `IChassisAssistModule` or `IWheelAssistModule`:

### Anti-Roll Bar (`AntiRollBarModule`)
Transfers vertical load across axles based on suspension compression differences:
$$F_{\text{arb}} = k_{\text{arb}} \cdot (\Delta L_{\text{left}} - \Delta L_{\text{right}})$$
Controls body roll in sharp turns without stiffening overall ride compliance.

### Anti-Lock Braking System (`AbsModule`)
Monitors longitudinal wheel slip ratio $s_x$ under braking. When wheel lockup is detected ($s_x < -0.22$), braking torque is modulated in high-frequency pulses to maintain steerability.

### Traction Control System (`TcsModule`)
Monitors excess drive wheel spin during acceleration ($s_x > 0.18$). Rapidly cuts or attenuates engine drive torque to eliminate uncontrollable wheelspin and maintain optimal forward propulsion.
