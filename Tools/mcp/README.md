# Unity MCP (Advanced)

This project now includes a local Unity MCP setup with two parts:

- Unity-side bridge: `Assets/Editor/UnityMcpHttpBridge.cs`
- MCP server: `Tools/mcp/unity_mcp_server.py`

## What you get

- Read Unity health and active scene
- Read hierarchy tree
- Find GameObjects by name
- Read recent Unity console logs
- Execute Unity menu commands
- Rebuild player animator state machine
- Set component fields by GameObject path

## 1) Unity side

Open the project in Unity. The bridge auto-starts by default.

Manual controls are in menu:

- `Tools/MCP/Start Bridge`
- `Tools/MCP/Stop Bridge`
- `Tools/MCP/Toggle Auto Start`

Bridge endpoint: `http://127.0.0.1:6401`

## 2) MCP server side

Install Python deps:

```bash
pip install -r Tools/mcp/requirements.txt
```

Run MCP server:

```bash
python Tools/mcp/unity_mcp_server.py
```

## 3) Example MCP client config

```json
{
  "mcpServers": {
    "unity": {
      "command": "python",
      "args": [
        "E:/UnityTask/metroidvania/Tools/mcp/unity_mcp_server.py"
      ]
    }
  }
}
```

## Available MCP tools

- `unity_health`
- `unity_hierarchy`
- `unity_find_gameobjects`
- `unity_console_tail`
- `unity_execute_menu`
- `unity_setup_player_animator`
- `unity_set_component_field`
