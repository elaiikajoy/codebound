// ============================================================
// 1. Script Name: CharacterManager.cs
// 2. Purpose: Applies the player's selected character (sprite/config)
//             to the player object in game scenes.
//             Reads the backend-backed "EquippedCharacter" first,
//             then falls back to the local "SelectedCharacter" index.
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

    private void OnEnable()
    {
        GameApiManager.OnLoginSuccess += HandleAccountStateChanged;
        GameApiManager.OnSessionRestored += HandleAccountStateChanged;
    }

    private void OnDisable()
    {
        GameApiManager.OnLoginSuccess -= HandleAccountStateChanged;
        GameApiManager.OnSessionRestored -= HandleAccountStateChanged;
    }

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

        int index = ResolveCharacterIndex();

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

    private void HandleAccountStateChanged(UserData _)
    {
        if (!isActiveAndEnabled)
            return;

        ApplySelectedCharacter();
    }

    private int ResolveCharacterIndex()
    {
        int equippedIndex = FindIndexByEquippedId();
        if (equippedIndex >= 0)
            return equippedIndex;

        if (PlayerPrefs.HasKey("SelectedCharacter"))
            return PlayerPrefs.GetInt("SelectedCharacter", 0);

        return 0;
    }

    private string NormalizeCharacterId(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        string normalized = value.Trim().ToLowerInvariant();
        if (normalized == "default") return "ranger";
        return normalized;
    }

    private string GetCharacterId(Characters character)
    {
        if (character == null) return string.Empty;
        if (!string.IsNullOrWhiteSpace(character.characterId))
            return NormalizeCharacterId(character.characterId);
        return NormalizeCharacterId(character.CharacterName);
    }

    private int FindIndexByEquippedId()
    {
        if (!PlayerPrefs.HasKey("EquippedCharacter"))
            return -1;

        string equipped = NormalizeCharacterId(PlayerPrefs.GetString("EquippedCharacter", string.Empty));

        if (string.IsNullOrEmpty(equipped))
            return -1;

        for (int i = 0; i < characterDB.CharacterCount; i++)
        {
            Characters c = characterDB.GetCharacter(i);
            if (c != null && GetCharacterId(c) == equipped)
                return i;
        }

        return -1;
    }

    /// <summary>
    /// Scene loader helper — kept for backward-compatible Inspector button wiring.
    /// </summary>
    public void ChangeScene(int sceneID)
    {
        UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(sceneID);
    }
}
