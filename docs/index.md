---
layout: home

hero:
  name: "VALYR"
  text: "Modular Vehicle Simulation Framework"
  tagline: High-performance, physically accurate simcade/arcade vehicle physics for Unity 6000+.
  actions:
    - theme: brand
      text: Quick Start Guide
      link: /guide/quick-start
    - theme: alt
      text: Architecture
      link: /guide/architecture
    - theme: alt
      text: GitHub Repository
      link: https://github.com/l0nnes/VALYR

features:
  - icon: 🧩
    title: 100% Pure Modular Architecture
    details: Built with decoupled C# interfaces using Unity's modern [SerializeReference]. Hot-swap steering, contact detection, suspension, tires, and powertrain dynamically.
  - icon: 🎯
    title: 5-Point Multi-Ray Contact Patch
    details: Eliminates standard needle-ray glitches. Sweeps tire width and forward profile to detect curbs, steps, ruts, and road banking smoothly before the wheel center hits them.
  - icon: 🛡️
    title: Equilibrium Suspension & Arch Guard
    details: Physics-based ride height centered at static equilibrium with zero sagging. Hard MinLength bump stop prevents wheels from ever clipping fenders or chassis from bottoming out.
  - icon: 🏎️
    title: Brush Tire Friction Dynamics
    details: Combined longitudinal slip ratio and lateral slip angle solver based on analytical Brush Tire mechanics, complete with progressive falloff and weight transfer response.
  - icon: 🎛️
    title: ScriptableObject Preset Ecosystem
    details: Save and share complete vehicle setups (Sedan, Sport, Offroad) in one click. Custom Inspector with categorized foldouts and zero clutter.
  - icon: 👁️
    title: Silky Smooth Visual Layer & Telemetry
    details: Decoupled VehicleVisualView with contact-aligned wheel hubs, rotation smoothing, and a comprehensive real-time developer HUD.
---

## System Overview

```mermaid
flowchart TD
    In[Driver / AI Input] --> Steer[1. Steering Module]
    Steer --> Contact[2. Wheel Contact Detection]
    subgraph ContactPatch [Contact Scanning]
        Contact --> M1[Single Ray]
        Contact --> M2[5-Point Patch]
        Contact --> M3[Radial Sector]
        Contact --> M4[SphereCast]
    end
    ContactPatch --> Susp[3. Physical Suspension Solver]
    subgraph Dynamics [Forces & Assists]
        Susp --> ARB[Anti-Roll Bar]
        ARB --> Engine[Engine & Transmission]
        Engine --> Assists[ABS & TCS]
        Assists --> Tires[Brush Tire Dynamics]
    end
    Tires --> UnityRB[Unity Rigidbody Physics]
    UnityRB --> VisView[VehicleVisualView & Telemetry]
```
