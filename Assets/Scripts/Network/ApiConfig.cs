// ============================================================
// ApiConfig.cs
// Purpose: Reads StreamingAssets/game.config.json at startup
//          and exposes configuration to all Network services.
//
// Unity Setup:
//   - Attach to a persistent "GameAPI" GameObject in the first
//     scene (e.g. Main or a dedicated Bootstrap scene).
//   - The GameObject will survive all scene loads.
//
// Config file: Assets/StreamingAssets/game.config.json
//   {
//     "backendBaseUrl": "http://localhost:3000",
//     "apiKey": "your-api-key-here",
//     "gameVersion": "1.0.0",
//     "debugMode": true
//   }
//
// All services wait for ApiConfig.Instance.IsReady == true
// before making any HTTP request.
// ============================================================

using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

public class ApiConfig : MonoBehaviour
{
    // ─── Singleton ────────────────────────────────────────────
    public static ApiConfig Instance { get; private set; }

    // ─── Public state ─────────────────────────────────────────
    /// <summary>Loaded configuration — null until IsReady.</summary>
    public GameConfig Config { get; private set; }

    /// <summary>True once game.config.json has been parsed.</summary>
    public bool IsReady { get; private set; }

    // ─── Lifecycle ────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        StartCoroutine(LoadConfig());
    }

    // ─── Config loader ────────────────────────────────────────
    private IEnumerator LoadConfig()
    {
        // Application.streamingAssetsPath is platform-aware:
        //   Windows/Mac/iOS editor: direct file path
        //   Android:                content:// URI inside compressed JAR
        // UnityWebRequest handles all cases safely.
        string path = Path.Combine(Application.streamingAssetsPath, "game.config.json");

        using (UnityWebRequest req = UnityWebRequest.Get(path))
        {
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError(
                    $"[ApiConfig] ✗ Failed to load game.config.json\n" +
                    $"Path : {path}\n" +
                    $"Error: {req.error}"
                );
                yield break;
            }

            Config = JsonUtility.FromJson<GameConfig>(req.downloadHandler.text);

            if (Config == null)
            {
                Debug.LogError("[ApiConfig] ✗ game.config.json parsed as null — check JSON syntax.");
                yield break;
            }

            IsReady = true;

            if (Config.debugMode)
                Debug.Log(
                    $"[ApiConfig] ✓ Config loaded\n" +
                    $"Backend : {Config.backendBaseUrl}\n" +
                    $"Version : {Config.gameVersion}"
                );
        }
    }
}
