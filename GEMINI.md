# Unity & Unity MCP Development Rules

## 1. Direct Unity MCP Usage
- When interacting with Unity (inspecting or modifying the scene, GameObjects, components, console, hierarchy, assets, profiler, etc.), **always use Unity MCP tools directly** via `call_mcp_tool` (`ServerName: "unityMCP"`) rather than executing ad-hoc external Python scripts or terminal workarounds.
- Key native tools available in `unityMCP`:
  - `manage_gameobject`: Creating, modifying, deleting, duplicating GameObjects and primitives.
  - `manage_components`: Adding, removing, and configuring component properties.
  - `manage_scene`: Managing active scenes, saving, and hierarchy operations.
  - `find_gameobjects`: Searching scene objects by name, tag, layer, or component.
  - `read_console`: Reading compiler errors, warnings, and runtime logs directly from the Unity Editor console.
  - `refresh_unity`: Triggering AssetDatabase refresh, requesting script compilation, and waiting for domain reload.
  - `execute_code`: Running arbitrary C# code inside the Unity Editor when necessary.

## 2. Script Creation and Modification Workflow
When creating or editing C# scripts for Unity, follow this strict sequence:

1. **Complete All Code Changes First**:
   - Write or modify all required C# scripts completely before starting compilation or validation steps.

2. **Trigger Recompilation & Wait for Readiness**:
   - Once all code is written, initiate compilation and wait for Unity to complete domain reload by calling `refresh_unity` via `call_mcp_tool`:
     ```json
     {
       "compile": "request",
       "scope": "scripts",
       "mode": "if_dirty",
       "wait_for_ready": true
     }
     ```
   - Must wait until Unity finishes compiling and reports readiness (`wait_for_ready: true`).

3. **Check for Compilation Errors**:
   - Only after Unity finishes recompilation, check for compiler errors in the console by calling `read_console`:
     ```json
     {
       "types": ["error"]
     }
     ```
   - Optionally validate specific scripts using `validate_script` (`level: "standard"`, `include_diagnostics: true`) if detailed Roslyn diagnostics are required.

4. **Zero Errors Guarantee**:
   - If any compilation errors or issues are detected, fix them in the code and repeat steps 2 and 3 (compile -> wait -> check) until 0 errors remain before reporting completion to the user.

## 3. Monorepo & UPM Package Architecture
The repository uses a **unified monorepo architecture** on `master` with decoupled Unity Package Manager (UPM) packages:

### Package Structure:
- **`Packages/com.valyr.core`**:
  - Core vehicle simulation framework: physics, kinematics, drivetrain, suspension, steering, assists, camera, input reader, telemetry, and editor inspectors.
  - Zero networking dependencies (does not require PurrNet, PurrDiction, Steamworks, etc.).
- **`Packages/com.valyr.multiplayer`**:
  - Multiplayer replication, client-side prediction, and rollback addon.
  - Depends only on `com.valyr.core`, `dev.purrnet.purrnet`, and `dev.purrnet.purrdiction`.
  - Free from game-specific lobby or Steamworks dependencies.
- **`Assets/`**:
  - Contains test tracks, demo scenes (`SampleScene`, `MenuScene`), vehicle models, audio, and demo utilities (`Assets/Scripts/Demo`).
  - Serves as the testbed and sandbox for development and CI validation.

### Development & Release Workflow:
1. **Single Source of Truth (`master`)**:
   - All development takes place on `master` (or short-lived feature PRs merged into `master`).
   - Changes to Core APIs and Multiplayer networking can be updated and tested atomically in the same commit.
   - No cross-branch rebasing or synchronization conflicts.
2. **Automated CI/CD Distribution**:
   - GitHub Actions validates package integrity, documentation, and headless compilation.
   - Semantic Release generates SemVer tags and releases both UPM `.tgz` tarballs (`com.valyr.core-vX.Y.Z.tgz` and `com.valyr.multiplayer-vX.Y.Z.tgz`).
   - Automatically maintains pure UPM distribution branches via subtree split:
     - `https://github.com/l0nnes/VALYR.git#upm/core`
     - `https://github.com/l0nnes/VALYR.git#upm/multiplayer`
