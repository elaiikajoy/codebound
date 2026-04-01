// ============================================================
// 1. Script Name: TerminalLevelController.cs
// 2. Purpose: Manages the coding terminal UI, validates the player's submitted C# code against JSON test cases, and syncs progress.
// 3. Unity Setup Instructions:
//    - Attach to: The main Canvas or a manager object in the scene.
//    - Required Components: A UI Canvas containing the Terminal Modal with TMP elements.
//    - Inspector Links:
//        - Set 'Scene Level Number' to match the level of this scene (e.g. 1 for Level1, 2 for Level2).
//        - Link all UI Text/Input elements (Title, Objective, etc.).
//        - Add functions to "On Level Solved" event (e.g. deactivate computer, open door).
// ============================================================

using TMPro;
using UnityEngine;
using UnityEngine.Events;
using System;
using System.Text.RegularExpressions;
using UnityEngine.EventSystems;

public class TerminalLevelController : MonoBehaviour
{
    public static event Action<bool> OnTerminalModalVisibilityChanged;

    [Header("UI")]
    [SerializeField] private GameObject terminalModal;
    [SerializeField] private TMP_Text levelTitleText;
    [SerializeField] private TMP_Text objectiveText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_InputField codeInputField;
    [SerializeField] private TMP_Text outputText;

    [Header("Level Assignment")]
    [Tooltip("Set this to the level number of THIS scene. e.g. Level1 scene = 1, Level2 scene = 2. This is NOT the player's save progress.")]
    [SerializeField] private int sceneLevelNumber = 1;

    [Header("Run Settings")]
    [SerializeField] private int fallbackTokenReward = 100;
    [SerializeField] private bool autoRunUsesSimpleValidation = true;
    [SerializeField] private bool advanceToNextLevelOnSuccess = true;
    [SerializeField] private bool requireBackendSyncBeforeClose = false;

    [Header("Events")]
    [Tooltip("Fired when the player successfully completes the level code, allowing Editor integration for opening doors or destroying terminals.")]
    public UnityEvent onLevelSolved;

    private int activeLevelNumber;
    private LevelData activeLevelData;

    public bool IsTerminalOpen => terminalModal != null && terminalModal.activeSelf;

    private void Start()
    {
        // Ensure the terminal modal is hidden at scene start.
        // The modal is opened only when the player interacts (presses E)
        // via TerminalTriggerZone -> OpenCurrentLevel().
        SetupCodeInputField();

        if (terminalModal != null)
        {
            terminalModal.SetActive(false);
            NotifyTerminalModalVisibilityChanged(false);
        }
        else
        {
            Debug.LogError("[TerminalLevelController] 'terminalModal' is UNASSIGNED in the Inspector! Assign the Terminal Panel UI object.");
        }

        // If the level number is not set in the inspector, attempt to infer it
        // from the scene name (e.g., "Level3" -> 3). This helps avoid the
        // common mistake of forgetting to set the inspector value.
        if (sceneLevelNumber <= 0)
        {
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            var match = Regex.Match(sceneName, "(\\d+)");
            if (match.Success && int.TryParse(match.Value, out int parsed))
            {
                sceneLevelNumber = parsed;
                Debug.Log($"[TerminalLevelController] Inferred sceneLevelNumber={sceneLevelNumber} from scene name '{sceneName}'");
            }
            else
            {
                Debug.LogWarning($"[TerminalLevelController] sceneLevelNumber is not set and could not be inferred from scene name '{sceneName}'.");
            }
        }
    }

    // ----------------------------------------------------------------
    // Programmatically configure the TMP_InputField for full multi-line
    // code input — no character limit, no early wrap, no text vanishing.
    // ----------------------------------------------------------------
    private void SetupCodeInputField()
    {
        if (codeInputField == null) return;

        // Allow unlimited characters (0 = no limit).
        codeInputField.characterLimit = 0;

        // Multi-line: Enter key adds a new line, Shift+Enter submits.
        codeInputField.lineType = TMP_InputField.LineType.MultiLineNewline;

        // Fix the inner Text Area child so it stretches to fill the
        // entire input field rather than using a narrow default padding.
        Transform textAreaTransform = codeInputField.transform.Find("Text Area");
        if (textAreaTransform != null)
        {
            RectTransform textAreaRect = textAreaTransform.GetComponent<RectTransform>();
            if (textAreaRect != null)
            {
                // Stretch to fill parent completely (no padding that shrinks width).
                textAreaRect.anchorMin = Vector2.zero;
                textAreaRect.anchorMax = Vector2.one;
                textAreaRect.offsetMin = new Vector2(4f, 4f);   // 4px inner padding
                textAreaRect.offsetMax = new Vector2(-4f, -4f); // 4px inner padding
            }
        }

        // BUG FIX: enableWordWrapping was FALSE — typed text was overflowing
        // off the right edge of the field and visually disappearing ("clearing").
        // Setting to TRUE makes text wrap to the next line correctly.
        if (codeInputField.textComponent != null)
        {
            codeInputField.textComponent.enableAutoSizing = false;
            codeInputField.textComponent.enableWordWrapping = true;           // MUST be true
            codeInputField.textComponent.overflowMode = TextOverflowModes.Overflow; // never truncate
        }

        // Apply same fix to the placeholder so it also wraps properly.
        if (codeInputField.placeholder != null)
        {
            TMP_Text placeholderTMP = codeInputField.placeholder.GetComponent<TMP_Text>();
            if (placeholderTMP != null)
            {
                placeholderTMP.enableWordWrapping = true;
                placeholderTMP.overflowMode = TextOverflowModes.Overflow;
            }
        }

        Debug.Log("[TerminalLevelController] CodeInputField setup: multi-line, unlimited chars, word-wrap ON.");
    }

