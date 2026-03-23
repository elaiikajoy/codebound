// ============================================================
// AchievementRowView.cs
// Purpose: Reusable row prefab logic for one achievement entry.
//          Put this on the row prefab and duplicate it for each item.
//
// Unity Setup:
//   - Attach to one achievement row prefab or row GameObject.
//   - Use child text objects named like Title/Description/Reward/etc.
//   - The script auto-wires references if they are left empty.
// ============================================================

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class AchievementRowView : MonoBehaviour
{
    [Header("Identity")]
    [SerializeField] private string achievementId;

    [Header("UI References")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text rewardText;
    [SerializeField] private TMP_Text requirementText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Button claimButton;

    [Header("Optional")]
    [SerializeField] private GameObject lockedOverlay;

    private Action<string> claimHandler;
    private AchievementStateItem currentAchievement;
    private string currentDisabledReason = string.Empty;
    private string runtimeAchievementId = string.Empty;

    public string AchievementId => !string.IsNullOrWhiteSpace(achievementId) ? achievementId : runtimeAchievementId;
    public bool HasExplicitAchievementId => !string.IsNullOrWhiteSpace(achievementId);
    public string DisabledReason => currentDisabledReason;
    public AchievementStateItem CurrentAchievement => currentAchievement;

    private void Awake()
    {
        AutoWireReferences();
    }

    private void OnValidate()
    {
        AutoWireReferences();
    }

    private void OnEnable()
    {
        if (claimButton != null)
        {
            claimButton.onClick.RemoveListener(HandleClaimClicked);
            claimButton.onClick.AddListener(HandleClaimClicked);
        }
    }

    private void OnDisable()
    {
        if (claimButton != null)
        {
            claimButton.onClick.RemoveListener(HandleClaimClicked);
        }
    }

    public void SetClaimHandler(Action<string> handler)
    {
        claimHandler = handler;
    }

    /// <summary>
    /// Allows controller-driven setup for legacy scenes where each claim button
    /// exists but rows were not fully wired in the inspector.
    /// </summary>
    public void ConfigureRuntime(string achievementIdOverride, Button claimButtonOverride = null)
    {
        runtimeAchievementId = achievementIdOverride ?? string.Empty;

        if (claimButtonOverride != null)
        {
            claimButton = claimButtonOverride;
            claimButton.onClick.RemoveListener(HandleClaimClicked);
            claimButton.onClick.AddListener(HandleClaimClicked);
        }
    }

    public void ApplyState(AchievementStateItem achievement, string disabledReason)
    {
        currentAchievement = achievement;
        currentDisabledReason = disabledReason ?? string.Empty;

        if (achievement == null)
        {
            SetMissingState();
            return;
        }

        if (titleText != null)
            titleText.text = achievement.title;

        if (descriptionText != null)
            descriptionText.text = achievement.description;

        if (rewardText != null)
            rewardText.text = $"CLAIM {achievement.rewardTokens}";

        if (requirementText != null)
            requirementText.text = BuildRequirementText(achievement);

        if (statusText != null)
            statusText.text = achievement.isClaimed ? "CLAIMED" : (achievement.canClaim ? "READY" : "LOCKED");

        if (claimButton != null)
            claimButton.interactable = achievement.canClaim;

        if (lockedOverlay != null)
            lockedOverlay.SetActive(!achievement.canClaim);
    }

    public void SetLocked(string message)
    {
        currentAchievement = null;
        currentDisabledReason = message ?? string.Empty;

        if (statusText != null)
            statusText.text = "LOCKED";

        if (requirementText != null)
            requirementText.text = currentDisabledReason;

        if (claimButton != null)
            claimButton.interactable = false;

        if (lockedOverlay != null)
            lockedOverlay.SetActive(true);
    }

    private void HandleClaimClicked()
    {
        if (claimHandler == null || claimButton == null || !claimButton.interactable)
            return;

        string idToClaim = AchievementId;
        if (!string.IsNullOrWhiteSpace(idToClaim))
        {
            Debug.Log($"[AchievementRowView] Claim button clicked for '{idToClaim}'.");
            claimHandler.Invoke(idToClaim);
        }
    }

    private void SetMissingState()
    {
        if (titleText != null)
            titleText.text = "Missing achievement";

        if (descriptionText != null)
            descriptionText.text = "This row is not linked to a backend achievement.";

        if (rewardText != null)
            rewardText.text = string.Empty;

        if (requirementText != null)
            requirementText.text = "Check the row prefab or achievementId.";

        if (statusText != null)
            statusText.text = "LOCKED";

        if (claimButton != null)
            claimButton.interactable = false;

        if (lockedOverlay != null)
            lockedOverlay.SetActive(true);
    }

    private string BuildRequirementText(AchievementStateItem achievement)
    {
        if (achievement.requiredHighestLevel > 0)
            return $"Requires Level {achievement.requiredHighestLevel}";

        if (achievement.requiredTotalTokens > 0)
            return $"Requires {achievement.requiredTotalTokens:N0} Tokens";

        return string.Empty;
    }

    private void AutoWireReferences()
    {
        if (titleText == null) titleText = FindTextByName("Title");
        if (descriptionText == null) descriptionText = FindTextByName("Description");
        if (rewardText == null) rewardText = FindTextByName("Reward");
        if (requirementText == null) requirementText = FindTextByName("Requirement");
        if (statusText == null) statusText = FindTextByName("Status");
        if (claimButton == null) claimButton = FindButtonByName("Claim");
        if (lockedOverlay == null) lockedOverlay = FindGameObjectByName("Locked");
    }

    private TMP_Text FindTextByName(string nameFragment)
    {
        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        foreach (TMP_Text text in texts)
        {
            if (text != null && text.name.IndexOf(nameFragment, StringComparison.OrdinalIgnoreCase) >= 0)
                return text;
        }

        return null;
    }

    private Button FindButtonByName(string nameFragment)
    {
        Button[] buttons = GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
        {
            if (button != null && button.name.IndexOf(nameFragment, StringComparison.OrdinalIgnoreCase) >= 0)
                return button;
        }

        return null;
    }

    private GameObject FindGameObjectByName(string nameFragment)
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        foreach (Transform child in children)
        {
            if (child != null && child.name.IndexOf(nameFragment, StringComparison.OrdinalIgnoreCase) >= 0)
                return child.gameObject;
        }

        return null;
    }
}