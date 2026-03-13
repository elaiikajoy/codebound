using TMPro;
using UnityEngine;
using System.Text.RegularExpressions;

public class TerminalLevelController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject terminalModal;
    [SerializeField] private TMP_Text levelTitleText;
    [SerializeField] private TMP_Text objectiveText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_InputField codeInputField;
    [SerializeField] private TMP_Text outputText;

    [Header("Run Settings")]
    [SerializeField] private int fallbackTokenReward = 100;
    [SerializeField] private bool autoRunUsesSimpleValidation = true;
    [SerializeField] private bool advanceToNextLevelOnSuccess = true;
    [SerializeField] private bool requireBackendSyncBeforeClose = true;

    private int activeLevelNumber;
    private LevelData activeLevelData;

    private void Start()
    {
        OpenCurrentLevel();
    }

    public void OpenCurrentLevel()
    {
        int currentLevel = PlayerPrefs.GetInt("CurrentLevel", 1);
        OpenForLevel(currentLevel);
    }

    public void OpenForLevel(int levelNumber)
    {
        activeLevelNumber = Mathf.Clamp(levelNumber, 1, 100);
        activeLevelData = LevelDataLoader.LoadLevel(activeLevelNumber);

        if (activeLevelData == null)
        {
            if (outputText != null)
            {
                outputText.text = "Failed to load level data.";
            }
            return;
        }

        if (levelTitleText != null)
        {
            levelTitleText.text = activeLevelData.levelName;
        }

        if (objectiveText != null)
        {
            objectiveText.text = activeLevelData.objective;
        }

        if (descriptionText != null)
        {
            descriptionText.text = activeLevelData.puzzleDescription;
        }

        if (codeInputField != null)
        {
            codeInputField.text = activeLevelData.starterCode;
        }

        if (outputText != null)
        {
            outputText.text = string.Empty;
        }

        if (terminalModal != null)
        {
            terminalModal.SetActive(true);
        }
    }

    public void CloseTerminalModal()
    {
        if (terminalModal != null)
        {
            terminalModal.SetActive(false);
        }
    }

    // Hook this to your Run button in Unity inspector.
    public void OnRunPressed()
    {
        if (activeLevelData == null)
        {
            if (outputText != null)
            {
                outputText.text = "No level loaded.";
            }
            return;
        }

        string submittedCode = codeInputField != null ? codeInputField.text : string.Empty;

        bool hasCodeErrors = false;
        string validationMessage = string.Empty;

        if (autoRunUsesSimpleValidation)
        {
            hasCodeErrors = !ValidateSubmittedCode(submittedCode, out validationMessage);

            if (!hasCodeErrors && !ValidateExpectedOutput(submittedCode, out validationMessage))
            {
                hasCodeErrors = true;
            }
        }

        if (outputText != null)
        {
            outputText.text = hasCodeErrors
                ? validationMessage
                : "Code validation passed. Completing level...";
        }

        HandleRunResult(hasCodeErrors);
    }

    private bool ValidateSubmittedCode(string submittedCode, out string message)
    {
        if (string.IsNullOrWhiteSpace(submittedCode))
        {
            message = "Code is empty. Write your solution first.";
            return false;
        }

        if (submittedCode.Contains("TODO"))
        {
            message = "Remove TODO comments and complete your code.";
            return false;
        }

        if (!HasBalancedPairs(submittedCode))
        {
            message = "Syntax check failed: unbalanced braces or parentheses.";
            return false;
        }

        if (activeLevelData != null)
        {
            if (!ContainsRequiredKeywords(submittedCode, activeLevelData.requiredKeywords, out message))
            {
                return false;
            }

            if (ContainsForbiddenKeywords(submittedCode, activeLevelData.forbiddenKeywords, out message))
            {
                return false;
            }

            if (activeLevelData.testCases != null)
            {
                for (int i = 0; i < activeLevelData.testCases.Length; i++)
                {
                    LevelTestCase testCase = activeLevelData.testCases[i];
                    if (testCase == null || testCase.requiredKeywords == null) continue;

                    if (!ContainsRequiredKeywords(submittedCode, testCase.requiredKeywords, out message))
                    {
                        message = $"Test case {i + 1} failed: {message}";
                        return false;
                    }
                }
            }
        }

        message = "Success.";
        return true;
    }

    private bool ContainsRequiredKeywords(string code, string[] requiredKeywords, out string message)
    {
        if (requiredKeywords == null || requiredKeywords.Length == 0)
        {
            message = string.Empty;
            return true;
        }

        string lowerCode = code.ToLowerInvariant();
        for (int i = 0; i < requiredKeywords.Length; i++)
        {
            string keyword = requiredKeywords[i];
            if (string.IsNullOrWhiteSpace(keyword)) continue;

            if (!lowerCode.Contains(keyword.ToLowerInvariant()))
            {
                message = $"Missing required keyword: {keyword}";
                return false;
            }
        }

        message = string.Empty;
        return true;
    }

    private bool ContainsForbiddenKeywords(string code, string[] forbiddenKeywords, out string message)
    {
        if (forbiddenKeywords == null || forbiddenKeywords.Length == 0)
        {
            message = string.Empty;
            return false;
        }

        string lowerCode = code.ToLowerInvariant();
        for (int i = 0; i < forbiddenKeywords.Length; i++)
        {
            string keyword = forbiddenKeywords[i];
            if (string.IsNullOrWhiteSpace(keyword)) continue;

            if (lowerCode.Contains(keyword.ToLowerInvariant()))
            {
                message = $"Forbidden keyword used: {keyword}";
                return true;
            }
        }

        message = string.Empty;
        return false;
    }

    private bool HasBalancedPairs(string code)
    {
        int braces = 0;
        int parens = 0;
        int brackets = 0;

        for (int i = 0; i < code.Length; i++)
        {
            char c = code[i];
            if (c == '{') braces++;
            if (c == '}') braces--;
            if (c == '(') parens++;
            if (c == ')') parens--;
            if (c == '[') brackets++;
            if (c == ']') brackets--;

            if (braces < 0 || parens < 0 || brackets < 0)
            {
                return false;
            }
        }

        return braces == 0 && parens == 0 && brackets == 0;
    }

    // Call this method from your real compiler/validator pipeline.
    public void HandleRunResult(bool hasCodeErrors)
    {
        if (hasCodeErrors)
        {
            if (outputText != null && string.IsNullOrWhiteSpace(outputText.text))
            {
                outputText.text = "Code has errors. Fix your code and run again.";
            }
            return;
        }

        int tokensEarned = activeLevelData != null ? activeLevelData.baseTokenReward : fallbackTokenReward;

        if (!requireBackendSyncBeforeClose)
        {
            ApplyLocalAdvance();
            if (outputText != null)
            {
                outputText.text = "Success! Level completed.";
            }
            CloseTerminalModal();
            return;
        }

        if (outputText != null)
        {
            outputText.text = "Validation passed. Syncing progress...";
        }

        ProgressService.SyncAfterLevel(
            levelNumber: activeLevelNumber,
            tokensEarned: tokensEarned,
            timeSpent: 0f,
            hintsUsed: 0,
            isPerfect: true,
            onSuccess: _ =>
            {
                ApplyLocalAdvance();
                if (outputText != null)
                {
                    outputText.text = "Success! Level completed.";
                }
                CloseTerminalModal();
            },
            onError: err =>
            {
                if (outputText != null)
                {
                    outputText.text = $"Progress sync failed: {err}";
                }
            }
        );
    }

    private void ApplyLocalAdvance()
    {
        if (!advanceToNextLevelOnSuccess)
        {
            return;
        }

        int highestLevel = PlayerPrefs.GetInt("HighestLevel", 1);
        int nextLevel = Mathf.Clamp(activeLevelNumber + 1, 1, 100);

        PlayerPrefs.SetInt("CurrentLevel", nextLevel);
        PlayerPrefs.SetInt("HighestLevel", Mathf.Max(highestLevel, nextLevel));
        PlayerPrefs.Save();
    }

    private bool ValidateExpectedOutput(string submittedCode, out string message)
    {
        string expected = string.Empty;

        if (activeLevelData != null)
        {
            if (!string.IsNullOrWhiteSpace(activeLevelData.expectedOutput))
            {
                expected = activeLevelData.expectedOutput;
            }
            else if (activeLevelData.testCases != null && activeLevelData.testCases.Length > 0)
            {
                for (int i = 0; i < activeLevelData.testCases.Length; i++)
                {
                    if (!string.IsNullOrWhiteSpace(activeLevelData.testCases[i].expectedOutput))
                    {
                        expected = activeLevelData.testCases[i].expectedOutput;
                        break;
                    }
                }
            }
        }

        if (string.IsNullOrWhiteSpace(expected))
        {
            message = string.Empty;
            return true;
        }

        string predicted = ExtractPredictedOutput(submittedCode);
        if (string.IsNullOrWhiteSpace(predicted))
        {
            message = "Expected output check failed: no recognizable output statement found.";
            return false;
        }

        if (!Normalize(predicted).Equals(Normalize(expected), System.StringComparison.Ordinal))
        {
            message = $"Output mismatch. Expected: {expected} | Found: {predicted}";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private string ExtractPredictedOutput(string code)
    {
        Match m = Regex.Match(code, "System\\.out\\.println\\s*\\(\\s*\"(?<txt>[^\"]+)\"\\s*\\)");
        if (m.Success)
        {
            return m.Groups["txt"].Value;
        }

        Match alt = Regex.Match(code, "print\\s*\\(\\s*\"(?<txt>[^\"]+)\"\\s*\\)");
        if (alt.Success)
        {
            return alt.Groups["txt"].Value;
        }

        return string.Empty;
    }

    private string Normalize(string s)
    {
        return (s ?? string.Empty).Replace("\r\n", "\n").Replace("\r", "\n").Trim();
    }
}
