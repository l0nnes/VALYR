# Powertrain & Steering Subsystems

## 1. Engine & Transmission

VALYR decouples engine generation from gear reduction and differential distribution:

### Arcade / Simcade Engine (`ArcadeEngineModule`)
* Configurable RPM bounds (`IdleRpm`, `RevLimiterRpm`, `MaxRpm`).
* Non-linear torque curve via `AnimationCurve`.
* Flywheel inertia simulation for natural RPM revving and rev drops.
* Natural engine braking when throttle is released.

### Automatic Transmission (`SimpleTransmissionModule`)
* Multi-gear setup (default: 5 forward gears + 1 reverse).
* Configurable gear ratios and final drive ratio.
* Torque converter multiplication at low speeds / launch.
* Smooth auto-shift thresholds based on RPM and load.

---

## 2. Steering Kinematics (`DirectSteeringModule`)

* **Speed-Sensitive Attenuation**: Smoothly reduces max steering angle from low-speed maneuvering (e.g. 36°) to high-speed stability (e.g. 12° at high speeds).
* **Ackermann Steering Geometry**: Angles the inner front wheel more sharply than the outer front wheel to prevent scrubbing around corners.
* **Rate-Limited Response**: Filters sudden input spikes to model steering rack damping.
