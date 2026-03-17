// ============================================================
// Main.cs
// Purpose: Main scene controller — handles navigation between scenes
//          and persisting game data. On SaveGameData, any overworld
//          coins collected since the last save are flushed to the
//          backend via ProgressService.FlushPendingTokens().
// ============================================================

using UnityEngine;
using UnityEngine.SceneManagement;

public class Main : MonoBehaviour
{
    void Start()
    {
        LoadGameData();
    }

    // ─── Navigation ───────────────────────────────────────────

    public void PlayGame()
    {
        SaveGameData();
        SceneManager.LoadSceneAsync("LevelPanel");
    }

    public void OpenShop()
    {
        SaveGameData();
        SceneManager.LoadSceneAsync("Shop");
    }

    public void BackToMainMenu()
    {
        SaveGameData();
        SceneManager.LoadSceneAsync("Main");
    }

    public void BackToMainMenuFromLevelPanel()
    {
        Debug.Log("[Main] BackToMainMenuFromLevelPanel called.");
        SaveGameData();
        SceneManager.LoadSceneAsync("Main");
    }

    // ─── Game Data ────────────────────────────────────────────

    /// <summary>
    /// Persists current PlayerPrefs and flushes any pending overworld
    /// tokens to the backend via POST /progress/sync-tokens.
    /// </summary>
    public void SaveGameData()
    {
        PlayerPrefs.Save();

        // Flush any coins collected in the overworld to the backend.
        // This is fire-and-forget — the game doesn't wait for the response.
        if (GameApiManager.Instance != null && GameApiManager.Instance.IsLoggedIn)
        {
            int pending = TokenManager.GetPending();
            if (pending > 0)
            {
                Debug.Log($"[Main] SaveGameData: flushing {pending} pending token(s) to backend.");
                ProgressService.FlushPendingTokens(
                    onSuccess: data => Debug.Log($"[Main] Token flush succeeded. New total: {data.totalTokens}"),
                    onError:   err  => Debug.LogWarning($"[Main] Token flush failed (offline?): {err}")
                );
            }
        }
        else
        {
            Debug.Log("[Main] SaveGameData: not logged in — pending tokens will sync on next login.");
        }
    }

    public void LoadGameData()
    {
        // PlayerPrefs are live — TokenManager reads them on demand.
        // Nothing extra needed here; SyncFromBackend is called by GameApiManager
        // on login/session restore.
        Debug.Log($"[Main] LoadGameData — current tokens: {TokenManager.GetTokens()}, pending: {TokenManager.GetPending()}");
    }

    public void ResetGameData()
    {
        PlayerPrefs.DeleteAll();
        Debug.Log("[Main] All game data reset.");
    }

    // Left for backward compatibility with old Inspector button references
    public void PlayLevel1() { }
}
