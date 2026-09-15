# RPG

A Unity 2D project, currently set up from Unity's default 2D template as a starting point for an RPG.

## Requirements

- Unity **6000.6.0f1** (Unity 6) — install via Unity Hub. Using a different version may prompt Unity to reimport assets or upgrade the project.

## Getting started

1. Open Unity Hub.
2. Click **Add** and select this project's folder.
3. Open the project with Unity `6000.6.0f1`.
4. Open the main scene at [Assets/Scenes/SampleScene.unity](Assets/Scenes/SampleScene.unity).

## Project structure

```
Assets/
  Scenes/    Game scenes (currently the default SampleScene)
  Script/    C# scripts
  Settings/  URP render pipeline, input actions, and scene template assets
  Welcome/   Assets from Unity's 2D template welcome screen
Packages/    Package manifest (manifest.json)
ProjectSettings/  Unity project configuration
```

## Rendering & input

- **Render pipeline:** Universal Render Pipeline (URP) 2D
- **Input:** Unity's new Input System, configured in [Assets/Settings/InputSystem_Actions.inputactions](Assets/Settings/InputSystem_Actions.inputactions)

## Notable packages

See [Packages/manifest.json](Packages/manifest.json) for the full list. Highlights:

- `com.unity.render-pipelines.universal` — URP
- `com.unity.inputsystem` — Input System
- `com.unity.2d.animation`, `com.unity.2d.tilemap`, `com.unity.2d.tilemap.extras`, `com.unity.2d.spriteshape` — 2D tooling
- `com.unity.2d.aseprite`, `com.unity.2d.psdimporter` — art pipeline importers
- `com.unity.visualscripting`, `com.unity.timeline` — scripting/cinematics

## Status

This is an early-stage project — the default template scene and a placeholder script are the only content so far. No gameplay systems have been implemented yet.
