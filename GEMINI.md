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

## 3. Branch Strategy & Core vs Addon Workflow
The repository strictly adheres to a **single-repo, two-branch** architecture:
- **`master`**: Core vehicle simulation framework only (Single-player physics, kinematics, telemetry, cameras, inputs, visuals, presets). Zero networking dependencies (no PurrNet, no PurrDiction).
- **`feat/multiplayer-addon`**: Core + Multiplayer Addon (`VALYR.Multiplayer` assembly, PurrNet, PurrDiction prediction/rollback, kinematic proxy collisions, network entities and lobby).

### Rules for Switching & Editing Branches:
1. **Core Development**:
   - Any work on vehicle physics, wheel contact, suspension, powertrain, steering, assists, cameras, or core visuals **MUST be done on `master`**.
   - Before editing Core, switch to `master` (`git checkout master`).
   - After committing changes to `master`, push to origin (`git push origin master`).

2. **Propagating Core Updates to Multiplayer Addon**:
   - Whenever changes are made to `master`, sync them into `feat/multiplayer-addon` using **rebase**:
     ```bash
     git checkout feat/multiplayer-addon
     git rebase master
     git push origin feat/multiplayer-addon --force-with-lease
     ```
   - Never merge `master` into `feat/multiplayer-addon` with merge commits; always keep the multiplayer commits rebased on top of `master`.

3. **Multiplayer Addon Development**:
   - Any multiplayer-specific logic (network state replication, PurrDiction prediction, network prefabs/lobby, networked doors/lights/interior) **MUST be done on `feat/multiplayer-addon`**.
   - Do NOT introduce multiplayer dependencies into `VALYR.Core`, `VALYR.Modules`, `VALYR.Runtime`, or `VALYR.Editor`.
   - Never commit multiplayer-specific scripts, packages, or scene setups to `master`.

