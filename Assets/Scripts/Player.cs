// ============================================================
// Player.cs
// Purpose: Applies the equipped character's sprite and Animator
//          Controller to the player in game scenes.
//          - Reads "EquippedCharacter" from PlayerPrefs (set by Shop).
//          - If logged in, fetches the equipped character from the
//            backend first (so DB always wins on first load).
//          - Falls back to "SelectedCharacter" index if no ID found.
// ============================================================

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
        // If the user is logged in and SkinService is available,
        // pull the equipped character from the backend so the DB always wins.
        // Otherwise, fall back to PlayerPrefs directly.
        if (SkinService.Instance != null
            && GameApiManager.Instance != null
            && GameApiManager.Instance.IsLoggedIn)
        {
            StartCoroutine(FetchAndApplyEquippedCharacter());
        }
        else
        {
            ApplyCharacterFromPrefs();
        }
    }

    private void HandleAccountStateChanged(UserData _)
    {
        if (!isActiveAndEnabled)
            return;

        ApplyCharacterFromPrefs();
    }

    // ─── Backend fetch ────────────────────────────────────────

    /// <summary>
    /// Fetches the currently equipped character from the backend and
    /// writes its ID into PlayerPrefs before applying it to the player.
    /// Falls back to the local PlayerPrefs value if the request fails.
    /// </summary>
    private IEnumerator FetchAndApplyEquippedCharacter()
    {
        bool done = false;

        yield return StartCoroutine(SkinService.Instance.GetCurrentCharacter(
            onSuccess: equippedId =>
            {
                if (!string.IsNullOrEmpty(equippedId))
                {
                    string normalized = NormalizeCharacterId(equippedId);
                    PlayerPrefs.SetString("EquippedCharacter", normalized);
                    PlayerPrefs.Save();
                    Debug.Log($"[Player] Backend equipped character: '{normalized}'");
                }
                done = true;
            },
            onError: err =>
            {
                Debug.LogWarning($"[Player] Could not fetch equipped character from backend: {err}. Using local PlayerPrefs.");
                done = true;
            }
        ));

        // Always apply after the fetch (success or fallback).
        ApplyCharacterFromPrefs();
    }

    // ─── Apply ───────────────────────────────────────────────

    private void ApplyCharacterFromPrefs()
    {
        selectedOption = ResolveSelectedCharacterIndex();
        UpdateCharacter(selectedOption);
    }

    private void UpdateCharacter(int index)
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

        if (index < 0 || index >= characterDB.CharacterCount)
            index = 0;

        Characters character = characterDB.GetCharacter(index);
        if (character == null) return;

        // ── 1. Swap the sprite ────────────────────────────────
        artworkSprite.sprite = character.characterSprite;

        // ── 2. Swap the Animator Controller ──────────────────
        // Only swap if the character has a controller assigned in CharacterDatabase.
        // This lets each character have unique walk / jump / die animations.
        if (character.animatorController != null)
        {
            Animator anim = artworkSprite.GetComponent<Animator>();
            if (anim == null)
                anim = GetComponent<Animator>(); // fallback: check parent object

            if (anim != null)
            {
                anim.runtimeAnimatorController = character.animatorController;
                Debug.Log($"[Player] Applied animator controller for '{character.CharacterName}'.");
            }
            else
            {
                Debug.LogWarning("[Player] No Animator found on artworkSprite or Player root.");
            }
        }

        Debug.Log($"[Player] Applied character '{character.CharacterName}' (index={index}, id={character.characterId}).");
    }

    // ─── Index resolution ────────────────────────────────────

    private int ResolveSelectedCharacterIndex()
    {
        // Priority 1: Equipped character ID from PlayerPrefs (set by Shop / backend)
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

        // Priority 2: Legacy index-based key
        if (PlayerPrefs.HasKey("SelectedCharacter"))
        {
            Load();
            if (selectedOption >= 0 && selectedOption < characterDB.CharacterCount)
                return selectedOption;
        }

        // Priority 3: Default (first character — usually Ranger)
        return 0;
    }

    private int FindCharacterIndexById(string characterId)
    {
        if (characterDB == null || characterDB.character == null)
            return -1;

        for (int i = 0; i < characterDB.character.Length; i++)
        {
            Characters c = characterDB.character[i];
            if (c == null) continue;

            string dbId = NormalizeCharacterId(c.characterId);
            if (!string.IsNullOrEmpty(dbId) && dbId == characterId)
                return i;

            // Legacy: match by name if characterId is not set
            string legacyName = NormalizeCharacterId(c.CharacterName);
            if (!string.IsNullOrEmpty(legacyName) && legacyName == characterId)
                return i;
        }

        return -1;
    }

    private string NormalizeCharacterId(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return "";
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
