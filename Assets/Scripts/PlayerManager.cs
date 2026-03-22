// ============================================================
// PlayerManager.cs
// Purpose: Tracks core in-scene player state (game-over, coin UI).
//          Coin display is driven by TokenManager.GetTokens() so
//          it always reflects the authoritative backend-synced total.
// ============================================================

using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerManager : MonoBehaviour
{
    [Header("Game Over")]
    public static bool isGameOver;
    public GameObject gameOverPanel;

    [Header("Pause UI")]
    public static bool isGamePaused;
    public GameObject pausePanel;

    [Header("Coin UI")]
    [Tooltip("Text element that shows the player's current token / coin count.")]
    public TextMeshProUGUI coinText;

    // ─── Lifecycle ────────────────────────────────────────────

    private void Awake()
    {
        isGameOver = false;
        isGamePaused = false;
        Time.timeScale = 1;
        // No need to read PlayerPrefs manually — TokenManager keeps "PlayerTokens" in sync.
    }

    private void Update()
    {
        // Game-over panel
        if (isGameOver && gameOverPanel != null)
            gameOverPanel.SetActive(true);

        // Update coin text every frame from the unified token store.
        if (coinText != null)
            coinText.text = TokenManager.GetTokens().ToString();
    }

    // ─── Public actions ───────────────────────────────────────

    public void ReplayLevel()
    {
        Time.timeScale = 1;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void PauseGame()
    {
        isGamePaused = true;
        Time.timeScale = 0;
        if (pausePanel != null)
        {
            pausePanel.SetActive(true);
        }
    }

    public void ResumeGame()
    {
        isGamePaused = false;
        Time.timeScale = 1;
        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
        }
    }

    public void HomeButton()
    {
        Time.timeScale = 1;
        SceneManager.LoadScene("Main");
    }
}
