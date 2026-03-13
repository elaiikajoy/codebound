// ============================================================
// SkinService.cs
// Purpose: Legacy Unity component used to sync character state
//          with the CodeBound backend.
//            GET  /characters           — current player character (protected)
//            POST /characters/equip     — set active character (protected)
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

    // ─── Available characters (local-only) ────────────────────
    /// <summary>
    /// Backend no longer serves character catalog data.
    /// Returns an empty array to preserve call compatibility.
    /// </summary>
    public IEnumerator GetAvailableCharacters(
        Action<CharacterItem[]> onSuccess,
        Action<string> onError = null)
    {
        onSuccess?.Invoke(new CharacterItem[0]);
        yield break;
    }

    // ─── Current character state (protected) ──────────────────
    /// <summary>
    /// GET /characters — returns the currently equipped character for the player.
    /// </summary>
    public IEnumerator GetCurrentCharacter(
        Action<string> onSuccess,
        Action<string> onError = null)
    {
        if (!GameApiManager.Instance.IsLoggedIn) { onError?.Invoke("Not logged in."); yield break; }

        yield return StartCoroutine(ApiClient.Instance.Get(
            "/characters",
            onSuccess: json =>
            {
                var result = JsonUtility.FromJson<ProgressResponse>(json);
                onSuccess?.Invoke(result?.data?.equippedCharacter ?? "default");
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