    public void OpenCurrentLevel()
    {
        // Always load the level assigned to THIS scene via the Inspector.
        // Do NOT use PlayerPrefs here — the player's save progress is separate
        // from which level problem this terminal shows. Each scene's terminal
        // has its own fixed sceneLevelNumber.
        OpenForLevel(sceneLevelNumber);
    }

    public void OpenForLevel(int levelNumber)
    {
        Debug.Log($"[TerminalLevelController] OpenForLevel called for level: {levelNumber}");

        activeLevelNumber = Mathf.Clamp(levelNumber, 1, 100);

        // Keep the inspector-level number in sync so any code paths that
        // rely on sceneLevelNumber (like progress sync) have a valid value.
        if (sceneLevelNumber != activeLevelNumber)
        {
            sceneLevelNumber = activeLevelNumber;
            Debug.Log($"[TerminalLevelController] Corrected sceneLevelNumber to {sceneLevelNumber} (OpenForLevel called with {levelNumber}).");
        }

        activeLevelData = LevelDataLoader.LoadLevel(activeLevelNumber);
        if (activeLevelData == null)
        {
            Debug.LogError("[TerminalLevelController] Failed to load JSON level data!");
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
            outputText.gameObject.SetActive(false); // Hide output initially
        }

        if (codeInputField != null)
        {
            codeInputField.gameObject.SetActive(true); // Ensure input is visible initially
        }

        if (terminalModal != null)
        {
            Debug.Log("[TerminalLevelController] Activating terminalModal UI Panel...");
            terminalModal.SetActive(true);
            NotifyTerminalModalVisibilityChanged(true);
        }
        else
        {
            Debug.LogError("[TerminalLevelController] Cannot open UI: 'terminalModal' is UNASSIGNED in the Inspector!");
        }

        // Ensure an EventSystem exists so UI input can receive focus/clicks.
        if (EventSystem.current == null)
        {
            Debug.LogWarning("[TerminalLevelController] No EventSystem found — creating one to enable UI input.");
            var esObj = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            EventSystem.current = esObj.GetComponent<EventSystem>();
        }

        // Make sure the input field is interactable and focused so the player
        // can immediately type when the terminal opens.
        if (codeInputField != null)
        {
            codeInputField.interactable = true;
            // Select and activate the TMP input field
            EventSystem.current.SetSelectedGameObject(codeInputField.gameObject);
            codeInputField.Select();
            codeInputField.ActivateInputField();
        }
    }

    public void CloseTerminalModal()
    {
        if (terminalModal != null)
        {
            terminalModal.SetActive(false);
            NotifyTerminalModalVisibilityChanged(false);
        }
    }

    private void NotifyTerminalModalVisibilityChanged(bool visible)
    {
        OnTerminalModalVisibilityChanged?.Invoke(visible);
    }

    // Hook this to your Retry button in Unity inspector.
    public void OnRetryPressed()
    {
        if (activeLevelData != null && codeInputField != null)
        {
            codeInputField.text = activeLevelData.starterCode; // Reset to original starter code
            codeInputField.gameObject.SetActive(true); // Show the coding input box again
        }
        if (outputText != null)
        {
            outputText.text = string.Empty; // Clear any existing errors/messages
            outputText.gameObject.SetActive(false); // Hide output text
        }
    }

    // Hook this to your Run button in Unity inspector.
    public void OnRunPressed()
    {
        if (codeInputField != null)
        {
            codeInputField.gameObject.SetActive(false); // Hide coding input box
        }
        if (outputText != null)
        {
            outputText.gameObject.SetActive(true); // Show output text
        }

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

        if (!hasCodeErrors)
        {
            // Ensure the next level is unlocked locally immediately, even if
            // backend sync fails or is interrupted by a scene change.
            UnlockNextLevelLocally();
        }

        HandleRunResult(hasCodeErrors);
    }

