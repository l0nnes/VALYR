# [1.2.0](https://github.com/l0nnes/VALYR/compare/v1.1.0...v1.2.0) (2026-10-18)

### Core

#### Features
* **camera, input, scene:** add cockpit head look, driving inputs, terrain environment, and update scene profile ([f359d3f](https://github.com/l0nnes/VALYR/commit/f359d3ff3ef2def07de4e3c6ba8159bd5d63e755))
* **camera, visual:** smooth camera tracking, wheel roll angular integration, and door collider linker ([d03fb14](https://github.com/l0nnes/VALYR/commit/d03fb14))

### Multiplayer Addon
* *(Addon features under active development in `feat/multiplayer-addon`)*

---

# [1.1.0](https://github.com/l0nnes/VALYR/compare/v1.0.1...v1.1.0) (2026-10-18)

### Core

#### Features
* **camera, input:** add Cinemachine multi-camera system and migrate vehicle input to New Input System ([34faa4f](https://github.com/l0nnes/VALYR/commit/34faa4fcb7c613fd23a759de698d0c7f2779be9b))

#### Bug Fixes
* **camera:** smooth look orbit damping and proper mouse/stick sensitivity scaling ([1980374](https://github.com/l0nnes/VALYR/commit/19803749d59d1e8ac108cbb3069cd02c8ae696bb))
* **physics:** implement static stiction hold and eliminate low-speed slope drift and wheel slip chatter ([e937259](https://github.com/l0nnes/VALYR/commit/e937259b06b58b94999d667c8438ede29bbeee5e))
* **powertrain:** implement Auto Hold, eliminate reverse brake-fighting bug, and make reverse shift responsive ([73adce6](https://github.com/l0nnes/VALYR/commit/73adce6b135ce242be7ea193ce0e0df89f4b6cb9))

### Multiplayer Addon

#### Features
* **prediction:** client-side prediction and rollback reconciliation using PurrDiction ([2adc492](https://github.com/l0nnes/VALYR/commit/2adc492))
* **replication:** modular network synchronization for vehicle doors, lights, and animated interior controls ([89a33a6](https://github.com/l0nnes/VALYR/commit/89a33a6))
* **lobby:** network lobby manager, spawn points, and connection HUD overlay ([03e4b13](https://github.com/l0nnes/VALYR/commit/03e4b13))

#### Bug Fixes
* **physics:** kinematic proxy and collision impulse transfer to eliminate penetration and shaking ([5182f8f](https://github.com/l0nnes/VALYR/commit/5182f8f))
* **wheels:** replicate per-wheel angular velocities for accurate handbrake lockup and visual roll ([122386c](https://github.com/l0nnes/VALYR/commit/122386c))
* **camera:** resolve camera hijacking, isolate input, and fix remote body sag ([fe7878a](https://github.com/l0nnes/VALYR/commit/fe7878a))
* **serialization:** register VehicleState packers and delta serializers for PurrDiction ([e955f10](https://github.com/l0nnes/VALYR/commit/e955f10))

---

## [1.0.1](https://github.com/l0nnes/VALYR/compare/v1.0.0...v1.0.1) (2026-10-18)

### Core

#### Bug Fixes
* **ci:** remove cache npm requirement from docs workflow ([3bb8bd9](https://github.com/l0nnes/VALYR/commit/3bb8bd92063a28e25ea9363c9eecf52f45adbccb))

### Multiplayer Addon
* *(Addon not yet initialized in v1.0.1)*

---

# 1.0.0 (2026-10-18)

### Core

#### Bug Fixes
* **core:** resolve CS8170 by using get/set indexer and ref-this extension method ([6aaf9ce](https://github.com/l0nnes/VALYR/commit/6aaf9ceb0fad8de6f7e721afc53d492556b187f4))
* **physics, transmission:** analytical friction brake locking, low-speed lateral chatter damping, reverse shift safety, and gear-shift speed scaling ([4a7d5cc](https://github.com/l0nnes/VALYR/commit/4a7d5cccbee36c6608f2d748a9fad83f421e5943))
* **physics:** fix suspension damper sign and mount point force, cap airborne wheel omega, and add VehicleTelemetryHUD ([cb66ea9](https://github.com/l0nnes/VALYR/commit/cb66ea98a2fed8512cdfaf0c04be8b56dec031e7))
* **tires, transmission, engine:** stabilize brush slip dynamics, remove zero-speed force crushing, fix D/R pedal mapping and speed-gated shifting ([9b35e42](https://github.com/l0nnes/VALYR/commit/9b35e428103460caa859feb1ac2b046d697e4acd))

#### Features
* add AI development rules (GEMINI.md) and Unity MCP package ([2d6a05e](https://github.com/l0nnes/VALYR/commit/2d6a05e424991c8a7dd5bc2d8ae7005e2de13e40))
* **ci:** integrate automated standalone asset packaging into semantic release pipeline ([97b57b6](https://github.com/l0nnes/VALYR/commit/97b57b6f62feea53f0dd21ca27fb0c71d9a41b4c))
* **contact:** separate wheel collision detection into IWheelContactModule with multi-ray patch ([b8ee8f1](https://github.com/l0nnes/VALYR/commit/b8ee8f15614a76f22b84dcb4f72b5e2edbfd86bf))
* **core:** implement Stage 1 data structures, math, interfaces and adapters ([8d6593b](https://github.com/l0nnes/VALYR/commit/8d6593b673db4c77aaab7cee8a10e06949f06512))
* **editor:** make vehicle components production-ready, english localized with custom inspector and presets ([d82b399](https://github.com/l0nnes/VALYR/commit/d82b39998e8f849227f61e7f97b0aa04514c317e))
* **gizmo:** add yellow sphere gizmo for wheel center at MaxLength droop position ([04b3470](https://github.com/l0nnes/VALYR/commit/04b347027989cc3bc2e4a26ff23fd398a9516cf4))
* **modules:** implement Stage 2 suspension, powertrain, steering, tire and assist modules with 0 errors ([6378eed](https://github.com/l0nnes/VALYR/commit/6378eed3d3327e35b6f58508315fcd2eba14288a))
* **preset:** configure civilian sedan profile (Solaris 1.6 FWD 6AT), add torque converter launch dynamics, and fix local tire force telemetry ([a04cc6c](https://github.com/l0nnes/VALYR/commit/a04cc6c6679250a0c04a7cd6408b98f3387c291e))
* **runtime:** implement VehicleController, VehicleVisualView, InputSystem reader and configure Erebus ([a94f2f6](https://github.com/l0nnes/VALYR/commit/a94f2f69cb9aab6be2089887d71127282fa359a9))
* **suspension, visual:** add wheel arch ceiling clamp and tune progressive bump stops against bottoming out ([37d7eee](https://github.com/l0nnes/VALYR/commit/37d7eeedf0bbff39c3c10b3cc250b3f3b051c9cf))
* **suspension:** fix equilibrium preload, hard arch protection, and bump stops ([984a0a8](https://github.com/l0nnes/VALYR/commit/984a0a848f810e4466265dd6f72633086d054ee9))
* **suspension:** physical length-based suspension with MinLength arch protection, TargetLength ride height, and MaxLength droop ([41ecbe6](https://github.com/l0nnes/VALYR/commit/41ecbe629fbf7ae2a3c4507889e0d0b24f98a9a2))

### Multiplayer Addon
* *(Addon not yet initialized in v1.0.0)*

---

# Changelog

All notable changes to this project will be documented in this file.
See [Conventional Commits](https://conventionalcommits.org) for commit guidelines.
