// ============================================================
// LevelSelectionManager.cs
// Purpose: Manages a grid of UI buttons for level selection. 
//          Automatically locks levels the player hasn't reached yet
//          by communicating with GameApiManager and ProgressService.
//
// Unity Setup:
//   - Attach to an empty GameObject named "LevelManager" in the LevelPanel scene.
//   - Assign all 100 Level Buttons to the 'LevelButtons' array in the inspector.
//   - Ensure each button acts as Level 1 for index 0, Level 2 for index 1, etc.
//   - This script dynamically adds onClick listeners, so you don't need
//     to manually link each button in the Unity Editor.
// ============================================================

using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class LevelSelectionManager : MonoBehaviour
{
    [Header("UI Configuration")]
    [Tooltip("Array of level buttons, strictly ordered from Level 1 to Level X.")]
    public Button[] levelButtons;

    [Header("Scene Config")]
    [Tooltip("The prefix name of your level scenes (e.g., 'Level' for 'Level1', 'Level2')")]
    public string levelScenePrefix = "Level";

    [Tooltip("If true, automatically fetches latest progress from the backend before showing levels.")]
    public bool fetchProgressOnStart = true;

    private int _currentPlayableLevel = 1;

    private void OnEnable()
    {
        // Refresh whenever this object becomes active. This ensures that if
        // progress was updated in another scene (e.g., after completing a level),
        // the UI reflects it immediately.
        RefreshButtonsFromLocalCache();
    }

    private void Start()
    {
        // 1. Initial local unlock so buttons look right immediately.
        //    Use the max of CurrentLevel/HighestLevel to handle scenarios where
        //    the backend treats 'currentLevel' differently than our UI.
        RefreshButtonsFromLocalCache();

        // 2. Fetch from backend after login/session restores.
        //    If the player isn't logged in yet, we'll refresh when they do.
        if (fetchProgressOnStart)
        {
            StartCoroutine(DelayedFetchProgress());
        }

        // 3. Keep UI updated when the player logs in later while on this screen.
        GameApiManager.OnLoginSuccess += HandleLoginOrSessionRestored;
        GameApiManager.OnSessionRestored += HandleLoginOrSessionRestored;
    }

    private void OnDestroy()
    {
        GameApiManager.OnLoginSuccess -= HandleLoginOrSessionRestored;
        GameApiManager.OnSessionRestored -= HandleLoginOrSessionRestored;
    }

    private void HandleLoginOrSessionRestored(UserData _) => TryFetchProgressFromBackend();

    private System.Collections.IEnumerator DelayedFetchProgress()
    {
        // Wait a little in case GameApiManager/ProgressService are still initializing.
        float elapsed = 0f;
        const float timeout = 5f;

        while ((GameApiManager.Instance == null || ProgressService.Instance == null) && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        TryFetchProgressFromBackend();
    }

    private void RefreshButtonsFromLocalCache()
    {
        int current = PlayerPrefs.HasKey("CurrentLevel")
            ? PlayerPrefs.GetInt("CurrentLevel")
            : 1;

        int highest = PlayerPrefs.HasKey("HighestLevel")
            ? PlayerPrefs.GetInt("HighestLevel")
            : 1;

        _currentPlayableLevel = Mathf.Max(current, highest);
        RefreshButtons();
    }

    private void TryFetchProgressFromBackend()
    {
        if (GameApiManager.Instance == null || !GameApiManager.Instance.IsLoggedIn)
            return;

        if (ProgressService.Instance == null)
            return;

        StartCoroutine(ProgressService.Instance.GetProgress(
            onSuccess: progress =>
            {
                // Ensure local cache is updated
                PlayerPrefs.SetInt("HighestLevel", progress.highestLevel);
                PlayerPrefs.SetInt("CurrentLevel", progress.currentLevel);
                PlayerPrefs.Save();

                _currentPlayableLevel = Mathf.Max(progress.currentLevel, progress.highestLevel);
                RefreshButtons();
            },
            onError: err =>
            {
                Debug.LogWarning($"[LevelSelectionManager] Failed to fetch progress from backend: {err}");
                // Silently fail to local cache
            }
        ));
    }

    /// <summary>
    /// Loops through all assigned buttons, locking or unlocking them,
    /// and assigning the dynamic scene-load click event.
    /// </summary>
    private void RefreshButtons()
    {
        if (levelButtons == null || levelButtons.Length == 0)
        {
            Debug.LogWarning("[LevelSelectionManager] No buttons assigned in the inspector!");
            return;
        }

        for (int i = 0; i < levelButtons.Length; i++)
        {
            Button btn = levelButtons[i];
            if (btn == null) continue;

            int levelNumber = i + 1; // Array index 0 is Level 1

            // Lock or unlock based on progress 
            // (levelNumber <= current means it's unlocked and playable)
            bool isUnlocked = (levelNumber <= _currentPlayableLevel);
            btn.interactable = isUnlocked;

            // Optional: change alpha or visual state of the button
            CanvasGroup group = btn.GetComponent<CanvasGroup>();
            if (group != null)
            {
                group.alpha = isUnlocked ? 1.0f : 0.5f;
            }

            // Clean up old listeners to prevent duplicates if we refresh multiple times
            btn.onClick.RemoveAllListeners();

            // Only attach listener if unlocked
            if (isUnlocked)
            {
                btn.onClick.AddListener(() => OnLevelButtonClicked(levelNumber));
            }
        }
    }

    /// <summary>
    /// Invoked dynamically when a specific Level button is clicked.
    /// Saves current game data globally, then loads the exact level scene.
    /// </summary>
    private void OnLevelButtonClicked(int levelNumber)
    {
        // Update the 'CurrentLevel' in PlayerPrefs so other scripts 
        // know what level the player is actively playing.
        PlayerPrefs.SetInt("CurrentLevel", levelNumber);
        PlayerPrefs.Save();

        string sceneName = levelScenePrefix + levelNumber;
        Debug.Log($"[LevelSelectionManager] Loading scene: {sceneName}");

        // Try direct load first
        if (Application.CanStreamedLevelBeLoaded(sceneName))
        {
            SceneManager.LoadSceneAsync(sceneName);
            return;
        }

        // Try common alternate path (some Build Settings entries include folder)
        string altPath = "Scenes/" + sceneName;
        if (Application.CanStreamedLevelBeLoaded(altPath))
        {
            SceneManager.LoadSceneAsync(altPath);
            return;
        }

        // Try scene file name with extension
        string altFile = sceneName + ".unity";
        if (Application.CanStreamedLevelBeLoaded(altFile))
        {
            SceneManager.LoadSceneAsync(altFile);
            return;
        }

        // As a last resort, enumerate Build Settings entries to find a matching path
        int count = SceneManager.sceneCountInBuildSettings;
        for (int i = 0; i < count; i++)
        {
            string path = UnityEngine.SceneManagement.SceneUtility.GetScenePathByBuildIndex(i);
            if (path.EndsWith("/" + sceneName + ".unity") || path.EndsWith("\\" + sceneName + ".unity") || path.Contains("/" + sceneName + ".unity") || path.Contains("\\" + sceneName + ".unity"))
            {
                Debug.Log($"[LevelSelectionManager] Found build-scene path: {path} — loading by path.");
                SceneManager.LoadSceneAsync(path);
                return;
            }
        }

        // Nothing matched — provide a detailed error listing build scenes for debugging.
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine($"[LevelSelectionManager] Scene '{sceneName}' not found in Build Settings. Scenes in Build:");
        for (int i = 0; i < count; i++)
        {
            string path = UnityEngine.SceneManagement.SceneUtility.GetScenePathByBuildIndex(i);
            sb.AppendLine($"  [{i}] {path}");
        }
        Debug.LogError(sb.ToString());
        Debug.LogError($"[LevelSelectionManager] Scene '{sceneName}' couldn't be loaded. Add it via File -> Build Settings or rename the scene to match '{sceneName}'.");
    }
}
