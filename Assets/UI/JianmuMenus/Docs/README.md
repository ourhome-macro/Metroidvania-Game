# Jianmu Menus

This module contains the opening title screen and the `Esc` pause menu.

Folders:
- `Runtime/`: auto-boot runtime menu scripts
- `Resources/Generated/`: bitmap assets loaded at runtime
- `Tools/GenerateJianmuMenuArt.ps1`: reproducible menu-art generator

Visual direction:
- Kai-style Chinese title lettering for `建木行者`, then weathered into bark and cracks
- Dark brown / gray wasteland palette
- Cyan and magenta neon glow leaking through wood cracks
- Ink-wash haze layered into pixel-art silhouettes, with drifting ink wisps in runtime UI

Runtime behavior:
- The opening screen appears once per play session when a scene contains `PlayerHealth`
- Clicking `开始游戏` or pressing `Enter` / `Space` dismisses the title overlay
- Pressing `Esc` during gameplay opens a pause panel with `继续游戏` and `退出游戏`

Main scripts:
- `JianmuMenuBootstrap.cs`
- `JianmuMenuController.cs`
- `JianmuMenuArtFactory.cs`
