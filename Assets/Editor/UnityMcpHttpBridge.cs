using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class UnityMcpHttpBridge
{
    private const string Prefix = "http://127.0.0.1:6401/";
    private const string AutoStartPrefKey = "UnityMcpHttpBridge.AutoStart";
    private const int MaxLogs = 300;

    private static readonly ConcurrentQueue<Action> MainThreadQueue = new ConcurrentQueue<Action>();
    private static readonly object LogLock = new object();
    private static readonly List<LogEntry> Logs = new List<LogEntry>();

    private static HttpListener listener;
    private static Thread serverThread;
    private static bool running;

    static UnityMcpHttpBridge()
    {
        EditorApplication.update += PumpMainThreadQueue;
        Application.logMessageReceivedThreaded += OnLog;

        bool autoStart = EditorPrefs.GetBool(AutoStartPrefKey, true);
        if (autoStart)
        {
            EditorApplication.delayCall += Start;
        }
    }

    [MenuItem("Tools/MCP/Start Bridge")]
    public static void Start()
    {
        if (running)
        {
            return;
        }

        try
        {
            listener = new HttpListener();
            listener.Prefixes.Add(Prefix);
            listener.Start();

            running = true;
            serverThread = new Thread(ServerLoop) { IsBackground = true, Name = "UnityMcpHttpBridge" };
            serverThread.Start();

            Debug.Log($"[UnityMCP] Bridge started at {Prefix}");
        }
        catch (Exception ex)
        {
            running = false;
            Debug.LogError($"[UnityMCP] Failed to start bridge: {ex.Message}");
        }
    }

    [MenuItem("Tools/MCP/Stop Bridge")]
    public static void Stop()
    {
        if (!running)
        {
            return;
        }

        running = false;

        try
        {
            listener?.Stop();
            listener?.Close();
        }
        catch
        {
        }

        listener = null;
        Debug.Log("[UnityMCP] Bridge stopped");
    }

    [MenuItem("Tools/MCP/Toggle Auto Start")]
    public static void ToggleAutoStart()
    {
        bool current = EditorPrefs.GetBool(AutoStartPrefKey, true);
        bool next = !current;
        EditorPrefs.SetBool(AutoStartPrefKey, next);
        Debug.Log($"[UnityMCP] Auto Start: {(next ? "ON" : "OFF")}");
    }

    private static void ServerLoop()
    {
        while (running && listener != null)
        {
            HttpListenerContext context;
            try
            {
                context = listener.GetContext();
            }
            catch
            {
                if (!running)
                {
                    return;
                }

                continue;
            }

            try
            {
                HandleRequest(context);
            }
            catch (Exception ex)
            {
                WriteJson(context.Response, 500, Json(new ErrorResponse
                {
                    ok = false,
                    error = ex.Message
                }));
            }
        }
    }

    private static void HandleRequest(HttpListenerContext context)
    {
        string path = context.Request.Url.AbsolutePath.TrimEnd('/').ToLowerInvariant();
        if (path.Length == 0)
        {
            path = "/";
        }

        if (path == "/health")
        {
            string json = ExecuteOnMainThread(() => Json(new HealthResponse
            {
                ok = true,
                projectPath = Directory.GetCurrentDirectory().Replace('\\', '/'),
                activeScene = SceneManager.GetActiveScene().name,
                isPlaying = EditorApplication.isPlaying
            }));
            WriteJson(context.Response, 200, json);
            return;
        }

        if (path == "/hierarchy")
        {
            string json = ExecuteOnMainThread(BuildHierarchyJson);
            WriteJson(context.Response, 200, json);
            return;
        }

        if (path == "/find" && context.Request.HttpMethod == "GET")
        {
            string name = context.Request.QueryString["name"] ?? string.Empty;
            string json = ExecuteOnMainThread(() => BuildFindResultJson(name));
            WriteJson(context.Response, 200, json);
            return;
        }

        if (path == "/console" && context.Request.HttpMethod == "GET")
        {
            int count = 50;
            int.TryParse(context.Request.QueryString["count"], out count);
            WriteJson(context.Response, 200, BuildConsoleJson(Mathf.Clamp(count, 1, MaxLogs)));
            return;
        }

        if (path == "/execute-menu" && context.Request.HttpMethod == "POST")
        {
            ExecuteMenuRequest payload = ReadBody<ExecuteMenuRequest>(context.Request);
            string json = ExecuteOnMainThread(() =>
            {
                bool result = !string.IsNullOrEmpty(payload.menuItem) && EditorApplication.ExecuteMenuItem(payload.menuItem);
                return Json(new OkResponse
                {
                    ok = result,
                    message = result ? "Menu executed" : "Menu item failed"
                });
            });
            WriteJson(context.Response, 200, json);
            return;
        }

        if (path == "/setup-player-animator" && context.Request.HttpMethod == "POST")
        {
            string json = ExecuteOnMainThread(() =>
            {
                PlayerAnimatorStateMachineSetup.SetupStateMachine();
                return Json(new OkResponse
                {
                    ok = true,
                    message = "Player animator configured"
                });
            });
            WriteJson(context.Response, 200, json);
            return;
        }

        if (path == "/setup-boss-animator" && context.Request.HttpMethod == "POST")
        {
            string json = ExecuteOnMainThread(() =>
            {
                BossBatchCreateAnimAssets.Generate();
                return Json(new OkResponse
                {
                    ok = true,
                    message = "Boss animator configured"
                });
            });
            WriteJson(context.Response, 200, json);
            return;
        }

        if (path == "/set-field" && context.Request.HttpMethod == "POST")
        {
            SetFieldRequest payload = ReadBody<SetFieldRequest>(context.Request);
            string json = ExecuteOnMainThread(() => SetFieldJson(payload));
            WriteJson(context.Response, 200, json);
            return;
        }

        WriteJson(context.Response, 404, Json(new ErrorResponse
        {
            ok = false,
            error = "Unknown route"
        }));
    }

    private static string BuildHierarchyJson()
    {
        Scene scene = SceneManager.GetActiveScene();
        GameObject[] roots = scene.GetRootGameObjects();
        HierarchyResponse response = new HierarchyResponse
        {
            ok = true,
            scene = scene.name,
            nodes = new List<HierarchyNode>()
        };

        for (int i = 0; i < roots.Length; i++)
        {
            response.nodes.Add(ToNode(roots[i].transform, roots[i].name));
        }

        return Json(response);
    }

    private static string BuildFindResultJson(string name)
    {
        List<GameObjectInfo> results = new List<GameObjectInfo>();
        Scene scene = SceneManager.GetActiveScene();
        GameObject[] roots = scene.GetRootGameObjects();

        for (int i = 0; i < roots.Length; i++)
        {
            CollectByName(roots[i].transform, name, results);
        }

        return Json(new FindResponse
        {
            ok = true,
            count = results.Count,
            results = results
        });
    }

    private static void CollectByName(Transform t, string name, List<GameObjectInfo> results)
    {
        if (string.IsNullOrEmpty(name) || t.name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
        {
            results.Add(new GameObjectInfo
            {
                name = t.name,
                path = BuildPath(t),
                active = t.gameObject.activeInHierarchy
            });
        }

        for (int i = 0; i < t.childCount; i++)
        {
            CollectByName(t.GetChild(i), name, results);
        }
    }

    private static string SetFieldJson(SetFieldRequest payload)
    {
        if (payload == null || string.IsNullOrEmpty(payload.path) || string.IsNullOrEmpty(payload.component) || string.IsNullOrEmpty(payload.field))
        {
            return Json(new ErrorResponse { ok = false, error = "path/component/field is required" });
        }

        GameObject go = FindByPath(payload.path);
        if (go == null)
        {
            return Json(new ErrorResponse { ok = false, error = "GameObject not found" });
        }

        Component component = go.GetComponent(payload.component);
        if (component == null)
        {
            return Json(new ErrorResponse { ok = false, error = "Component not found" });
        }

        FieldInfo field = component.GetType().GetField(payload.field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field == null)
        {
            return Json(new ErrorResponse { ok = false, error = "Field not found" });
        }

        object converted;
        if (!TryConvert(payload.value, field.FieldType, out converted))
        {
            return Json(new ErrorResponse { ok = false, error = "Type conversion failed" });
        }

        Undo.RecordObject(component, "MCP Set Field");
        field.SetValue(component, converted);
        EditorUtility.SetDirty(component);
        return Json(new OkResponse { ok = true, message = "Field updated" });
    }

    private static bool TryConvert(string value, Type type, out object converted)
    {
        converted = null;

        try
        {
            if (type == typeof(string))
            {
                converted = value;
                return true;
            }

            if (type == typeof(int))
            {
                converted = int.Parse(value);
                return true;
            }

            if (type == typeof(float))
            {
                converted = float.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
                return true;
            }

            if (type == typeof(bool))
            {
                converted = bool.Parse(value);
                return true;
            }

            if (type == typeof(Vector2))
            {
                string[] parts = value.Split(',');
                converted = new Vector2(
                    float.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture),
                    float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture));
                return true;
            }

            if (type == typeof(Vector3))
            {
                string[] parts = value.Split(',');
                converted = new Vector3(
                    float.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture),
                    float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture),
                    float.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture));
                return true;
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    private static GameObject FindByPath(string path)
    {
        string[] tokens = path.Split('/');
        Scene scene = SceneManager.GetActiveScene();
        GameObject[] roots = scene.GetRootGameObjects();

        for (int i = 0; i < roots.Length; i++)
        {
            if (!string.Equals(roots[i].name, tokens[0], StringComparison.Ordinal))
            {
                continue;
            }

            Transform current = roots[i].transform;
            for (int t = 1; t < tokens.Length && current != null; t++)
            {
                Transform next = current.Find(tokens[t]);
                current = next;
            }

            if (current != null)
            {
                return current.gameObject;
            }
        }

        return null;
    }

    private static HierarchyNode ToNode(Transform transform, string path)
    {
        HierarchyNode node = new HierarchyNode
        {
            name = transform.name,
            path = path,
            active = transform.gameObject.activeInHierarchy,
            children = new List<HierarchyNode>()
        };

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            node.children.Add(ToNode(child, path + "/" + child.name));
        }

        return node;
    }

    private static string BuildPath(Transform transform)
    {
        if (transform.parent == null)
        {
            return transform.name;
        }

        return BuildPath(transform.parent) + "/" + transform.name;
    }

    private static void PumpMainThreadQueue()
    {
        while (MainThreadQueue.TryDequeue(out Action action))
        {
            action.Invoke();
        }
    }

    private static T ExecuteOnMainThread<T>(Func<T> action)
    {
        ManualResetEventSlim wait = new ManualResetEventSlim(false);
        T result = default;
        Exception exception = null;

        MainThreadQueue.Enqueue(() =>
        {
            try
            {
                result = action.Invoke();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
            finally
            {
                wait.Set();
            }
        });

        wait.Wait();
        if (exception != null)
        {
            throw exception;
        }

        return result;
    }

    private static void WriteJson(HttpListenerResponse response, int statusCode, string json)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        response.StatusCode = statusCode;
        response.ContentType = "application/json";
        response.ContentEncoding = Encoding.UTF8;
        response.ContentLength64 = bytes.Length;
        using (Stream s = response.OutputStream)
        {
            s.Write(bytes, 0, bytes.Length);
        }
    }

    private static T ReadBody<T>(HttpListenerRequest request) where T : new()
    {
        if (!request.HasEntityBody)
        {
            return new T();
        }

        using (StreamReader reader = new StreamReader(request.InputStream, request.ContentEncoding))
        {
            string body = reader.ReadToEnd();
            if (string.IsNullOrWhiteSpace(body))
            {
                return new T();
            }

            return JsonUtility.FromJson<T>(body);
        }
    }

    private static string BuildConsoleJson(int count)
    {
        List<LogEntry> snapshot;
        lock (LogLock)
        {
            int skip = Mathf.Max(0, Logs.Count - count);
            snapshot = Logs.GetRange(skip, Logs.Count - skip);
        }

        return Json(new ConsoleResponse
        {
            ok = true,
            count = snapshot.Count,
            logs = snapshot
        });
    }

    private static void OnLog(string condition, string stackTrace, LogType type)
    {
        lock (LogLock)
        {
            Logs.Add(new LogEntry
            {
                type = type.ToString(),
                message = condition,
                stack = stackTrace
            });

            if (Logs.Count > MaxLogs)
            {
                Logs.RemoveAt(0);
            }
        }
    }

    private static string Json<T>(T payload)
    {
        return JsonUtility.ToJson(payload, true);
    }

    [Serializable]
    private class HealthResponse
    {
        public bool ok;
        public string projectPath;
        public string activeScene;
        public bool isPlaying;
    }

    [Serializable]
    private class HierarchyResponse
    {
        public bool ok;
        public string scene;
        public List<HierarchyNode> nodes;
    }

    [Serializable]
    private class HierarchyNode
    {
        public string name;
        public string path;
        public bool active;
        public List<HierarchyNode> children;
    }

    [Serializable]
    private class GameObjectInfo
    {
        public string name;
        public string path;
        public bool active;
    }

    [Serializable]
    private class FindResponse
    {
        public bool ok;
        public int count;
        public List<GameObjectInfo> results;
    }

    [Serializable]
    private class ExecuteMenuRequest
    {
        public string menuItem;
    }

    [Serializable]
    private class SetFieldRequest
    {
        public string path;
        public string component;
        public string field;
        public string value;
    }

    [Serializable]
    private class OkResponse
    {
        public bool ok;
        public string message;
    }

    [Serializable]
    private class ErrorResponse
    {
        public bool ok;
        public string error;
    }

    [Serializable]
    private class LogEntry
    {
        public string type;
        public string message;
        public string stack;
    }

    [Serializable]
    private class ConsoleResponse
    {
        public bool ok;
        public int count;
        public List<LogEntry> logs;
    }
}
