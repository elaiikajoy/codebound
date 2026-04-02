using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour
{
    public CharacterDatabase characterDB;
    public SpriteRenderer artworkSprite;
    private int selectedOption = 0;

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

    void Start()
    {
        ApplyCharacterFromPrefs();
    }

    private void HandleAccountStateChanged(UserData _)
    {
        if (!isActiveAndEnabled)
            return;

        ApplyCharacterFromPrefs();
    }

    private void ApplyCharacterFromPrefs()
    {
        selectedOption = ResolveSelectedCharacterIndex();
        UpdateCharacter(selectedOption);
    }

    private void UpdateCharacter(int selectedOption)
    {
        if (characterDB == null || characterDB.character == null || characterDB.character.Length == 0)
        {
            Debug.LogWarning("[Player] CharacterDatabase is not configured.");
            return;
        }

        if (artworkSprite == null)
        {
            Debug.LogWarning("[Player] Artwork SpriteRenderer is not assigned.");
            return;
        }

        if (selectedOption < 0 || selectedOption >= characterDB.CharacterCount)
        {
            selectedOption = 0;
        }

        Characters character = characterDB.GetCharacter(selectedOption);
        artworkSprite.sprite = character.characterSprite;

        ApplyCharacterAnimator(character);
    }

    private int ResolveSelectedCharacterIndex()
    {
        string equippedId = NormalizeCharacterId(PlayerPrefs.GetString("EquippedCharacter", ""));
        if (!string.IsNullOrEmpty(equippedId))
        {
            int equippedIndex = FindCharacterIndexById(equippedId);
            if (equippedIndex >= 0)
            {
                PlayerPrefs.SetInt("SelectedCharacter", equippedIndex);
                return equippedIndex;
            }
        }

        if (PlayerPrefs.HasKey("SelectedCharacter"))
        {
            Load();
            if (selectedOption >= 0 && selectedOption < characterDB.CharacterCount)
            {
                return selectedOption;
            }
        }

        return 0;
    }

    private int FindCharacterIndexById(string characterId)
    {
        if (characterDB == null || characterDB.character == null)
        {
            return -1;
        }

        for (int i = 0; i < characterDB.character.Length; i++)
        {
            Characters c = characterDB.character[i];
            if (c == null)
            {
                continue;
            }

            string dbId = NormalizeCharacterId(c.characterId);
            if (!string.IsNullOrEmpty(dbId) && dbId == characterId)
            {
                return i;
            }

            // Backward compatibility for databases that still only use CharacterName.
            string legacyName = NormalizeCharacterId(c.CharacterName);
            if (!string.IsNullOrEmpty(legacyName) && legacyName == characterId)
            {
                return i;
            }
        }

        return -1;
    }

    private void ApplyCharacterAnimator(Characters character)
    {
        if (artworkSprite == null || character == null || character.animatorController == null)
        {
            return;
        }

        Animator animator = artworkSprite.GetComponent<Animator>();
        if (animator == null)
        {
            return;
        }

        animator.runtimeAnimatorController = character.animatorController;
        animator.enabled = true;
    }

    private string NormalizeCharacterId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return string.Empty;
        }

        string normalized = id.Trim().ToLowerInvariant();
        if (normalized == "default") return "ranger";
        if (normalized == "minotaur") return "minatour";
        return normalized;
    }

    private void Load()
    {
        selectedOption = PlayerPrefs.GetInt("SelectedCharacter");
    }
}
