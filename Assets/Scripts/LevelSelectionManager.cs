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
using System.Collections.Generic;
using TMPro;
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

    [Header("Locked State Visuals")]
    [Tooltip("Locked buttons are tinted to this alpha after grayscale is applied.")]
    [Range(0f, 1f)]
    public float lockedButtonAlpha = 0.65f;

    private int _currentPlayableLevel = 1;
    private readonly Dictionary<int, Color> _originalGraphicColors = new Dictionary<int, Color>();
    private readonly Dictionary<int, Material> _originalGraphicMaterials = new Dictionary<int, Material>();
    private static Material _lockedImageMaterial;

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

            ApplyButtonVisualState(btn, isUnlocked);

            // Clean up old listeners to prevent duplicates if we refresh multiple times
            btn.onClick.RemoveAllListeners();

            // Only attach listener if unlocked
            if (isUnlocked)
            {
                btn.onClick.AddListener(() => OnLevelButtonClicked(levelNumber));
            }
        }
    }

    private void ApplyButtonVisualState(Button btn, bool isUnlocked)
    {
        Graphic[] graphics = btn.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];
            if (graphic == null) continue;

            if (isUnlocked)
            {
                RestoreGraphicVisual(graphic);
                continue;
            }

            ApplyLockedGraphicVisual(graphic);
        }
    }

    private void CacheOriginalGraphicVisual(Graphic graphic)
    {
        int graphicId = graphic.GetInstanceID();

        if (!_originalGraphicColors.ContainsKey(graphicId))
        {
            _originalGraphicColors[graphicId] = graphic.color;
        }

        if ((graphic is Image || graphic is RawImage) && !_originalGraphicMaterials.ContainsKey(graphicId))
        {
            _originalGraphicMaterials[graphicId] = graphic.material;
        }
    }

    private void RestoreGraphicVisual(Graphic graphic)
    {
        CacheOriginalGraphicVisual(graphic);

        int graphicId = graphic.GetInstanceID();
        if (_originalGraphicColors.TryGetValue(graphicId, out Color originalColor))
        {
            graphic.color = originalColor;
        }

        if (_originalGraphicMaterials.TryGetValue(graphicId, out Material originalMaterial))
        {
            graphic.material = originalMaterial;
        }
    }

    private void ApplyLockedGraphicVisual(Graphic graphic)
    {
        CacheOriginalGraphicVisual(graphic);

        int graphicId = graphic.GetInstanceID();
        Color originalColor = _originalGraphicColors[graphicId];

        if (graphic is TMP_Text tmpText)
        {
            Color lockedTextColor = Color.Lerp(originalColor, new Color(0.78f, 0.80f, 0.86f, originalColor.a), 0.9f);
            lockedTextColor.a = originalColor.a * lockedButtonAlpha;
            tmpText.color = lockedTextColor;
            return;
        }

        if (graphic is Image || graphic is RawImage)
        {
            Material lockedMaterial = GetLockedImageMaterial();
            if (lockedMaterial != null)
            {
                graphic.material = lockedMaterial;
            }

            Color lockedColor = Color.Lerp(originalColor, new Color(0.62f, 0.62f, 0.62f, originalColor.a), 0.88f);
            lockedColor.a = originalColor.a * lockedButtonAlpha;
            graphic.color = lockedColor;
            return;
        }

        Color fallbackColor = Color.Lerp(originalColor, Color.gray, 0.9f);
        fallbackColor.a = originalColor.a * lockedButtonAlpha;
        graphic.color = fallbackColor;
    }

    private static Material GetLockedImageMaterial()
    {
        if (_lockedImageMaterial != null)
            return _lockedImageMaterial;

        Shader shader = Shader.Find("Custom/UIGrayscale");
        if (shader == null)
        {
            Debug.LogWarning("[LevelSelectionManager] Custom/UIGrayscale shader not found. Locked buttons will use tint only.");
            return null;
        }

        _lockedImageMaterial = new Material(shader)
        {
            name = "UIGrayscale_Runtime"
        };

        return _lockedImageMaterial;
    }

    /// <summary>
    /// Invoked dynamically when a specific Level button is clicked.
    /// Saves current game data globally, then loads the exact level scene.
    /// </summary>
    private void OnLevelButtonClicked(int levelNumber)
    {
        // Keep the player's actual progress intact.
        // SelectedLevel is only for the scene that will be opened.
        PlayerPrefs.SetInt("SelectedLevel", levelNumber);
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
