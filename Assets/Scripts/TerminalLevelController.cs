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
using UnityEngine.UI;
using UnityEngine.Events;
using System;
using System.Collections.Generic;
using UnityEngine.EventSystems;

public class TerminalLevelController : MonoBehaviour
{
    public TMP_Text achievementText;

    private List<string> _currentDynamicPrompts = new List<string>();
    public static event Action<bool> OnTerminalModalVisibilityChanged;

    [Header("UI")]
    [SerializeField] private GameObject terminalModal;
    [SerializeField] private TMP_Text levelTitleText;
    [SerializeField] private TMP_Text objectiveText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_InputField codeInputField;
    [SerializeField] private TMP_Text outputText;
    [SerializeField] private TMP_InputField consoleInputField; // NEW: For Interactive Input
    [SerializeField] private Button retryButton;

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
        SetupConsoleInputField();

        if (consoleInputField != null)
        {
            consoleInputField.gameObject.SetActive(false);
            // We handle submit manually in Update() to avoid Unity TMP bugs with Shift+Enter
            // consoleInputField.onSubmit.AddListener(OnConsoleInputSubmitted);
        }

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
            if (TryParseLevelNumberFromSceneName(sceneName, out int parsed))
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

    private bool TryParseLevelNumberFromSceneName(string sceneName, out int parsedLevel)
    {
        parsedLevel = 0;
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return false;
        }

        int start = -1;
        for (int i = 0; i < sceneName.Length; i++)
        {
            if (char.IsDigit(sceneName[i]))
            {
                start = i;
                break;
            }
        }

        if (start < 0)
        {
            return false;
        }

        int end = start;
        while (end < sceneName.Length && char.IsDigit(sceneName[end]))
        {
            end++;
        }

        return int.TryParse(sceneName.Substring(start, end - start), out parsedLevel);
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

    private void SetupConsoleInputField()
    {
        if (consoleInputField == null) return;

        // We use MultiLineNewline so Shift+Enter AND Enter both naturally add a new line without triggering buggy TMP onSubmit events.
        // We will manually intercept the Enter key (without shift) in Update() to submit the code.
        consoleInputField.lineType = TMP_InputField.LineType.MultiLineNewline;
        consoleInputField.characterLimit = 0;

        if (consoleInputField.textComponent != null)
        {
            consoleInputField.textComponent.enableAutoSizing = false;
            consoleInputField.textComponent.enableWordWrapping = true;
            consoleInputField.textComponent.overflowMode = TextOverflowModes.Overflow;
        }

        if (consoleInputField.placeholder != null)
        {
            TMP_Text placeholderTMP = consoleInputField.placeholder.GetComponent<TMP_Text>();
            if (placeholderTMP != null)
            {
                placeholderTMP.enableWordWrapping = true;
                placeholderTMP.overflowMode = TextOverflowModes.Overflow;
                placeholderTMP.text = "Enter input here then click enter for the next line input...";
            }
        }

        Debug.Log("[TerminalLevelController] ConsoleInputField setup: multi-line newline. Submit handled in Update().");
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
            codeInputField.interactable = true;
        }

        SetRetryInteractable(true);

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

    private void SetRetryInteractable(bool enabled)
    {
        if (retryButton != null)
        {
            retryButton.interactable = enabled;
        }
    }

    // Hook this to your Retry button in Unity inspector.
    public void OnRetryPressed()
    {
        if (codeInputField != null)
        {
            // Keep user's current code; retry should not reset to starter code.
            codeInputField.gameObject.SetActive(true);
            codeInputField.interactable = true;
        }

        if (outputText != null)
        {
            outputText.text = string.Empty;
            outputText.gameObject.SetActive(false);
        }

        if (consoleInputField != null)
        {
            consoleInputField.gameObject.SetActive(false);
            consoleInputField.text = string.Empty;
        }

        SetRetryInteractable(true);

        if (codeInputField != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(codeInputField.gameObject);
            codeInputField.Select();
            codeInputField.ActivateInputField();
        }
    }