    private void UnlockNextLevelLocally()
    {
        int completedLevel = activeLevelNumber > 0 ? activeLevelNumber : sceneLevelNumber;
        if (completedLevel <= 0)
            return;

        // If keys don't exist yet, seed them from the completed level so
        // progression continues naturally instead of using a hard-coded 1.
        int current = PlayerPrefs.HasKey("CurrentLevel")
            ? PlayerPrefs.GetInt("CurrentLevel")
            : completedLevel;

        int highest = PlayerPrefs.HasKey("HighestLevel")
            ? PlayerPrefs.GetInt("HighestLevel")
            : completedLevel;

        int nextLevel = completedLevel + 1;
        int newCurrent = Mathf.Max(current, nextLevel);
        int newHighest = Mathf.Max(highest, nextLevel);

        PlayerPrefs.SetInt("CurrentLevel", newCurrent);
        PlayerPrefs.SetInt("HighestLevel", newHighest);
        PlayerPrefs.Save();

        Debug.Log($"[TerminalLevelController] Locally unlocked next level: CurrentLevel={newCurrent}, HighestLevel={newHighest} (completed {completedLevel}).");
    }

    private bool ValidateSubmittedCode(string submittedCode, out string message)
    {
        if (string.IsNullOrWhiteSpace(submittedCode))
        {
            message = "Code is empty. Write your solution first.";
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

        string predictedOutput = ExtractPredictedOutput(codeInputField != null ? codeInputField.text : string.Empty);
        StartCoroutine(SuccessCountdownRoutine(predictedOutput));
    }

    private System.Collections.IEnumerator SuccessCountdownRoutine(string rawOutput)
    {
        string baseSuccessText = "Success! Level completed.\n\n";

        if (!string.IsNullOrWhiteSpace(rawOutput))
        {
            baseSuccessText += $"Output: {rawOutput}\n\n";
        }

        if (outputText != null)
        {
            outputText.text = baseSuccessText + "Closing terminal in 3...";
        }

        for (int i = 3; i > 0; i--)
        {
            if (outputText != null)
            {
                outputText.text = baseSuccessText + $"Closing terminal in {i}...";
            }
            yield return new WaitForSeconds(1f);
        }

        // ── Backend progress sync ─────────────────────────────────────────
        // POST /progress/update for the currently logged-in user.
        // Uses GameApiManager.CurrentUser to identify the player —
        // whoever is logged in is the player currently playing this level.
        // The sync is non-blocking: if not logged in or the request fails,
        // the game flow still continues and the door still opens.
        int tokensEarned = activeLevelData != null ? activeLevelData.baseTokenReward : fallbackTokenReward;
        bool syncDone = false;
        bool syncSuccess = false;

        if (outputText != null)
            outputText.text = baseSuccessText + "Saving progress...";

        int levelToSync = activeLevelNumber > 0 ? activeLevelNumber : sceneLevelNumber;

        if (levelToSync <= 0)
        {
            Debug.LogError($"[TerminalLevelController] Invalid levelToSync ({levelToSync}) — cannot sync progress.");
            syncDone = true;
            syncSuccess = false;
        }
        else
        {
            Debug.Log($"[TerminalLevelController] Syncing progress to backend: level={levelToSync}, tokens={tokensEarned}");
            ProgressService.SyncAfterLevel(
                levelNumber: levelToSync,
                tokensEarned: tokensEarned,
                onSuccess: _ => { syncDone = true; syncSuccess = true; },
                onError: _ => { syncDone = true; syncSuccess = false; }
            );
        }

        // Wait up to 5 seconds for the HTTP response.
        float elapsed = 0f;
        while (!syncDone && elapsed < 5f)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (outputText != null)
        {
            outputText.text = syncSuccess
                ? baseSuccessText + "Progress saved!"
                : baseSuccessText + "Progress saved locally.";
        }

        yield return new WaitForSeconds(0.8f);
        // ─────────────────────────────────────────────────────────────────

        onLevelSolved?.Invoke();
        CloseTerminalModal();
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

        string normalizedPredicted = Normalize(predicted);
        string normalizedExpected = Normalize(expected);

        // Debug: log exact values so mismatches can be traced in the Console.
        Debug.Log($"[TerminalLevelController] Output check — Expected: '{normalizedExpected}' | Found: '{normalizedPredicted}'");

        // BUG FIX: Use OrdinalIgnoreCase instead of Ordinal.
        // Ordinal was case-sensitive AND would fail on invisible Unicode chars
        // (zero-width spaces, BOM, RTL marks) that TMP_InputField silently injects.
        if (!normalizedPredicted.Equals(normalizedExpected, System.StringComparison.OrdinalIgnoreCase))
        {
            message = $"Output mismatch. Expected: {expected} | Found: {predicted}";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private string ExtractPredictedOutput(string code)
    {
        // Find a print call: System.out.println(...), System.out.print(...), or print(...)
        Match call = Regex.Match(code, @"System\.out\.(?:println|print)\s*\(\s*(?<expr>.*?)\s*\)", RegexOptions.Singleline);
        if (!call.Success)
            call = Regex.Match(code, @"\bprint\s*\(\s*(?<expr>.*?)\s*\)", RegexOptions.Singleline);

        if (!call.Success)
            return string.Empty;

        string expr = call.Groups["expr"].Value.Trim();

        // 1) Exact string literal: "text"
        Match strLit = Regex.Match(expr, "^\"(?<txt>[\\s\\S]*)\"$");
        if (strLit.Success)
            return strLit.Groups["txt"].Value;

        // 2) Numeric literal (integer or float, allow negative)
        Match numLit = Regex.Match(expr, "^(?<num>-?\\d+(?:\\.\\d+)?)$");
        if (numLit.Success)
            return numLit.Groups["num"].Value;

        // 3) Expression with concatenation using + — collect tokens
        // Tokenize: string literals, numbers, identifiers
        var tokenRegex = new Regex("\"(?<str>[^\"]*)\"|(?<num>-?\\d+(?:\\.\\d+)?)|(?<id>[A-Za-z_][A-Za-z0-9_]*)");
        var matches = tokenRegex.Matches(expr);
        if (matches.Count > 0)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            bool any = false;
            foreach (Match mm in matches)
            {
                if (mm.Groups["str"].Success)
                {
                    sb.Append(mm.Groups["str"].Value);
                    any = true;
                    continue;
                }
                if (mm.Groups["num"].Success)
                {
                    sb.Append(mm.Groups["num"].Value);
                    any = true;
                    continue;
                }
                if (mm.Groups["id"].Success)
                {
                    string id = mm.Groups["id"].Value;
                    // Look for a literal assignment to this identifier earlier in the code
                    var assign = Regex.Match(code, $"\\b(?:int|long|float|double|var)?\\s*{Regex.Escape(id)}\\s*=\\s*(?<val>-?\\d+(?:\\.\\d+)?)\\s*;",
                        RegexOptions.Singleline);
                    if (assign.Success)
                    {
                        sb.Append(assign.Groups["val"].Value);
                        any = true;
                        continue;
                    }
                    // Also try simple assignment without type
                    assign = Regex.Match(code, $"\\b{Regex.Escape(id)}\\s*=\\s*(?<val>-?\\d+(?:\\.\\d+)?)\\s*;", RegexOptions.Singleline);
                    if (assign.Success)
                    {
                        sb.Append(assign.Groups["val"].Value);
                        any = true;
                        continue;
                    }
                    // If identifier can't be resolved, we can't predict output
                    return string.Empty;
                }
            }

            if (any)
                return sb.ToString();
        }

        // 4) Single identifier: try to resolve assignment
        var idOnly = Regex.Match(expr, "^(?<id>[A-Za-z_][A-Za-z0-9_]*)$");
        if (idOnly.Success)
        {
            string id = idOnly.Groups["id"].Value;
            var assign = Regex.Match(code, $"\\b(?:int|long|float|double|var)?\\s*{Regex.Escape(id)}\\s*=\\s*(?<val>-?\\d+(?:\\.\\d+)?)\\s*;", RegexOptions.Singleline);
            if (assign.Success)
                return assign.Groups["val"].Value;
            assign = Regex.Match(code, $"\\b{Regex.Escape(id)}\\s*=\\s*(?<val>-?\\d+(?:\\.\\d+)?)\\s*;", RegexOptions.Singleline);
            if (assign.Success)
                return assign.Groups["val"].Value;
        }

        // Unknown/unhandled expression
        return string.Empty;


    }

    // -------------------------------------------------------
    // Normalizes text for comparison:
    // - Strips invisible Unicode chars TMP_InputField can inject
    //   (zero-width spaces U+200B, BOM U+FEFF, soft hyphens, etc.)
    // - Normalises line endings
    // - Trims outer whitespace
    // -------------------------------------------------------
    private string Normalize(string s)
    {
        if (s == null) return string.Empty;

        // Remove invisible/control Unicode characters that TMP may inject.
        System.Text.StringBuilder sb = new System.Text.StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            // Keep only printable characters (visible text + standard whitespace).
            if (c == '\n' || c == '\r' || c == '\t' || (!char.IsControl(c) && !char.GetUnicodeCategory(c).Equals(System.Globalization.UnicodeCategory.OtherNotAssigned) && c != '\u200B' && c != '\uFEFF' && c != '\u00AD'))
            {
                sb.Append(c);
            }
        }

        return sb.ToString()
                 .Replace("\r\n", "\n")
                 .Replace("\r", "\n")
                 .Trim();
    }
}
