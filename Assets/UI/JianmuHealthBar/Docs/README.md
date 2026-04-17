# Jianmu Health Bar

This module lives under `Assets/UI/JianmuHealthBar/` and is intentionally self-contained.

Design direction:
- Top-left runtime HUD for a 2D pixel action game
- The life bar is treated as a sacred Jianmu branch instead of a generic red slider
- HP loss causes three visual changes at the same time:
  - the inner "sap" fill shrinks
  - leaf clusters dry out from right to left
  - the Jianmu icon swaps to progressively more withered stages

Files:
- `Runtime/JianmuHealthBarBootstrap.cs`: creates the UI automatically after scene load
- `Runtime/JianmuHealthBarController.cs`: builds the Canvas and reacts to HP changes
- `Runtime/JianmuHealthBarArtFactory.cs`: generates the pixel-art sprites at runtime

Integration notes:
- The controller listens to `GameEvents.OnHpChanged`
- On scene changes it rebinds to `PlayerHealth`
- No scene YAML or prefab edits are required

Fast tuning points:
- `MaxFillWidth` in `JianmuHealthBarController.cs`
- `referenceResolution` in `JianmuHealthBarController.cs`
- palette values in `JianmuHealthBarController.cs`
- generated bark / leaf colors inside `JianmuHealthBarArtFactory.cs`
