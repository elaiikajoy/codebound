using UnityEngine;

// Lightweight on-screen debug/status overlay for network integration.
// Shows whether ApiConfig/ApiClient/GameApiManager/ProgressService are ready
// and displays the last progress sync status recorded by ProgressService.
// Attach this to any GameObject in the first scene, or let it create itself at runtime.
public class DebugStatusOverlay : MonoBehaviour
{
    public static DebugStatusOverlay Instance { get; private set; }

    private bool visible = true;
    private Vector2 scroll = Vector2.zero;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        // Toggle with F1
        if (Input.GetKeyDown(KeyCode.F1)) visible = !visible;
    }

    private void OnGUI()
    {
        if (!visible) return;

        int w = 360;
        int h = 160;
        int margin = 10;
        Rect rect = new Rect(Screen.width - w - margin, margin, w, h);
        GUI.Box(rect, "Network Debug");

        GUILayout.BeginArea(new Rect(rect.x + 8, rect.y + 20, rect.width - 16, rect.height - 28));
        scroll = GUILayout.BeginScrollView(scroll);

        // ApiConfig
        bool cfgReady = ApiConfig.Instance != null && ApiConfig.Instance.IsReady;
        GUILayout.Label($"ApiConfig: {(cfgReady ? "Ready" : "Not ready")}");

        // ApiClient
        GUILayout.Label($"ApiClient: {(ApiClient.Instance != null ? "Present" : "Missing")}");

        // GameApiManager / auth
        bool gamemgrPresent = GameApiManager.Instance != null;
        string authState = "No";
        string username = "-";
        if (gamemgrPresent)
        {
            authState = GameApiManager.Instance.IsLoggedIn ? "Yes" : "No";
            username = GameApiManager.Instance.CurrentUser != null ? GameApiManager.Instance.CurrentUser.username : "-";
        }
        GUILayout.Label($"Logged in: {authState}  user: {username}");

        // ProgressService
        GUILayout.Label($"ProgressService: {(ProgressService.Instance != null ? "Present" : "Missing")}");

        // Last sync info
        GUILayout.Label($"Last Sync: {ProgressService.LastSyncStatus}");
        GUILayout.Label($"Time: {ProgressService.LastSyncTime}");
        GUILayout.Label("Details:");
        GUILayout.TextArea(ProgressService.LastSyncDetails ?? "", GUILayout.Height(36));

        GUILayout.EndScrollView();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Manual Push"))
        {
            // Compute last completed level from HighestLevel convention
            int localHighest = PlayerPrefs.HasKey("HighestLevel") ? PlayerPrefs.GetInt("HighestLevel") : 1;
            int completed = Mathf.Max(1, localHighest - 1);
            int tokens = PlayerPrefs.HasKey("TotalTokens") ? PlayerPrefs.GetInt("TotalTokens") : 0;
            Debug.Log($"[DebugStatusOverlay] ManualPush -> completed:{completed} tokens:{tokens}");
            ProgressService.SyncAfterLevel(completed, tokens);
        }

        if (GUILayout.Button("Clear Last"))
        {
            ProgressService.LastSyncStatus = "Never";
            ProgressService.LastSyncDetails = string.Empty;
            ProgressService.LastSyncTime = string.Empty;
        }
        GUILayout.EndHorizontal();

        GUILayout.EndArea();
    }
}
