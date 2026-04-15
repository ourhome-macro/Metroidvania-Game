import json
import urllib.parse
import urllib.request
from typing import Any, Dict

from mcp.server.fastmcp import FastMCP


BRIDGE_BASE = "http://127.0.0.1:6401"
mcp = FastMCP("unity-mcp")


def _get(path: str) -> Dict[str, Any]:
    req = urllib.request.Request(f"{BRIDGE_BASE}{path}", method="GET")
    with urllib.request.urlopen(req, timeout=10) as resp:
        return json.loads(resp.read().decode("utf-8"))


def _post(path: str, payload: Dict[str, Any]) -> Dict[str, Any]:
    data = json.dumps(payload).encode("utf-8")
    req = urllib.request.Request(
        f"{BRIDGE_BASE}{path}",
        data=data,
        method="POST",
        headers={"Content-Type": "application/json"},
    )
    with urllib.request.urlopen(req, timeout=20) as resp:
        return json.loads(resp.read().decode("utf-8"))


@mcp.tool()
def unity_health() -> Dict[str, Any]:
    """Check Unity bridge status and active scene."""
    return _get("/health")


@mcp.tool()
def unity_hierarchy() -> Dict[str, Any]:
    """Get active scene hierarchy."""
    return _get("/hierarchy")


@mcp.tool()
def unity_find_gameobjects(name: str) -> Dict[str, Any]:
    """Find GameObjects by partial name in active scene."""
    query = urllib.parse.urlencode({"name": name})
    return _get(f"/find?{query}")


@mcp.tool()
def unity_console_tail(count: int = 50) -> Dict[str, Any]:
    """Read recent Unity console logs."""
    count = max(1, min(count, 300))
    return _get(f"/console?count={count}")


@mcp.tool()
def unity_execute_menu(menu_item: str) -> Dict[str, Any]:
    """Execute a Unity editor menu item, e.g. Tools/Animation/Generate Clips And Controllers."""
    return _post("/execute-menu", {"menuItem": menu_item})


@mcp.tool()
def unity_setup_player_animator() -> Dict[str, Any]:
    """Rebuild player animator state machine."""
    return _post("/setup-player-animator", {})


@mcp.tool()
def unity_set_component_field(path: str, component: str, field: str, value: str) -> Dict[str, Any]:
    """Set a component field on a scene GameObject.

    path format example: player/attackPoint
    value examples:
    - int/float/bool/string as text
    - Vector2: "1.5,2"
    - Vector3: "1,2,3"
    """
    return _post(
        "/set-field",
        {
            "path": path,
            "component": component,
            "field": field,
            "value": value,
        },
    )


if __name__ == "__main__":
    mcp.run()
