# Visual Presentation & Developer Telemetry

## 1. Visual Presentation Layer (`VehicleVisualView`)

Physical simulation in Unity runs in `FixedUpdate` (typically 50–60 Hz), while rendering runs in `Update` (variable frame rate: 60–240+ FPS). 

`VehicleVisualView` decouples rendering from physics:

### Ground Contact Patch Alignment
* When touching the ground, the wheel hub is placed directly above the contact patch:
  ```text
  P_hub = P_contact + N_contact * WheelRadius
  ```
* **Zero Ground Penetration**: The bottom of the tire sits 100% flush on the road surface with zero sinking or hovering.
* **Zero Arch Clipping**: The hub is clamped so it physically cannot rise closer to the anchor than `MinLength`.

### Motion Smoothing & Rotation
* Vertical motion smoothing eliminates jitter when crossing jagged collision triangles.
* Steer rotation is applied around the local Y axis, and rolling rotation is accumulated around the local X axis based on `WheelState.RotationAngle`.

---

## 2. Developer Telemetry HUD (`VehicleTelemetryHUD`)

Press **[F1]** during Play Mode to toggle the real-time HUD:
* **Engine & Powertrain**: Speed (km/h), Engine RPM, Active Gear, Clutch percentage.
* **Driver Input**: Throttle, Brake, Steer, Handbrake bars.
* **4-Wheel Card Grid**:
  * Grounded / Airborne status indicator
  * Damper current length vs target ride height (cm)
  * Damper velocity (m/s) and Normal Load `Fz` (N)
  * Wheel RPM, Wheel Speed, and Angular velocity `omega`
  * Longitudinal Slip Ratio (`sx`) and Lateral Slip Angle (`sy`)
  * Longitudinal Traction (`Fx`) and Lateral Cornering Force (`Fy`) in Newtons
  * Skid intensity percentage for audio & smoke VFX
