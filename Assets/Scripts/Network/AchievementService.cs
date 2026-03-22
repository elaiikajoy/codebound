// ============================================================
// AchievementService.cs
// Purpose: Wraps CodeBound achievement HTTP calls for Unity.
//          - GET  /achievements/progress  -> per-user achievement state
//          - POST /achievements/claim     -> claim a reward once
//
// Unity Setup:
//   - Attach to the persistent "GameAPI" GameObject.
//   - Use AchievementPanelController to request state / claim rewards.
// ============================================================

using System;
using System.Collections;
using UnityEngine;

public class AchievementService : MonoBehaviour
{
    public static AchievementService Instance { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticsOnDomainReload()
    {
        Instance = null;
    }

    public static AchievementService GetOrFindInstance()
    {
        if (Instance != null)
            return Instance;

        Instance = FindObjectOfType<AchievementService>(true);
        return Instance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Fetches the current user's achievement state, including claimable entries.
    /// </summary>
    public IEnumerator GetAchievementState(
        Action<AchievementStateData> onSuccess,
        Action<string> onError = null)
    {
        if (GameApiManager.Instance == null || !GameApiManager.Instance.IsLoggedIn)
        {
            onError?.Invoke("Not logged in.");
            yield break;
        }

        yield return StartCoroutine(ApiClient.Instance.Get(
            "/achievements/progress",
            onSuccess: json =>
            {
                var resp = JsonUtility.FromJson<AchievementStateResponse>(json);
                if (resp != null && resp.success && resp.data != null)
                {
                    Debug.Log($"[AchievementService] GET /achievements/progress succeeded. total={resp.data.total}, claimable={resp.data.claimableCount}, unlocked={resp.data.unlockedCount}.");
                    onSuccess?.Invoke(resp.data);
                }
                else
                {
                    Debug.LogWarning($"[AchievementService] GET /achievements/progress returned invalid payload: {json}");
                    onError?.Invoke(resp?.message ?? "Failed to fetch achievement state.");
                }
            },
            onError: onError ?? (err => Debug.LogWarning(err)),
            requiresAuth: true
        ));
    }

    /// <summary>
    /// Claims one achievement reward and mirrors the returned token total into PlayerPrefs.
    /// </summary>
    public IEnumerator ClaimAchievement(
        string achievementId,
        Action<AchievementClaimData> onSuccess,
        Action<string> onError = null)
    {
        if (GameApiManager.Instance == null || !GameApiManager.Instance.IsLoggedIn)
        {
            onError?.Invoke("Not logged in.");
            yield break;
        }

        var body = new AchievementClaimRequest { achievementId = achievementId };

        Debug.Log($"[AchievementService] POST /achievements/claim started for '{achievementId}'.");

        yield return StartCoroutine(ApiClient.Instance.Post(
            "/achievements/claim",
            body,
            onSuccess: json =>
            {
                var resp = JsonUtility.FromJson<AchievementClaimResponse>(json);
                if (resp != null && resp.success && resp.data != null)
                {
                    Debug.Log($"[AchievementService] Claim response success for '{achievementId}'. rewardTokens={resp.data.rewardTokens}, totalTokens={resp.data.progress?.totalTokens ?? 0}.");
                    if (resp.data.progress != null)
                    {
                        PlayerPrefs.SetInt("CurrentLevel", resp.data.progress.currentLevel);
                        PlayerPrefs.SetInt("HighestLevel", resp.data.progress.highestLevel);
                        PlayerPrefs.SetInt("TotalTokens", resp.data.progress.totalTokens);
                        PlayerPrefs.Save();

                        TokenManager.SyncFromBackend(resp.data.progress.totalTokens);
                        Debug.Log($"[AchievementService] Local PlayerPrefs synced from backend. HighestLevel={resp.data.progress.highestLevel}, TotalTokens={resp.data.progress.totalTokens}.");
                    }

                    onSuccess?.Invoke(resp.data);
                }
                else
                {
                    Debug.LogWarning($"[AchievementService] Claim response failed or malformed for '{achievementId}': {json}");
                    onError?.Invoke(resp?.message ?? "Failed to claim achievement.");
                }
            },
            onError: onError ?? (err => Debug.LogWarning(err)),
            requiresAuth: true
        ));
    }
}