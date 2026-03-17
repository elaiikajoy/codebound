// ============================================================
// 1. Script Name: CharacterManager.cs
// 2. Purpose: Applies the player's selected character (sprite/config)
//             to the player object in game scenes.
//             Reads "SelectedCharacter" (int index) from PlayerPrefs
//             which is written by Shop.cs when the player hits Play.
//
// 3. Unity Setup Instructions:
//    - Attach to: Player GameObject (or a persistent manager object).
//    - Inspector Links:
//        characterDB   – CharacterDatabase ScriptableObject
//        artworkSprite – SpriteRenderer on the Player (or preview object)
//        nameText      – Optional TMP_Text to display the character name
// ============================================================

using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class CharacterManager : MonoBehaviour
{
    [Header("Data")]
    public CharacterDatabase characterDB;

    [Header("UI / Visuals")]
    [Tooltip("World-space display (SpriteRenderer). If assigned, its Rigidbody2D will be set to Kinematic IF in a preview setting.")]
    public SpriteRenderer artworkSprite;

    [Tooltip("UI-space display (Image). Used if the character display is inside a Canvas.")]
    public UnityEngine.UI.Image artworkImage;

    [Tooltip("Optional: a text label that shows the character name.")]
    public TextMeshProUGUI nameText;

    // ─── Lifecycle ────────────────────────────────────────────

    private void Start()
    {
        ApplySelectedCharacter();
    }

    // ─── Public API ───────────────────────────────────────────

    /// <summary>
    /// Reads the saved character index from PlayerPrefs and applies
    /// the correct sprite and name to the player / preview object.
    /// Safe to call at any time (e.g. after returning from the shop).
    /// </summary>
    public void ApplySelectedCharacter()
    {
        if (characterDB == null)
        {
            Debug.LogError("[CharacterManager] CharacterDatabase is not assigned in the Inspector!");
            return;
        }

        int index = PlayerPrefs.GetInt("SelectedCharacter", 0);

        // Clamp in case the database was resized after the player saved.
        index = Mathf.Clamp(index, 0, characterDB.CharacterCount - 1);

        Characters character = characterDB.GetCharacter(index);
        if (character == null)
        {
            Debug.LogWarning($"[CharacterManager] No character found at index {index}.");
            return;
        }

        if (artworkSprite != null)
        {
            artworkSprite.sprite = character.characterSprite;

            // BUG FIX: Auto-freeze physics if this manager is on a preview object
            // This prevents falling in the main menu/shop if a real prefab is used.
            var rb = artworkSprite.GetComponent<Rigidbody2D>();
            if (rb != null && !SceneManager.GetActiveScene().name.Contains("Level"))
            {
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.velocity = Vector2.zero;
            }
        }

        if (artworkImage != null)
            artworkImage.sprite = character.characterSprite;

        if (nameText != null)
            nameText.text = character.CharacterName;

        Debug.Log($"[CharacterManager] Applied character '{character.CharacterName}' (index={index}, id={character.characterId}).");
    }

    /// <summary>
    /// Scene loader helper — kept for backward-compatible Inspector button wiring.
    /// </summary>
    public void ChangeScene(int sceneID)
    {
        UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(sceneID);
    }
}
