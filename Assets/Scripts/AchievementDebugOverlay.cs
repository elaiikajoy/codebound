// ============================================================
// AchievementDebugOverlay.cs
// Purpose: Small in-game debug overlay that explains why a claim
//          button is disabled for each achievement row.
//
// Unity Setup:
//   - Attach to a small debug panel in the Achievement scene UI.
//   - Assign a TMP_Text object to debugText.
//   - Keep it hidden by default if you only want it for testing.
// ============================================================

using System.Text;
using TMPro;
using UnityEngine;

public class AchievementDebugOverlay : MonoBehaviour
{
    [SerializeField] private TMP_Text debugText;
    [SerializeField] private bool visibleByDefault = false;
    [SerializeField] private bool showUnlockedRows = false;

    private void Awake()
    {
        SetVisible(visibleByDefault);
    }

    public void SetVisible(bool visible)
    {
        if (debugText != null)
            debugText.gameObject.SetActive(visible);
    }

    public void SetMessage(string message)
    {
        if (debugText == null)
            return;

        debugText.gameObject.SetActive(true);
        debugText.text = string.IsNullOrWhiteSpace(message) ? "Achievement debug: idle" : message;
    }

    public void Render(AchievementStateData data, AchievementRowView[] rows)
    {
        if (debugText == null)
            return;

        if (data == null)
        {
            SetMessage("Achievement debug: no data");
            return;
        }

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("Achievement Debug");
        builder.AppendLine($"Level: {data.progress?.highestLevel ?? 0} | Tokens: {data.progress?.totalTokens ?? 0}");
        builder.AppendLine($"Unlocked: {data.unlockedCount}/{data.total} | Claimable: {data.claimableCount}");

        if (rows != null)
        {
            foreach (AchievementRowView row in rows)
            {
                if (row == null || row.CurrentAchievement == null)
                    continue;

                bool showRow = showUnlockedRows || !row.CurrentAchievement.isClaimed;
                if (!showRow)
                    continue;

                builder.AppendLine($"- {row.AchievementId}: {(row.CurrentAchievement.isClaimed ? "CLAIMED" : (row.CurrentAchievement.canClaim ? "READY" : "LOCKED"))}");

                if (!row.CurrentAchievement.canClaim && !row.CurrentAchievement.isClaimed)
                {
                    string reason = string.IsNullOrWhiteSpace(row.DisabledReason) ? "Locked" : row.DisabledReason;
                    builder.AppendLine($"  reason: {reason}");
                }
            }
        }

        debugText.gameObject.SetActive(true);
        debugText.text = builder.ToString();
    }
}