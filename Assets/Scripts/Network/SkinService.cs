// ============================================================
// SkinService.cs
// Purpose: Character service for shop dropdown and buy/equip integration.
//            GET  /characters           — equipped + owned + catalog + tokens
//            POST /characters/buy       — buy a character
//            POST /characters/equip     — set active character
//
// Unity Setup:
//   - Attach to the "GameAPI" persistent GameObject.
//   - Shop.cs calls EquipCharacter() after local character selection.
//
// Character purchases stay local in Unity.
// Backend stores only the currently equipped character.
// ============================================================

using System;
using System.Collections;
using UnityEngine;

public class SkinService : MonoBehaviour
{
    // ─── Singleton ────────────────────────────────────────────
    public static SkinService Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ─── Character dropdown state (protected) ─────────────────
    public IEnumerator GetCharacterState(
        Action<CharacterStateData> onSuccess,
        Action<string> onError = null)
    {
        if (!GameApiManager.Instance.IsLoggedIn) { onError?.Invoke("Not logged in."); yield break; }

        yield return StartCoroutine(ApiClient.Instance.Get(
            "/characters",
            onSuccess: json =>
            {
                var result = JsonUtility.FromJson<CharacterStateResponse>(json);
                if (result != null && result.success && result.data != null)
                    onSuccess?.Invoke(result.data);
                else
                    onError?.Invoke(result?.message ?? "Failed to fetch character state.");
            },
            onError: onError ?? (e => Debug.LogWarning(e)),
            requiresAuth: true
        ));
    }

    // ─── Current character state (protected) ──────────────────
    /// <summary>
    /// GET /characters — returns the currently equipped character for the player.
    /// </summary>
    public IEnumerator GetCurrentCharacter(
        Action<string> onSuccess,
        Action<string> onError = null)
    {
        yield return StartCoroutine(GetCharacterState(
            onSuccess: state => onSuccess?.Invoke(state?.equippedCharacter ?? "default"),
            onError: onError
        ));
    }

    public IEnumerator GetAvailableCharacters(
        Action<CharacterItem[]> onSuccess,
        Action<string> onError = null)
    {
        yield return StartCoroutine(GetCharacterState(
            onSuccess: state => onSuccess?.Invoke(state?.availableCharacters ?? new CharacterItem[0]),
            onError: onError
        ));
    }

    public IEnumerator BuyCharacter(
        string characterId,
        Action<CharacterStateData> onSuccess = null,
        Action<string> onError = null)
    {
        if (!GameApiManager.Instance.IsLoggedIn) { onError?.Invoke("Not logged in."); yield break; }

        var body = new BuyCharacterRequest { characterId = characterId };

        yield return StartCoroutine(ApiClient.Instance.Post(
            "/characters/buy",
            body,
            onSuccess: json =>
            {
                var result = JsonUtility.FromJson<BuyCharacterResponse>(json);
                if (result != null && result.success && result.data != null)
                    onSuccess?.Invoke(result.data);
                else
                    onError?.Invoke(result?.message ?? "Failed to buy character.");
            },
            onError: onError ?? (e => Debug.LogWarning(e)),
            requiresAuth: true
        ));
    }

    // ─── Equip character (protected) ──────────────────────────
    /// <summary>
    /// POST /characters/equip
    /// Sets the active character on the backend and mirrors it into PlayerPrefs.
    /// </summary>
    public IEnumerator EquipCharacter(
        string characterId,
        Action<string> onSuccess = null,
        Action<string> onError = null)
    {
        if (!GameApiManager.Instance.IsLoggedIn) { onError?.Invoke("Not logged in."); yield break; }

        var body = new EquipCharacterRequest { characterId = characterId };

        yield return StartCoroutine(ApiClient.Instance.Post(
            "/characters/equip",
            body,
            onSuccess: json =>
            {
                var resp = JsonUtility.FromJson<EquipCharacterResponse>(json);
                if (resp != null && resp.success)
                {
                    string equipped = resp.data?.equippedCharacter ?? characterId;
                    PlayerPrefs.SetString("EquippedCharacter", equipped);
                    PlayerPrefs.Save();
                    onSuccess?.Invoke(equipped);
                }
                else
                    onError?.Invoke(resp?.message ?? "Equip failed.");
            },
            onError: onError ?? (e => Debug.LogWarning(e)),
            requiresAuth: true
        ));
    }

    // ─── Legacy wrappers kept for scene compatibility ────────
    public IEnumerator GetAvailableSkins(Action<CharacterItem[]> onSuccess, Action<string> onError = null)
    {
        yield return StartCoroutine(GetAvailableCharacters(onSuccess, onError));
    }

    public IEnumerator EquipSkin(string skinId, Action<string> onSuccess = null, Action<string> onError = null)
    {
        yield return StartCoroutine(EquipCharacter(skinId, onSuccess, onError));
    }
}