    // Hook this to your Run button in Unity inspector.
    public void OnRunPressed()
    {
        // If we're currently in interactive input mode, use Run as submit.
        if (consoleInputField != null && consoleInputField.gameObject.activeInHierarchy)
        {
            OnConsoleInputSubmitted(consoleInputField.text);
            return;
        }

        // Hide the code input field immediately — after Run is pressed,
        // only the output area is shown. The user either sees success or
        // an error with a Retry button. Retry restores the field.
        if (codeInputField != null)
        {
            codeInputField.gameObject.SetActive(false);
        }

        if (outputText != null)
        {
            outputText.gameObject.SetActive(true);
        }

        if (activeLevelData == null)
        {
            if (outputText != null)
            {
                outputText.text = "No level loaded.";
            }
            SetRetryInteractable(true);
            return;
        }

        string submittedCode = codeInputField != null ? codeInputField.text : string.Empty;

        TerminalSubmissionResult analysis = TerminalSubmissionAnalyzer.Analyze(submittedCode, activeLevelData, sceneLevelNumber, string.Empty);

        if (!analysis.IsValid)
        {
            if (outputText != null)
            {
                outputText.text = analysis.Message;
            }

            // Keep code field hidden — Retry button brings it back.
            SetRetryInteractable(true);

            HandleRunResult(true, string.Empty);
            return;
        }

        if (analysis.RequiresInput || LevelRequiresInput())
        {
            EnterDataInjectionPhase();
            return;
        }

        if (outputText != null)
        {
            outputText.text = "Code validation passed. Completing level...";
        }

        SetRetryInteractable(false);

        HandleRunResult(false, analysis.PredictedOutput);
    }

    private bool LevelRequiresInput()
    {
        if (activeLevelData == null)
        {
            return false;
        }

        string submittedCode = codeInputField != null ? codeInputField.text : string.Empty;
        TerminalSubmissionResult analysis = TerminalSubmissionAnalyzer.Analyze(submittedCode, activeLevelData, sceneLevelNumber, string.Empty);
        return analysis.RequiresInput;
    }

