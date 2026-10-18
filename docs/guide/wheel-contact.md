# Wheel Contact & Collision Detection

In vehicle simulation, standard single vertical raycasts fail when encountering steps, curbs, or deep ruts. The wheel drives inside the obstacle until the wheel center reaches the edge, causing severe clipping followed by sudden launch physics.

VALYR provides a dedicated, decoupled **`WheelContactModule`** solving this challenge with multiple collision scanning algorithms.

---

## 1. Supported Contact Modes

### 1. `FivePointPatch` (Recommended Default)
Catches curbs, steps, and lateral road banking with minimal performance cost ($5 \times 4 = 20$ rays per frame):
* **Center Ray** $(x=0, y=0)$: Base road depth directly beneath the hub.
* **Forward Bumper Ray** $(x=+0.55 R, y=0)$: Scans ahead for curbs, ramps, and road obstacles. Starts lifting the wheel smoothly *before* the center arrives.
* **Rear Ray** $(x=-0.40 R, y=0)$: Catches steps when driving in reverse.
* **Left Shoulder** $(y = -\text{Width} \cdot 0.45)$: Detects ruts and curbs on the left side of the tire.
* **Right Shoulder** $(y = +\text{Width} \cdot 0.45)$: Detects ruts and curbs on the right side of the tire.

### 2. `RadialSector`
Full radial sector arc around the lower tire circumference $\times$ tire width:
* Configurable `SectorAngle` (e.g. $60^\circ - 90^\circ$).
* Configurable `RadialResolution` (3 to 9 rays) and `WidthResolution` (1 to 5 rays).
* Weighted depth averaging for smooth normal interpolation across complex terrain.

### 3. `SphereCast`
Continuous volumetric sphere sweep with tire radius along the suspension travel axis. Ideal for rounded obstacles, boulders, and offroad rocks.

### 4. `SingleRay`
Single vertical raycast for ultra-lightweight background AI traffic.

---

## 2. Mathematical Profile Clearance

For any sample ray $k$ offset longitudinally by $x_k$ in the wheel's local frame:
$$\Delta z_k = R - \sqrt{\max(0, R^2 - x_k^2)}$$

If the ray hits an obstacle at distance $d_k$ from the mount plane:
$$L_k = d_k - \sqrt{\max(0, R^2 - x_k^2)}$$

Since a tire is a physical body, the effective damper clearance is dictated by the highest obstacle under the wheel footprint:
$$L_{\text{effective}} = \min_k (L_k)$$

The contact point and contact normal are calculated as a compression-weighted blend:
$$w_k = \max(0.001, (D_{\text{max}} - d_k)^2)$$
$$\mathbf{N}_{\text{contact}} = \frac{\sum w_k \mathbf{n}_k}{\sum w_k}$$

---

## 3. Scene View Gizmos

When the vehicle is selected in the Editor Scene view, `WheelContactModule` renders the contact rays in real-time:
* 🟢 **Green Sphere & Ray**: Ground contact point and surface normal vector.
* 🔵 **Cyan Rays**: Active 5-point patch footprints showing exact road sampling points.
