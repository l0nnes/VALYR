# Unity & Unity MCP Development Rules

## 1. Direct Unity MCP Usage
- When interacting with Unity (inspecting or modifying the scene, GameObjects, components, console, hierarchy, assets, profiler, etc.), **always use Unity MCP tools directly** via `call_mcp_tool` (`ServerName: "unityMCP"`) rather than executing ad-hoc external Python scripts or terminal workarounds.
- Key native tools available in `unityMCP`:
  - `manage_gameobject`: Creating, modifying, deleting, duplicating GameObjects and primitives.
  - `manage_components`: Adding, removing, and configuring component properties.
  - `manage_scene`: Managing active scenes, saving, and hierarchy operations.
  - `find_gameobjects`: Searching scene objects by name, tag, layer, or component.
  - `read_console`: Reading compiler errors, warnings, and runtime logs directly from the Unity Editor console.
  - `refresh_unity`: Triggering AssetDatabase refresh and requesting script compilation.
  - `execute_code`: Running arbitrary C# code inside the Unity Editor when necessary.

## 2. Script Creation and Modification Workflow
When creating or editing C# scripts for Unity:
1. **Code Creation / Editing**: Implement the requested changes or new scripts maintaining clean architecture and project conventions.
2. **Roslyn Script Validation via Unity MCP**: Immediately after writing or modifying scripts, perform validation through Unity MCP:
   - Call `validate_script`:
     ```json
     {
       "uri": "Assets/Path/To/Script.cs",
       "level": "standard",
       "include_diagnostics": true
     }
     ```
   - Request compilation and domain reload via `refresh_unity`:
     ```json
     {
       "compile": "request",
       "scope": "scripts",
       "mode": "if_dirty",
       "wait_for_ready": true
     }
     ```
   - Verify compiler diagnostics from Roslyn/Unity by calling `read_console`:
     ```json
     {
       "types": ["error"]
     }
     ```
3. **Zero Errors Guarantee**: If any diagnostics or compilation errors are found, fix them immediately and re-validate before reporting completion.