    private void EnterDataInjectionPhase()
    {
        if (consoleInputField != null)
        {
            consoleInputField.gameObject.SetActive(true);
            if (activeLevelData != null && (activeLevelData.levelNumber >= 21 && activeLevelData.levelNumber <= 41))
            {
                string submittedCode = codeInputField != null ? codeInputField.text : string.Empty;
                _currentDynamicPrompts.Clear();
                
                // NEW: AST-based Prompt Extraction (Option 1.5 - AST Migration)
                _currentDynamicPrompts.Clear();
                
                TerminalLexer lexer = new TerminalLexer(submittedCode);
                List<TerminalToken> tokens = lexer.Tokenize();
                TerminalParser parser = new TerminalParser(tokens);
                TerminalCompilationUnitNode ast = parser.ParseCompilationUnit();

                if (ast != null)
                {
                    _currentDynamicPrompts = ExtractPromptsFromAst(ast);
                }

                if (_currentDynamicPrompts.Count > 0)
                {
                    consoleInputField.text = string.Join("\n", _currentDynamicPrompts);
                }
                else
                {
                    consoleInputField.text = "";
                }
            }
            else if (activeLevelData != null && activeLevelData.levelNumber == 35) // Fallback for specific logic if needed
            {
                consoleInputField.text = "Enter item count: \n";
            }
            else
            {
                consoleInputField.text = "";
            }

            if (consoleInputField.placeholder != null)
            {
                TMP_Text placeholderTMP = consoleInputField.placeholder.GetComponent<TMP_Text>();
                if (placeholderTMP != null)
                {
                    if (activeLevelData != null && (activeLevelData.levelNumber >= 21 && activeLevelData.levelNumber <= 41))
                    {
                        placeholderTMP.text = string.Empty;
                    }
                    else
                    {
                        placeholderTMP.text = "Enter input here...";
                    }
                }
            }

            // Re-select UI so player can just start typing
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(consoleInputField.gameObject);
                consoleInputField.Select();
                consoleInputField.ActivateInputField();
                if (activeLevelData != null && (activeLevelData.levelNumber >= 21 && activeLevelData.levelNumber <= 41))
                {
                    int firstValuePosition = 0;
                    if (_currentDynamicPrompts.Count > 0)
                    {
                        firstValuePosition = _currentDynamicPrompts[0].Length;
                    }
                    consoleInputField.caretPosition = firstValuePosition;
                }
            }

            // Clear the terminal screen (output text) for a cleaner "blank" injection phase
            if (outputText != null)
            {
                outputText.text = string.Empty;
            }
        }
        else
        {
            Debug.LogError("[TerminalLevelController] Cannot enter Data Injection Phase: consoleInputField is NOT assigned!");
        }
    }

    private void Update()
    {
        // Keep Enter as newline. Use Ctrl+Enter as keyboard submit shortcut.
        if (consoleInputField != null && consoleInputField.gameObject.activeInHierarchy && consoleInputField.isFocused)
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                {
                    // Prevent adding a new line when using Ctrl+Enter submit.
                    if (consoleInputField.text.EndsWith("\n"))
                    {
                        consoleInputField.text = consoleInputField.text.Substring(0, consoleInputField.text.Length - 1);
                    }
                    if (EventSystem.current != null)
                    {
                        EventSystem.current.SetSelectedGameObject(null);
                    }

                    OnConsoleInputSubmitted(consoleInputField.text);
                }
            }
        }
    }

    public void OnConsoleInputSubmitted(string userInput)
    {
        if (string.IsNullOrWhiteSpace(userInput) && consoleInputField != null)
        {
            userInput = consoleInputField.text;
        }

        // Only process if we are currently waiting for input
        if (consoleInputField == null || !consoleInputField.gameObject.activeSelf) return;
        if (!LevelRequiresInput()) return;

        // Block empty submit
        if (string.IsNullOrWhiteSpace(userInput)) return;

        userInput = NormalizeInjectedInput(userInput).Trim();

        // NEW: System-level Input Validation (e.g., Level 38 Operator Check / Level 39 Number Check)
        if (activeLevelData != null)
        {
            if (activeLevelData.levelNumber == 38)
            {
                string[] lines = userInput.Split('\n');
                if (lines.Length >= 3)
                {
                    string opStr = lines[2].Trim();
                    if (!string.IsNullOrEmpty(opStr))
                    {
                        char opChar = opStr[0];
                        if (opChar != '+' && opChar != '-' && opChar != '*' && opChar != '/')
                        {
                            if (outputText != null)
                            {
                                outputText.text = "<color=white>Runtime Error: Invalid Operator '" + opChar + "'. Please use +, -, *, or /.</color>";
                            }
                            if (consoleInputField != null) consoleInputField.gameObject.SetActive(false);
                            SetRetryInteractable(true);
                            return; // Stop execution
                        }
                    }
                }
            }
            else if (activeLevelData.levelNumber == 39)
            {
                string[] lines = userInput.Split('\n');
                if (lines.Length >= 1)
                {
                    string valStr = lines[0].Trim();
                    if (!int.TryParse(valStr, out _))
                    {
                        if (outputText != null) outputText.text = "<color=white>Runtime Error: Invalid Input '" + valStr + "'. Please enter a valid number.</color>";
                        if (consoleInputField != null) consoleInputField.gameObject.SetActive(false);
                        SetRetryInteractable(true);
                        return; // Stop execution
                    }
                }
            }
            else if (activeLevelData.levelNumber == 40)
            {
                string[] lines = userInput.Split('\n');
                if (lines.Length >= 3)
                {
                    // Helper to strip labels if present
                    string GetValue(int index) {
                        string raw = lines[index].Trim();
                        if (_currentDynamicPrompts.Count > index) {
                            string p = _currentDynamicPrompts[index].Trim();
                            if (raw.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                                return raw.Substring(p.Length).Trim();
                        }
                        return raw;
                    }

                    string ageVal = GetValue(0);
                    string incVal = GetValue(1);
                    string vipVal = GetValue(2).ToLower();

                    // Validate Age
                    if (!int.TryParse(ageVal, out _))
                    {
                        if (outputText != null) outputText.text = "<color=white>Runtime Error: Invalid Age '" + ageVal + "'. Please enter a number.</color>";
                        if (consoleInputField != null) consoleInputField.gameObject.SetActive(false);
                        SetRetryInteractable(true);
                        return;
                    }
                    // Validate Income
                    if (!double.TryParse(incVal, out _))
                    {
                        if (outputText != null) outputText.text = "<color=white>Runtime Error: Invalid Income '" + incVal + "'. Please enter a decimal value.</color>";
                        if (consoleInputField != null) consoleInputField.gameObject.SetActive(false);
                        SetRetryInteractable(true);
                        return;
                    }
                    // Validate VIP Status
                    if (vipVal != "true" && vipVal != "false")
                    {
                        if (outputText != null) outputText.text = "<color=white>Runtime Error: Invalid VIP Status '" + vipVal + "'. Please enter 'true' or 'false'.</color>";
                        if (consoleInputField != null) consoleInputField.gameObject.SetActive(false);
                        SetRetryInteractable(true);
                        return;
                    }
                }
            }
        }

        string submittedCode = codeInputField != null ? codeInputField.text : string.Empty;
        TerminalSubmissionResult analysis = TerminalSubmissionAnalyzer.Analyze(submittedCode, activeLevelData, sceneLevelNumber, userInput);

        if (analysis.IsValid)
        {
            consoleInputField.gameObject.SetActive(false);

            if (outputText != null)
                outputText.text = "Data Injected Successfully! Validating...";

            HandleRunResult(false, analysis.PredictedOutput);
        }
        else
        {
            consoleInputField.gameObject.SetActive(false);

            if (outputText != null)
            {
                outputText.text = analysis.Message;
            }

            SetRetryInteractable(true);
            HandleRunResult(true, string.Empty);
        }
    }

    private string NormalizeInjectedInput(string userInput)
    {
        if (string.IsNullOrWhiteSpace(userInput) || activeLevelData == null)
        {
            return userInput;
        }

        if (activeLevelData.levelNumber < 21 || activeLevelData.levelNumber > 41)
        {
            return userInput;
        }

        string normalized = userInput.Replace("\r\n", "\n").Replace("\r", "\n");
        string[] lines = normalized.Split('\n');
        string val1 = string.Empty;
        string val2 = string.Empty;
        string val3 = string.Empty;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i] != null ? lines[i].Trim() : string.Empty;
            if (line.Length == 0)
            {
                continue;
            }

            if (activeLevelData.levelNumber >= 21 && activeLevelData.levelNumber <= 41)
            {
                if (_currentDynamicPrompts.Count > 0)
                {
                    string p1 = _currentDynamicPrompts[0].Trim();
                    if (p1.Length > 0 && line.StartsWith(p1, StringComparison.OrdinalIgnoreCase))
                    {
                        val1 = line.Substring(p1.Length).Trim();
                        continue;
                    }
                }
                if (_currentDynamicPrompts.Count > 1)
                {
                    string p2 = _currentDynamicPrompts[1].Trim();
                    if (p2.Length > 0 && line.StartsWith(p2, StringComparison.OrdinalIgnoreCase))
                    {
                        val2 = line.Substring(p2.Length).Trim();
                        continue;
                    }
                }
                if (_currentDynamicPrompts.Count > 2)
                {
                    string p3 = _currentDynamicPrompts[2].Trim();
                    if (p3.Length > 0 && line.StartsWith(p3, StringComparison.OrdinalIgnoreCase))
                    {
                        val3 = line.Substring(p3.Length).Trim();
                        continue;
                    }
                }
            }
        }

        if (string.IsNullOrWhiteSpace(val1) || string.IsNullOrWhiteSpace(val2) || string.IsNullOrWhiteSpace(val3))
        {
            List<string> nonEmpty = new List<string>();
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i] != null ? lines[i].Trim() : string.Empty;
                if (line.Length > 0)
                {
                    nonEmpty.Add(line);
                }
            }

            if (string.IsNullOrWhiteSpace(val1) && nonEmpty.Count > 0)
            {
                val1 = nonEmpty[0];
            }
            if (string.IsNullOrWhiteSpace(val2) && nonEmpty.Count > 1)
            {
                val2 = nonEmpty[1];
            }
            if (string.IsNullOrWhiteSpace(val3) && nonEmpty.Count > 2)
            {
                val3 = nonEmpty[2];
            }
        }

        return string.Format("{0}\n{1}\n{2}", val1, val2, val3).Trim();
    }


    public void HandleRunResult(bool hasCodeErrors, string providedOutput)
    {
        if (hasCodeErrors)
        {
            if (outputText != null && string.IsNullOrWhiteSpace(outputText.text))
            {
                outputText.text = "Code has errors. Fix your code and run again.";
            }
            return;
        }

        int levelNumber = activeLevelNumber > 0 ? activeLevelNumber : sceneLevelNumber;
        int baseRewardTokens = activeLevelData != null ? activeLevelData.baseTokenReward : fallbackTokenReward;
        int rewardTokens = GetCompletionRewardTokens(levelNumber, baseRewardTokens);

        string predictedOutput = string.IsNullOrEmpty(providedOutput)
            ? GetFallbackExpectedOutput()
            : providedOutput;

        // Visual Filter: Remove dynamic prompts from the displayed output to avoid redundancy
        string filteredOutput = predictedOutput;
        if (_currentDynamicPrompts != null)
        {
            foreach (string prompt in _currentDynamicPrompts)
            {
                if (!string.IsNullOrEmpty(prompt))
                {
                    // Remove the prompt and trim any leftover whitespace/newlines
                    filteredOutput = filteredOutput.Replace(prompt, "").TrimStart();
                }
            }
        }

        StartCoroutine(SuccessCountdownRoutine(filteredOutput, baseRewardTokens, rewardTokens));
    }

    private string GetFallbackExpectedOutput()
    {
        if (activeLevelData == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(activeLevelData.expectedOutput))
        {
            return activeLevelData.expectedOutput;
        }

        if (activeLevelData.testCases != null)
        {
            for (int i = 0; i < activeLevelData.testCases.Length; i++)
            {
                LevelTestCase testCase = activeLevelData.testCases[i];
                if (testCase == null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(testCase.expectedOutput))
                {
                    return testCase.expectedOutput;
                }

                if (!string.IsNullOrWhiteSpace(testCase.output))
                {
                    return testCase.output;
                }
            }
        }

        return string.Empty;
    }

    private System.Collections.IEnumerator SuccessCountdownRoutine(string rawOutput, int baseRewardTokens, int rewardTokens)
    {
        string successText = BuildSuccessMessage(rawOutput, rewardTokens);

        if (outputText != null)
        {
            outputText.text = successText + "Closing terminal in 8...";
        }

        for (int i = 8; i > 0; i--)
        {
            if (outputText != null)
            {
                outputText.text = successText + $"Closing terminal in {i}...";
            }
            yield return new WaitForSeconds(1f);
        }

        // ── Backend progress sync ─────────────────────────────────────────
        // POST /progress/update for the currently logged-in user.
        // Uses GameApiManager.CurrentUser to identify the player —
        // whoever is logged in is the player currently playing this level.
        // The sync is non-blocking: if not logged in or the request fails,
        // the game flow still continues and the door still opens.
        bool syncDone = false;
        bool syncSuccess = false;

        if (outputText != null)
            outputText.text = successText + "Saving progress...";

        int levelToSync = activeLevelNumber > 0 ? activeLevelNumber : sceneLevelNumber;

        int current = PlayerPrefs.HasKey("CurrentLevel") ? PlayerPrefs.GetInt("CurrentLevel") : 1;
        int highest = PlayerPrefs.HasKey("HighestLevel") ? PlayerPrefs.GetInt("HighestLevel") : 1;
        int currentPlayableLevel = Mathf.Max(current, highest);
        bool isReplay = levelToSync < currentPlayableLevel;

        if (isReplay && rewardTokens > 0)
        {
            TokenManager.AddTokens(rewardTokens);
            // We do NOT request sync here to prevent race conditions with SyncAfterLevel.
            // It will be requested after SyncAfterLevel completes.
        }

        if (levelToSync <= 0)
        {
            Debug.LogError($"[TerminalLevelController] Invalid levelToSync ({levelToSync}) — cannot sync progress.");
            syncDone = true;
            syncSuccess = false;
        }
        else
        {
            int tokensToSyncViaProgress = isReplay ? 0 : rewardTokens;
            Debug.Log($"[TerminalLevelController] Syncing progress to backend: level={levelToSync}, tokens={tokensToSyncViaProgress}");
            ProgressService.SyncAfterLevel(
                levelNumber: levelToSync,
                tokensEarned: tokensToSyncViaProgress,
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
            outputText.text = successText + (syncSuccess
                ? "Progress saved!"
                : "Progress saved locally.");
        }

        // Now that Progress Update is completely done, safely push the pending coins
        // (including the replay reward) without triggering a backend database race condition.
        if (TokenManager.GetPending() > 0)
        {
            TokenManager.RequestPendingSync();
        }

        yield return new WaitForSeconds(0.8f);
        // ─────────────────────────────────────────────────────────────────

        onLevelSolved?.Invoke();
        CloseTerminalModal();
    }

    private string BuildSuccessMessage(string rawOutput, int tokensEarned)
    {
        string successColor = "#39D353";
        System.Text.StringBuilder builder = new System.Text.StringBuilder();

        builder.AppendLine($"<color={successColor}><b>Success! Level Complete - Moving to the Next Level</b></color>");
        builder.AppendLine();

        if (!string.IsNullOrWhiteSpace(rawOutput))
        {
            builder.AppendLine($"Output: {rawOutput}");
            builder.AppendLine();
        }

        builder.AppendLine($"Bonus Coins: {tokensEarned}");
        builder.AppendLine($"Challenge cleared: you have successfully claimed {tokensEarned} Reward Token to add to your balance.");
        builder.AppendLine();

        return builder.ToString();
    }

    private int GetCompletionRewardTokens(int levelNumber, int firstClearRewardTokens)
    {
        int current = PlayerPrefs.HasKey("CurrentLevel") ? PlayerPrefs.GetInt("CurrentLevel") : 1;
        int highest = PlayerPrefs.HasKey("HighestLevel") ? PlayerPrefs.GetInt("HighestLevel") : 1;
        int currentPlayableLevel = Mathf.Max(current, highest);

        // Levels below the current playable level are replays.
        // Replays get the fixed reward, while the current playable level gets the original reward.
        return levelNumber < currentPlayableLevel ? 30 : firstClearRewardTokens;
    }

    private List<string> ExtractPromptsFromAst(TerminalCompilationUnitNode ast)
    {
        List<string> prompts = new List<string>();
        if (ast == null) return prompts;

        // Extract statements from main method or top level
        List<TerminalStatementNode> statements = new List<TerminalStatementNode>();
        foreach (var method in ast.Methods)
        {
            if (string.Equals(method.Name, "main", StringComparison.OrdinalIgnoreCase))
            {
                statements.AddRange(method.Body.Statements);
                break;
            }
        }
        if (statements.Count == 0) statements.AddRange(ast.TopLevelStatements);

        // Look for pairs: System.out.print("...") followed by scanner.next...()
        for (int i = 0; i < statements.Count - 1; i++)
        {
            var current = statements[i];
            var next = statements[i + 1];

            // 1. Check if current is System.out.print("...")
            string promptValue = TryGetLiteralFromPrintCall(current);
            if (promptValue != null)
            {
                // 2. Check if next contains a Scanner call
                if (ContainsScannerCall(next))
                {
                    prompts.Add(promptValue);
                }
            }
        }

        return prompts;
    }

    private string TryGetLiteralFromPrintCall(TerminalStatementNode stmt)
    {
        // Must be an expression statement
        var exprStmt = stmt as TerminalExpressionStatementNode;
        if (exprStmt == null) return null;

        // Must be a call to print/println
        var call = exprStmt.Expression as TerminalCallExpressionNode;
        if (call == null) return null;

        var member = call.Callee as TerminalMemberExpressionNode;
        if (member == null) return null;

        // Check if it's System.out.print or out.print
        bool isPrint = string.Equals(member.MemberName, "print", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(member.MemberName, "println", StringComparison.OrdinalIgnoreCase);
        if (!isPrint) return null;

        // Check if it has a string literal argument
        if (call.Arguments.Count > 0)
        {
            var literal = call.Arguments[0] as TerminalLiteralExpressionNode;
            if (literal != null && literal.Value is string)
            {
                return (string)literal.Value;
            }
        }

        return null;
    }

    private bool ContainsScannerCall(TerminalStatementNode stmt)
    {
        TerminalExpressionNode expr = null;
        if (stmt is TerminalExpressionStatementNode exprStmt) expr = exprStmt.Expression;
        else if (stmt is TerminalVariableDeclarationNode varDecl) expr = varDecl.Initializer;

        return IsScannerExpression(expr);
    }

    private bool IsScannerExpression(TerminalExpressionNode expr)
    {
        if (expr == null) return false;

        if (expr is TerminalCallExpressionNode call)
        {
            if (call.Callee is TerminalMemberExpressionNode member)
            {
                if (member.MemberName.ToLowerInvariant().StartsWith("next")) return true;
                // Recursive check for targets like sc.next().charAt(0)
                if (IsScannerExpression(member.Target)) return true;
            }
        }
        return false;
    }
}

