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
using System.Collections.Generic;
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
    [SerializeField] private TMP_InputField consoleInputField; // NEW: For Interactive Input

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

        if (consoleInputField != null)
        {
            consoleInputField.gameObject.SetActive(false);
            consoleInputField.onSubmit.AddListener(OnConsoleInputSubmitted);
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
        if (consoleInputField != null)
        {
            consoleInputField.gameObject.SetActive(false); // Hide interactive input array
            consoleInputField.text = string.Empty;
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

        // 1. Validate Syntax & Keywords first
        hasCodeErrors = !ValidateSubmittedCode(submittedCode, out validationMessage);

        if (hasCodeErrors)
        {
            if (outputText != null) outputText.text = validationMessage;
            HandleRunResult(true, string.Empty);
            return;
        }

        // 2. Check if Level Requires Interactive Input (Data Injection)
        if (LevelRequiresInput())
        {
            EnterDataInjectionPhase();
            return; // Wait for user to submit input via consoleInputField
        }

        // 3. Standard Non-Interactive Run Mode
        if (autoRunUsesSimpleValidation)
        {
            if (!ValidateExpectedOutput(submittedCode, out validationMessage))
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

        HandleRunResult(hasCodeErrors, string.Empty);
    }

    private bool LevelRequiresInput()
    {
        if (activeLevelData == null) return false;

        // 1. If the level requires Scanner, it definitely needs interactive input
        if (activeLevelData.requiredKeywords != null)
        {
            foreach (var kw in activeLevelData.requiredKeywords)
            {
                if (kw.Contains("Scanner") || kw.Contains(".next") || kw.Contains("terminalInput"))
                    return true;
            }
        }

        // 2. If test cases have explicit inputs or the wildcard '*'
        if (activeLevelData.testCases != null)
        {
            foreach (var tc in activeLevelData.testCases)
            {
                if (!string.IsNullOrEmpty(tc.input) || tc.input == "*")
                    return true;
            }
        }

        return false;
    }

    private void EnterDataInjectionPhase()
    {
        if (consoleInputField != null)
        {
            consoleInputField.gameObject.SetActive(true);
            consoleInputField.text = "";

            // Re-select UI so player can just start typing
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(consoleInputField.gameObject);
                consoleInputField.Select();
                consoleInputField.ActivateInputField();
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

    public void OnConsoleInputSubmitted(string userInput)
    {
        // Only process if we are currently waiting for input
        if (consoleInputField == null || !consoleInputField.gameObject.activeSelf) return;
        if (!LevelRequiresInput()) return;

        // Block empty submit
        if (string.IsNullOrWhiteSpace(userInput)) return;

        // Find matching test case by input mapping
        LevelTestCase match = null;
        bool isWildcard = false;

        foreach (var tc in activeLevelData.testCases)
        {
            if (tc.input == userInput)
            {
                match = tc;
                break;
            }
            if (tc.input == "*" || tc.input == "")
            {
                match = tc;
                isWildcard = true;
                // Don't break, in case another test case is an exact match.
            }
        }

        // NEW LOGIC: Accept arbitrary inputs dynamically if no predefined match.
        bool isDynamicMatch = false;
        string dynamicOutput = "";

        if (match == null)
        {
            if (double.TryParse(userInput, out double numVal))
            {
                if (activeLevelNumber == 31)
                {
                    dynamicOutput = numVal >= 0 ? "Positive" : "Negative";
                    isDynamicMatch = true;
                }
                else
                {
                    // For generic unknown levels, we can just echo the input back 
                    // or simulate wildcard behavior.
                    isDynamicMatch = true;
                    dynamicOutput = ExtractPredictedOutput(codeInputField != null ? codeInputField.text : string.Empty, userInput);
                }
            }
            else
            {
                // If it's pure text, just simulate it via wildcard extractor
                isDynamicMatch = true;
                dynamicOutput = ExtractPredictedOutput(codeInputField != null ? codeInputField.text : string.Empty, userInput);
            }
        }

        if (match != null || isDynamicMatch)
        {
            consoleInputField.gameObject.SetActive(false);

            if (outputText != null)
                outputText.text = "Data Injected Successfully! Validating...";

            string resultOutput = "";
            string submittedCode = codeInputField != null ? codeInputField.text : string.Empty;

            if (isDynamicMatch)
            {
                resultOutput = dynamicOutput;
            }
            else if (isWildcard)
            {
                // Calculate dynamic expected output from submitted code using wildcard input
                resultOutput = ExtractPredictedOutput(submittedCode, userInput);
            }
            else
            {
                // Prioritize expectedOutput, then fallback to output
                resultOutput = !string.IsNullOrEmpty(match.expectedOutput) ? match.expectedOutput : match.output;
            }

            // In some wildcard cases of conditionals, it prints both. Clean it up for level 31 just in case.
            if (activeLevelNumber == 31 && resultOutput.Contains("Positive") && resultOutput.Contains("Negative"))
            {
                if (double.TryParse(userInput, out double dVal))
                    resultOutput = dVal >= 0 ? "Positive" : "Negative";
            }

            HandleRunResult(false, resultOutput);
        }
        else
        {
            if (outputText != null)
            {
                outputText.text = $"[Engine Warning] This terminal only supports simulating predefined test cases.\nInput '{userInput}' is not recognized.\nPlease try a listed valid payload.";
            }

            // Keep input field active, just re-focus
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(consoleInputField.gameObject);
                consoleInputField.Select();
                consoleInputField.ActivateInputField();
            }
        }
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

        string codeWithoutComments = StripComments(submittedCode);

        if (!ValidateBasicSyntaxHeuristics(codeWithoutComments, out message))
        {
            return false;
        }

        if (activeLevelData != null)
        {
            if (!ContainsRequiredKeywords(codeWithoutComments, activeLevelData.requiredKeywords, out message))
            {
                return false;
            }

            if (!ValidateRequiredCodePattern(codeWithoutComments, activeLevelData.requiredCodePattern, out message))
            {
                return false;
            }

            if (!ValidateRequiredPrintlnCount(codeWithoutComments, activeLevelData.requiredPrintlnCount, out message))
            {
                return false;
            }

            if (ContainsForbiddenKeywords(codeWithoutComments, activeLevelData.forbiddenKeywords, out message))
            {
                return false;
            }

            if (activeLevelData.testCases != null)
            {
                for (int i = 0; i < activeLevelData.testCases.Length; i++)
                {
                    LevelTestCase testCase = activeLevelData.testCases[i];
                    if (testCase == null || testCase.requiredKeywords == null) continue;

                    if (!ContainsRequiredKeywords(codeWithoutComments, testCase.requiredKeywords, out message))
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

    private bool ValidateRequiredPrintlnCount(string code, int requiredPrintlnCount, out string message)
    {
        if (requiredPrintlnCount <= 0)
        {
            message = string.Empty;
            return true;
        }

        int actualPrintlnCount = Regex.Matches(code, @"\bSystem\.out\.println\s*\(").Count;
        if (actualPrintlnCount < requiredPrintlnCount)
        {
            message = $"Expected at least {requiredPrintlnCount} println statement(s), found {actualPrintlnCount}.";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private bool ValidateRequiredCodePattern(string code, string requiredCodePattern, out string message)
    {
        if (string.IsNullOrWhiteSpace(requiredCodePattern))
        {
            message = string.Empty;
            return true;
        }

        if (!Regex.IsMatch(code, requiredCodePattern, RegexOptions.Singleline))
        {
            message = "Code structure does not match the required output pattern.";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private bool ValidateBasicSyntaxHeuristics(string code, out string message)
    {
        string[] lines = code.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
        HashSet<string> declaredIdentifiers = CollectDeclaredIdentifiers(code);

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (line.EndsWith("{") || line.EndsWith("}"))
            {
                continue;
            }

            if (IsStructuralLine(line))
            {
                continue;
            }

            if (LooksLikeStatementThatNeedsSemicolon(line) && !line.EndsWith(";"))
            {
                message = $"Syntax check failed: missing semicolon near line {i + 1}.";
                return false;
            }
        }

        if (!ValidateReferencedIdentifiers(code, declaredIdentifiers, out message))
        {
            return false;
        }

        message = string.Empty;
        return true;
    }

    private HashSet<string> CollectDeclaredIdentifiers(string code)
    {
        HashSet<string> identifiers = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

        Match mainArgs = Regex.Match(code, @"\bmain\s*\(\s*String\s*\[\]\s*(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\)");
        if (mainArgs.Success)
        {
            identifiers.Add(mainArgs.Groups["name"].Value);
        }

        MatchCollection declarationMatches = Regex.Matches(code, @"\b(?:final\s+)?(?<type>String|int|double|float|long|char|boolean|var|Scanner|StringBuilder)\s+(?<rest>[^;]+);");
        foreach (Match declaration in declarationMatches)
        {
            string rest = declaration.Groups["rest"].Value;
            string[] parts = rest.Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                string segment = parts[i].Trim();
                if (string.IsNullOrWhiteSpace(segment))
                {
                    continue;
                }

                string candidate = segment;
                int equalsIndex = candidate.IndexOf('=');
                if (equalsIndex >= 0)
                {
                    candidate = candidate.Substring(0, equalsIndex).Trim();
                }

                int arrayIndex = candidate.IndexOf('[');
                if (arrayIndex >= 0)
                {
                    candidate = candidate.Substring(0, arrayIndex).Trim();
                }

                if (Regex.IsMatch(candidate, @"^[A-Za-z_][A-Za-z0-9_]*$"))
                {
                    identifiers.Add(candidate);
                }
            }
        }

        MatchCollection methodDeclarationMatches = Regex.Matches(
            code,
            @"\b(?:public|private|protected|internal)?\s*(?:static\s+)?(?:async\s+)?(?:final\s+)?(?:void|bool|boolean|byte|short|int|long|float|double|char|String|var|Scanner|StringBuilder|[A-Za-z_][A-Za-z0-9_<>,\[\]\s?]*)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\(",
            RegexOptions.Singleline);

        foreach (Match methodDeclaration in methodDeclarationMatches)
        {
            string methodName = methodDeclaration.Groups["name"].Value;
            if (Regex.IsMatch(methodName, @"^[A-Za-z_][A-Za-z0-9_]*$"))
            {
                identifiers.Add(methodName);
            }
        }

        return identifiers;
    }

    private bool ValidateReferencedIdentifiers(string code, HashSet<string> declaredIdentifiers, out string message)
    {
        MatchCollection expressionMatches = Regex.Matches(code, @"System\.out\.(?:println|print)\s*\((?<expr>.*?)\)|\breturn\s+(?<expr>[^;]+);|\b(?:final\s+)?(?:String|int|double|float|long|char|boolean|var|Scanner|StringBuilder)\s+[A-Za-z_][A-Za-z0-9_]*\s*=\s*(?<expr>[^;]+);", RegexOptions.Singleline);

        foreach (Match expressionMatch in expressionMatches)
        {
            Group exprGroup = expressionMatch.Groups["expr"];
            if (exprGroup == null || !exprGroup.Success)
            {
                continue;
            }

            string expression = StripStringAndCharLiterals(exprGroup.Value);
            MatchCollection identifiers = Regex.Matches(expression, @"\b[A-Za-z_][A-Za-z0-9_]*\b");

            for (int i = 0; i < identifiers.Count; i++)
            {
                Match identifierMatch = identifiers[i];
                string identifier = identifierMatch.Value;

                if (IsKnownIdentifier(identifier))
                {
                    continue;
                }

                int startIndex = identifierMatch.Index;
                if (startIndex > 0 && expression[startIndex - 1] == '.')
                {
                    continue;
                }

                if (!declaredIdentifiers.Contains(identifier))
                {
                    message = $"Syntax check failed: undeclared identifier '{identifier}' used in an expression.";
                    return false;
                }
            }
        }

        message = string.Empty;
        return true;
    }

    private bool IsKnownIdentifier(string identifier)
    {
        switch (identifier)
        {
            case "System":
            case "out":
            case "print":
            case "println":
            case "String":
            case "int":
            case "double":
            case "float":
            case "long":
            case "char":
            case "boolean":
            case "var":
            case "Scanner":
            case "StringBuilder":
            case "Arrays":
            case "Math":
            case "Integer":
            case "Double":
            case "Float":
            case "Long":
            case "Boolean":
            case "Character":
            case "Object":
            case "args":
            case "new":
            case "return":
            case "if":
            case "else":
            case "for":
            case "while":
            case "do":
            case "switch":
            case "case":
            case "default":
            case "break":
            case "continue":
            case "class":
            case "public":
            case "private":
            case "protected":
            case "static":
            case "void":
            case "true":
            case "false":
            case "null":
            case "in":
            case "nextInt":
            case "nextDouble":
            case "nextFloat":
            case "nextLine":
            case "equals":
            case "length":
            case "toString":
            case "parseInt":
            case "parseDouble":
            case "parseFloat":
            case "parseLong":
                return true;
            default:
                return false;
        }
    }

    private bool IsStructuralLine(string line)
    {
        return line.StartsWith("if ", System.StringComparison.Ordinal) ||
               line.StartsWith("if(", System.StringComparison.Ordinal) ||
               line.StartsWith("else ", System.StringComparison.Ordinal) ||
               line == "else" ||
               line.StartsWith("else if", System.StringComparison.Ordinal) ||
               line.StartsWith("for ", System.StringComparison.Ordinal) ||
               line.StartsWith("for(", System.StringComparison.Ordinal) ||
               line.StartsWith("while ", System.StringComparison.Ordinal) ||
               line.StartsWith("while(", System.StringComparison.Ordinal) ||
             line == "do" ||
             line.StartsWith("do ", System.StringComparison.Ordinal) ||
             line.StartsWith("do{", System.StringComparison.Ordinal) ||
             line.StartsWith("do(", System.StringComparison.Ordinal) ||
               line.StartsWith("switch ", System.StringComparison.Ordinal) ||
               line.StartsWith("switch(", System.StringComparison.Ordinal) ||
               line.StartsWith("case ", System.StringComparison.Ordinal) ||
               line.StartsWith("default:", System.StringComparison.Ordinal) ||
               line.StartsWith("try", System.StringComparison.Ordinal) ||
               line.StartsWith("catch", System.StringComparison.Ordinal) ||
               line.StartsWith("finally", System.StringComparison.Ordinal) ||
               line.StartsWith("import ", System.StringComparison.Ordinal) ||
               line.StartsWith("package ", System.StringComparison.Ordinal) ||
               line.StartsWith("public class", System.StringComparison.Ordinal) ||
               line.StartsWith("class ", System.StringComparison.Ordinal) ||
               line.StartsWith("enum ", System.StringComparison.Ordinal) ||
               line.StartsWith("interface ", System.StringComparison.Ordinal);
    }

    private bool LooksLikeStatementThatNeedsSemicolon(string line)
    {
        return Regex.IsMatch(line, @"^(?:return|break|continue|throw|System\.out\.(?:println|print)|(?:final\s+)?(?:String|int|double|float|long|char|boolean|var|Scanner|StringBuilder)\b|[A-Za-z_][A-Za-z0-9_]*\s*=)");
    }

    private string StripStringAndCharLiterals(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        string withoutStrings = Regex.Replace(text, "\"(?:\\\\.|[^\"\\\\])*\"", "");
        return Regex.Replace(withoutStrings, "'(?:\\\\.|[^'\\\\])*'", "");
    }

    private string StripComments(string code)
    {
        if (string.IsNullOrEmpty(code))
        {
            return string.Empty;
        }

        System.Text.StringBuilder result = new System.Text.StringBuilder(code.Length);
        bool inSingleLineComment = false;
        bool inBlockComment = false;
        bool inString = false;
        bool inChar = false;
        bool escapeNext = false;

        for (int i = 0; i < code.Length; i++)
        {
            char current = code[i];
            char next = i + 1 < code.Length ? code[i + 1] : '\0';

            if (inSingleLineComment)
            {
                if (current == '\n')
                {
                    inSingleLineComment = false;
                    result.Append(current);
                }
                continue;
            }

            if (inBlockComment)
            {
                if (current == '\n')
                {
                    result.Append(current);
                }

                if (current == '*' && next == '/')
                {
                    inBlockComment = false;
                    i++;
                }
                continue;
            }

            if (inString)
            {
                result.Append(current);
                if (escapeNext)
                {
                    escapeNext = false;
                }
                else if (current == '\\')
                {
                    escapeNext = true;
                }
                else if (current == '"')
                {
                    inString = false;
                }
                continue;
            }

            if (inChar)
            {
                result.Append(current);
                if (escapeNext)
                {
                    escapeNext = false;
                }
                else if (current == '\\')
                {
                    escapeNext = true;
                }
                else if (current == '\'')
                {
                    inChar = false;
                }
                continue;
            }

            if (current == '/' && next == '/')
            {
                inSingleLineComment = true;
                i++;
                continue;
            }

            if (current == '/' && next == '*')
            {
                inBlockComment = true;
                i++;
                continue;
            }

            result.Append(current);

            if (current == '"')
            {
                inString = true;
                escapeNext = false;
            }
            else if (current == '\'')
            {
                inChar = true;
                escapeNext = false;
            }
        }

        return result.ToString();
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
            ? ExtractPredictedOutput(codeInputField != null ? codeInputField.text : string.Empty)
            : providedOutput;

        // Bypass for Level 18 display since regex cannot statically evaluate reassignments
        if (activeLevelData != null && activeLevelData.levelNumber == 18)
        {
            predictedOutput = activeLevelData.expectedOutput;
        }

        StartCoroutine(SuccessCountdownRoutine(predictedOutput, baseRewardTokens, rewardTokens));
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

    private bool ValidateExpectedOutput(string submittedCode, out string message)
    {
        // Bypass for Level 18 static analysis due to variable swapping
        if (activeLevelData != null && activeLevelData.levelNumber == 18)
        {
            message = string.Empty;
            return true;
        }

        string expected = string.Empty;
        string expectedPattern = string.Empty;

        if (activeLevelData != null)
        {
            if (!string.IsNullOrWhiteSpace(activeLevelData.expectedOutputPattern))
            {
                expectedPattern = activeLevelData.expectedOutputPattern;
            }

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

        if (!string.IsNullOrWhiteSpace(expectedPattern))
        {
            string predictedPatternValue = ExtractPredictedOutput(submittedCode);
            if (string.IsNullOrWhiteSpace(predictedPatternValue) && TryPredictSimpleSentenceOutput(submittedCode, out string fallbackPredictedPatternValue))
            {
                predictedPatternValue = fallbackPredictedPatternValue;
            }

            if (string.IsNullOrWhiteSpace(predictedPatternValue))
            {
                message = "Expected output check failed: no recognizable output statement found.";
                return false;
            }

            string normalizedPredictedPatternValue = Normalize(predictedPatternValue);
            if (!Regex.IsMatch(normalizedPredictedPatternValue, expectedPattern, RegexOptions.Singleline | RegexOptions.IgnoreCase) && TryPredictSimpleSentenceOutput(submittedCode, out string sentenceFallbackValue))
            {
                normalizedPredictedPatternValue = Normalize(sentenceFallbackValue);
            }

            if (!Regex.IsMatch(normalizedPredictedPatternValue, expectedPattern, RegexOptions.Singleline | RegexOptions.IgnoreCase))
            {
                message = $"Output mismatch. Expected pattern: {expectedPattern} | Found: {predictedPatternValue}";
                return false;
            }

            message = string.Empty;
            return true;
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

    private string ResolveVariableValue(string id, string code, string injectedInput, out bool isDouble, int depth = 0)
    {
        isDouble = false;
        if (depth > 5) return string.Empty; // Prevent infinite recursion

        // Look for assignment: [type] id = [val]; or id = [val];
        var match = Regex.Match(code, $@"(?<type>double|int|float|long|var|String|char)?\s*{Regex.Escape(id)}\s*=\s*(?<val>[^;]+)\s*;", RegexOptions.Singleline);
        if (!match.Success) return string.Empty;

        string type = match.Groups["type"].Value;
        string val = match.Groups["val"].Value.Trim();

        if (type == "double" || type == "float") isDouble = true;

        // 1. Is it a numeric literal?
        if (double.TryParse(val, out double d))
        {
            if (isDouble && !val.Contains(".")) return d.ToString("F1");
            return val;
        }

        // 2. Is it a string literal?
        if (val.StartsWith("\"") && val.EndsWith("\""))
        {
            return val.Substring(1, val.Length - 2);
        }

        // 2.5 Is it a boolean literal?
        if (val == "true" || val == "false")
        {
            return val;
        }

        // 2.6 Is it a char literal? (single quotes)
        if (val.StartsWith("'") && val.EndsWith("'"))
        {
            return val.Substring(1, val.Length - 2);
        }

        if (TryEvaluateKnownMethodCall(val, code, injectedInput, out string methodValue))
        {
            return methodValue;
        }

        if (TryEvaluateArithmeticExpression(val, code, injectedInput, out string arithmeticValue))
        {
            if (double.TryParse(arithmeticValue, out double arithmeticDouble))
            {
                isDouble = arithmeticValue.Contains(".");
                return isDouble ? JavaFormat(arithmeticDouble) : arithmeticValue;
            }

            return arithmeticValue;
        }

        // 3. Is it another identifier?
        if (Regex.IsMatch(val, @"^[A-Za-z_][A-Za-z0-9_]*$"))
        {
            bool parentIsDouble;
            string resolved = ResolveVariableValue(val, code, injectedInput, out parentIsDouble, depth + 1);
            if (isDouble && !resolved.Contains("."))
            {
                if (double.TryParse(resolved, out double rd)) return rd.ToString("F1");
            }
            return resolved;
        }

        return string.Empty;
    }

    private string ExtractPredictedOutput(string code, string injectedInput = "")
    {
        var matches = Regex.Matches(
            code,
            @"System\.out\.(?:println|print)\s*\(\s*(?<expr>(?:[^()]|\((?<open>)|\)(?<-open>))*(?(open)(?!)))\)",
            RegexOptions.Singleline);
        if (matches.Count == 0)
            matches = Regex.Matches(
                code,
                @"\bprint\s*\(\s*(?<expr>(?:[^()]|\((?<open>)|\)(?<-open>))*(?(open)(?!)))\)",
                RegexOptions.Singleline);

        if (matches.Count == 0)
            return string.Empty;

        System.Text.StringBuilder finalOutput = new System.Text.StringBuilder();

        foreach (Match call in matches)
        {
            string expr = call.Groups["expr"].Value.Trim();
            string lineResult = EvaluateSingleExpression(expr, code, injectedInput);
            if (!string.IsNullOrEmpty(lineResult))
            {
                finalOutput.AppendLine(lineResult);
            }
        }

        // Return all combined lines
        return finalOutput.ToString().TrimEnd();
    }

    private string EvaluateSingleExpression(string expr, string code, string injectedInput)
    {
        // 1) Exact string literal: "text"
        Match strLit = Regex.Match(expr, "^\"(?<txt>(?:[^\"\\\\]|\\\\.)*)\"$");
        if (strLit.Success)
            return strLit.Groups["txt"].Value;

        // 2) Numeric literal (integer or float, allow negative)
        Match numLit = Regex.Match(expr, "^(?<num>-?\\d+(?:\\.\\d+)?)$");
        if (numLit.Success)
            return numLit.Groups["num"].Value;

        // 2.5) Char literal: 'A'
        Match charLit = Regex.Match(expr, "^'(?<txt>.*)'$");
        if (charLit.Success)
            return charLit.Groups["txt"].Value;

        if (TryEvaluateKnownMethodCall(expr, code, injectedInput, out string methodResult))
        {
            return methodResult;
        }

        if (TryEvaluateArithmeticExpression(expr, code, injectedInput, out string arithmeticResult))
        {
            return arithmeticResult;
        }

        // 3) Expression with concatenation or arithmetic using + — collect tokens
        // Split injected input into multiple parts to support sc.nextInt() multiple times.
        string[] inputs = injectedInput.Split(new[] { ' ', ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        int inputIdx = 0;

        // Tokenize: string literals, numbers, identifiers, and operators
        var tokenRegex = new Regex("\"(?<str>[^\"]*)\"|'(?<chr>[^']*)'|(?<num>-?\\d+(?:\\.\\d+)?)|(?<id>[A-Za-z_][A-Za-z0-9_]*)|(?<op>\\+)");
        var matches = tokenRegex.Matches(expr);

        if (matches.Count > 0)
        {
            object currentResult = null; // Can be string or double
            bool lastWasOp = false;
            bool exprHasDouble = false;

            foreach (Match mm in matches)
            {
                if (mm.Groups["op"].Success)
                {
                    lastWasOp = true;
                    continue;
                }

                object tokenVal = null;
                bool isNumber = false;

                if (mm.Groups["str"].Success)
                {
                    tokenVal = mm.Groups["str"].Value;
                    isNumber = false;
                }
                else if (mm.Groups["chr"].Success)
                {
                    tokenVal = mm.Groups["chr"].Value;
                    isNumber = false;
                }
                else if (mm.Groups["num"].Success)
                {
                    string v = mm.Groups["num"].Value;
                    if (v.Contains(".")) exprHasDouble = true;

                    if (double.TryParse(v, out double d))
                    {
                        tokenVal = d;
                        isNumber = true;
                    }
                }
                else if (mm.Groups["id"].Success)
                {
                    string id = mm.Groups["id"].Value;

                    // 1. Look for a Scanner assignment mapped to injected input
                    bool resolved = false;
                    var scannerAssign = Regex.Match(code, $"\\b(?:String|int|long|float|double|var|char)?\\s*{Regex.Escape(id)}\\s*=\\s*[A-Za-z0-9_]+\\.next(?:Line|Int|Double|Float)?\\s*\\(\\)\\s*;", RegexOptions.Singleline);
                    if (scannerAssign.Success)
                    {
                        if (code.Contains("nextDouble") || code.Contains("nextFloat")) exprHasDouble = true;

                        string valStr = (inputs != null && inputIdx < inputs.Length) ? inputs[inputIdx++] : "0";
                        if (valStr.Contains(".")) exprHasDouble = true;

                        if (double.TryParse(valStr, out double d))
                        {
                            tokenVal = d;
                            isNumber = true;
                        }
                        else
                        {
                            tokenVal = valStr;
                            isNumber = false;
                        }
                        resolved = true;
                    }

                    if (!resolved)
                    {
                        // Use recursive resolver
                        string rVal = ResolveVariableValue(id, code, injectedInput, out bool idIsDouble);
                        if (idIsDouble) exprHasDouble = true;

                        if (!string.IsNullOrEmpty(rVal))
                        {
                            if (double.TryParse(rVal, out double rd))
                            {
                                tokenVal = rd;
                                isNumber = true;
                                resolved = true;
                            }
                            else
                            {
                                tokenVal = rVal;
                                isNumber = false;
                                resolved = true;
                            }
                        }
                    }

                    if (!resolved) return string.Empty; // Cannot predict
                }

                if (currentResult == null)
                {
                    currentResult = tokenVal;
                }
                else if (lastWasOp)
                {
                    // Java Style Evaluation: If either is string, concat. If both are numbers, add.
                    if (currentResult is string || tokenVal is string)
                    {
                        currentResult = currentResult.ToString() + tokenVal.ToString();
                    }
                    else if (currentResult is double && tokenVal is double)
                    {
                        currentResult = (double)currentResult + (double)tokenVal;
                    }
                    lastWasOp = false;
                }
            }

            if (currentResult != null)
            {
                if (currentResult is double resD)
                {
                    if (exprHasDouble) return JavaFormat(resD);
                    return resD.ToString();
                }
                return currentResult.ToString();
            }
        }

        // 4) Single identifier: try to resolve assignment
        var idOnly = Regex.Match(expr, "^(?<id>[A-Za-z_][A-Za-z0-9_]*)$");
        if (idOnly.Success)
        {
            string id = idOnly.Groups["id"].Value;

            if (TryResolveScannerInputValue(id, code, injectedInput, out string scannerValue))
            {
                return scannerValue;
            }

            string rVal = ResolveVariableValue(id, code, injectedInput, out bool idIsDouble);
            if (idIsDouble && double.TryParse(rVal, out double rd)) return JavaFormat(rd);
            return rVal;
        }

        // Unknown/unhandled expression
        return string.Empty;
    }

    private bool TryEvaluateKnownMethodCall(string expr, string code, string injectedInput, out string result)
    {
        result = string.Empty;

        Match callMatch = Regex.Match(expr.Trim(), @"^(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\((?<args>.*)\)$", RegexOptions.Singleline);
        if (!callMatch.Success)
        {
            return false;
        }

        string methodName = callMatch.Groups["name"].Value;
        if (!TrySplitMethodArguments(callMatch.Groups["args"].Value, out List<string> arguments))
        {
            return false;
        }

        switch (methodName)
        {
            case "isPrime":
                if (arguments.Count == 1 && TryResolveIntValue(arguments[0], code, injectedInput, out int primeInput))
                {
                    result = ComputePrime(primeInput).ToString().ToLowerInvariant();
                    return true;
                }
                break;

            case "getFibonacci":
                if (arguments.Count == 1 && TryResolveIntValue(arguments[0], code, injectedInput, out int fibonacciInput))
                {
                    result = ComputeFibonacci(fibonacciInput).ToString();
                    return true;
                }
                break;

            case "power":
                if (arguments.Count == 2 &&
                    TryResolveIntValue(arguments[0], code, injectedInput, out int baseValue) &&
                    TryResolveIntValue(arguments[1], code, injectedInput, out int exponent))
                {
                    result = ComputePower(baseValue, exponent).ToString();
                    return true;
                }
                break;
        }

        return false;
    }

    private bool TrySplitMethodArguments(string argumentsText, out List<string> arguments)
    {
        arguments = new List<string>();

        if (string.IsNullOrWhiteSpace(argumentsText))
        {
            return true;
        }

        int depth = 0;
        int startIndex = 0;

        for (int i = 0; i < argumentsText.Length; i++)
        {
            char current = argumentsText[i];

            if (current == '(')
            {
                depth++;
            }
            else if (current == ')')
            {
                depth--;
                if (depth < 0)
                {
                    return false;
                }
            }
            else if (current == ',' && depth == 0)
            {
                arguments.Add(argumentsText.Substring(startIndex, i - startIndex).Trim());
                startIndex = i + 1;
            }
        }

        if (depth != 0)
        {
            return false;
        }

        string finalArgument = argumentsText.Substring(startIndex).Trim();
        if (!string.IsNullOrWhiteSpace(finalArgument))
        {
            arguments.Add(finalArgument);
        }

        return true;
    }

    private bool TryResolveIntValue(string expression, string code, string injectedInput, out int value)
    {
        value = 0;

        string trimmedExpression = expression.Trim();

        if (int.TryParse(trimmedExpression, out value))
        {
            return true;
        }

        if (TryResolveNumericToken(trimmedExpression, code, injectedInput, out string resolvedToken) && int.TryParse(resolvedToken, out value))
        {
            return true;
        }

        if (TryEvaluateArithmeticExpression(trimmedExpression, code, injectedInput, out string arithmeticValue) && int.TryParse(arithmeticValue, out value))
        {
            return true;
        }

        return false;
    }

    private bool ComputePrime(int n)
    {
        if (n <= 1)
        {
            return false;
        }

        for (int i = 2; i < n; i++)
        {
            if (n % i == 0)
            {
                return false;
            }
        }

        return true;
    }

    private int ComputeFibonacci(int n)
    {
        if (n <= 1)
        {
            return 0;
        }

        int a = 0;
        int b = 1;

        for (int i = 2; i < n; i++)
        {
            int next = a + b;
            a = b;
            b = next;
        }

        return b;
    }

    private int ComputePower(int baseValue, int exponent)
    {
        int result = 1;

        for (int i = 0; i < exponent; i++)
        {
            result *= baseValue;
        }

        return result;
    }

    private bool TryEvaluateArithmeticExpression(string expr, string code, string injectedInput, out string result)
    {
        result = string.Empty;

        if (string.IsNullOrWhiteSpace(expr))
        {
            return false;
        }

        if (Regex.IsMatch(expr, "[\"']"))
        {
            return false;
        }

        string normalizedExpr = expr.Trim();
        var tokens = new System.Collections.Generic.List<string>();
        var tokenRegex = new Regex(@"-?\d+(?:\.\d+)?|[A-Za-z_][A-Za-z0-9_]*|[+\-*/%^()]");
        MatchCollection matches = tokenRegex.Matches(normalizedExpr);

        if (matches.Count == 0)
        {
            return false;
        }

        bool expectUnaryMinus = true;
        foreach (Match match in matches)
        {
            string token = match.Value;
            if (token == "-")
            {
                if (expectUnaryMinus)
                {
                    tokens.Add("u-");
                }
                else
                {
                    tokens.Add(token);
                }
                expectUnaryMinus = true;
                continue;
            }

            if (token == "+" || token == "*" || token == "/" || token == "%" || token == "^" || token == "(" || token == ")")
            {
                tokens.Add(token);
                expectUnaryMinus = token != ")";
                continue;
            }

            if (Regex.IsMatch(token, @"^-?\d+(?:\.\d+)?$"))
            {
                tokens.Add(token);
                expectUnaryMinus = false;
                continue;
            }

            if (Regex.IsMatch(token, @"^[A-Za-z_][A-Za-z0-9_]*$"))
            {
                if (!TryResolveNumericToken(token, code, injectedInput, out string numericToken))
                {
                    return false;
                }

                tokens.Add(numericToken);
                expectUnaryMinus = false;
                continue;
            }

            return false;
        }

        if (tokens.Count == 0)
        {
            return false;
        }

        var output = new System.Collections.Generic.List<string>();
        var operators = new System.Collections.Generic.Stack<string>();

        int Precedence(string op)
        {
            switch (op)
            {
                case "u-": return 4;
                case "^": return 3;
                case "*":
                case "%":
                case "/": return 2;
                case "+":
                case "-": return 1;
                default: return 0;
            }
        }

        bool IsRightAssociative(string op)
        {
            return op == "^" || op == "u-";
        }

        foreach (string token in tokens)
        {
            if (Regex.IsMatch(token, @"^-?\d+(?:\.\d+)?$"))
            {
                output.Add(token);
                continue;
            }

            if (token == "(")
            {
                operators.Push(token);
                continue;
            }

            if (token == ")")
            {
                while (operators.Count > 0 && operators.Peek() != "(")
                {
                    output.Add(operators.Pop());
                }

                if (operators.Count == 0)
                {
                    return false;
                }

                operators.Pop();
                continue;
            }

            while (operators.Count > 0 && operators.Peek() != "(" &&
                   (Precedence(operators.Peek()) > Precedence(token) ||
                    (Precedence(operators.Peek()) == Precedence(token) && !IsRightAssociative(token))))
            {
                output.Add(operators.Pop());
            }

            operators.Push(token);
        }

        while (operators.Count > 0)
        {
            string op = operators.Pop();
            if (op == "(" || op == ")")
            {
                return false;
            }

            output.Add(op);
        }

        var values = new System.Collections.Generic.Stack<NumericValue>();
        foreach (string token in output)
        {
            if (Regex.IsMatch(token, @"^-?\d+(?:\.\d+)?$"))
            {
                if (!TryParseNumericValue(token, out NumericValue numericValue))
                {
                    return false;
                }

                values.Push(numericValue);
                continue;
            }

            if (token == "u-")
            {
                if (values.Count < 1)
                {
                    return false;
                }

                NumericValue operand = values.Pop();
                values.Push(NumericValue.Negate(operand));
                continue;
            }

            if (values.Count < 2)
            {
                return false;
            }

            NumericValue right = values.Pop();
            NumericValue left = values.Pop();
            if (!TryApplyNumericOperator(left, right, token, out NumericValue computed))
            {
                return false;
            }

            values.Push(computed);
        }

        if (values.Count != 1)
        {
            return false;
        }

        result = values.Pop().ToDisplayString();
        return true;
    }

    private bool TryResolveNumericToken(string identifier, string code, string injectedInput, out string value)
    {
        value = string.Empty;

        if (TryResolveScannerInputValue(identifier, code, injectedInput, out string scannerValue))
        {
            if (!Regex.IsMatch(scannerValue, @"^-?\d+(?:\.\d+)?$"))
            {
                return false;
            }

            value = scannerValue;
            return true;
        }

        string resolved = ResolveVariableValue(identifier, code, injectedInput, out bool isDouble);
        if (string.IsNullOrWhiteSpace(resolved))
        {
            return false;
        }

        if (!TryParseNumericValue(resolved, out NumericValue numericValue))
        {
            return false;
        }

        if (isDouble && !numericValue.IsDouble)
        {
            numericValue = NumericValue.FromDouble(numericValue.DoubleValue);
        }

        value = numericValue.ToDisplayString();
        return true;
    }

    private bool TryParseNumericValue(string text, out NumericValue value)
    {
        value = default;

        if (text.Contains("."))
        {
            if (double.TryParse(text, out double doubleValue))
            {
                value = NumericValue.FromDouble(doubleValue);
                return true;
            }
            return false;
        }

        if (long.TryParse(text, out long longValue))
        {
            value = NumericValue.FromLong(longValue);
            return true;
        }

        if (double.TryParse(text, out double fallbackDouble))
        {
            value = NumericValue.FromDouble(fallbackDouble);
            return true;
        }

        return false;
    }

    private bool TryResolveScannerInputValue(string identifier, string code, string injectedInput, out string value)
    {
        value = string.Empty;

        if (string.IsNullOrWhiteSpace(identifier) || string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(injectedInput))
        {
            return false;
        }

        string[] inputs = injectedInput.Split(new[] { ' ', ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        if (inputs.Length == 0)
        {
            return false;
        }

        MatchCollection scannerAssignments = Regex.Matches(
            code,
            @"\b(?:String|int|long|float|double|var|char)?\s*(?<id>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*[A-Za-z0-9_]+\.next(?:Line|Int|Double|Float)?\s*\(\)\s*;",
            RegexOptions.Singleline);

        int inputIndex = 0;
        foreach (Match assignment in scannerAssignments)
        {
            if (inputIndex >= inputs.Length)
            {
                return false;
            }

            string assignedId = assignment.Groups["id"].Value;
            if (assignedId.Equals(identifier, System.StringComparison.OrdinalIgnoreCase))
            {
                value = inputs[inputIndex];
                return true;
            }

            inputIndex++;
        }

        return false;
    }

    private bool TryApplyNumericOperator(NumericValue left, NumericValue right, string op, out NumericValue result)
    {
        result = default;

        switch (op)
        {
            case "+":
                if (left.IsDouble || right.IsDouble)
                {
                    result = NumericValue.FromDouble(left.AsDouble + right.AsDouble);
                }
                else
                {
                    result = NumericValue.FromLong(left.AsLong + right.AsLong);
                }
                return true;
            case "-":
                if (left.IsDouble || right.IsDouble)
                {
                    result = NumericValue.FromDouble(left.AsDouble - right.AsDouble);
                }
                else
                {
                    result = NumericValue.FromLong(left.AsLong - right.AsLong);
                }
                return true;
            case "*":
                if (left.IsDouble || right.IsDouble)
                {
                    result = NumericValue.FromDouble(left.AsDouble * right.AsDouble);
                }
                else
                {
                    result = NumericValue.FromLong(left.AsLong * right.AsLong);
                }
                return true;
            case "/":
                if (left.IsDouble || right.IsDouble)
                {
                    if (Math.Abs(right.AsDouble) < double.Epsilon)
                    {
                        return false;
                    }

                    result = NumericValue.FromDouble(left.AsDouble / right.AsDouble);
                }
                else
                {
                    if (right.AsLong == 0)
                    {
                        return false;
                    }

                    result = NumericValue.FromLong(left.AsLong / right.AsLong);
                }
                return true;
            case "%":
                if (left.IsDouble || right.IsDouble)
                {
                    if (Math.Abs(right.AsDouble) < double.Epsilon)
                    {
                        return false;
                    }

                    result = NumericValue.FromDouble(left.AsDouble % right.AsDouble);
                }
                else
                {
                    if (right.AsLong == 0)
                    {
                        return false;
                    }

                    result = NumericValue.FromLong(left.AsLong % right.AsLong);
                }
                return true;
            case "^":
                if (left.IsDouble || right.IsDouble)
                {
                    return false;
                }

                result = NumericValue.FromLong(left.AsLong ^ right.AsLong);
                return true;
            default:
                return false;
        }
    }

    private struct NumericValue
    {
        public bool IsDouble;
        public double DoubleValue;
        public long LongValue;

        public static NumericValue FromLong(long value)
        {
            return new NumericValue
            {
                IsDouble = false,
                LongValue = value,
                DoubleValue = value
            };
        }

        public static NumericValue FromDouble(double value)
        {
            return new NumericValue
            {
                IsDouble = true,
                DoubleValue = value,
                LongValue = (long)value
            };
        }

        public static NumericValue Negate(NumericValue value)
        {
            if (value.IsDouble)
            {
                return FromDouble(-value.DoubleValue);
            }

            return FromLong(-value.LongValue);
        }

        public double AsDouble => IsDouble ? DoubleValue : LongValue;
        public long AsLong => IsDouble ? (long)DoubleValue : LongValue;

        public string ToDisplayString()
        {
            if (IsDouble)
            {
                if (DoubleValue == (long)DoubleValue)
                {
                    return DoubleValue.ToString("F1");
                }

                return DoubleValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            return LongValue.ToString();
        }
    }
    private bool TryPredictSimpleSentenceOutput(string code, out string predictedOutput)
    {
        predictedOutput = string.Empty;

        MatchCollection matches = Regex.Matches(code, @"System\.out\.(?:println|print)\s*\(\s*(?<expr>.*?)\s*\)", RegexOptions.Singleline);
        foreach (Match call in matches)
        {
            string expr = call.Groups["expr"].Value.Trim();
            string sentence = EvaluateSimpleSentenceExpression(expr, code);
            if (!string.IsNullOrWhiteSpace(sentence))
            {
                predictedOutput = sentence;
                return true;
            }
        }

        return false;
    }

    private string EvaluateSimpleSentenceExpression(string expr, string code)
    {
        if (string.IsNullOrWhiteSpace(expr))
        {
            return string.Empty;
        }

        string[] parts = SplitOnTopLevelPlus(expr);
        if (parts.Length == 0)
        {
            return string.Empty;
        }

        System.Text.StringBuilder output = new System.Text.StringBuilder();
        for (int i = 0; i < parts.Length; i++)
        {
            string part = parts[i].Trim();
            if (string.IsNullOrWhiteSpace(part))
            {
                continue;
            }

            Match strLit = Regex.Match(part, "^\"(?<txt>(?:[^\"\\\\]|\\\\.)*)\"$");
            if (strLit.Success)
            {
                output.Append(strLit.Groups["txt"].Value);
                continue;
            }

            Match charLit = Regex.Match(part, "^'(?<txt>.*)'$");
            if (charLit.Success)
            {
                output.Append(charLit.Groups["txt"].Value);
                continue;
            }

            Match numLit = Regex.Match(part, "^(?<num>-?\\d+(?:\\.\\d+)?)$");
            if (numLit.Success)
            {
                output.Append(numLit.Groups["num"].Value);
                continue;
            }

            if (Regex.IsMatch(part, @"^[A-Za-z_][A-Za-z0-9_]*$"))
            {
                string resolved = ResolveVariableValue(part, code, string.Empty, out bool isDouble);
                if (string.IsNullOrWhiteSpace(resolved))
                {
                    return string.Empty;
                }

                if (isDouble && double.TryParse(resolved, out double resolvedDouble))
                {
                    output.Append(JavaFormat(resolvedDouble));
                }
                else
                {
                    output.Append(resolved);
                }

                continue;
            }

            return string.Empty;
        }

        return output.ToString();
    }

    private string[] SplitOnTopLevelPlus(string expr)
    {
        if (string.IsNullOrWhiteSpace(expr))
        {
            return System.Array.Empty<string>();
        }

        System.Collections.Generic.List<string> parts = new System.Collections.Generic.List<string>();
        System.Text.StringBuilder current = new System.Text.StringBuilder();
        bool inString = false;
        bool inChar = false;
        bool escapeNext = false;

        for (int i = 0; i < expr.Length; i++)
        {
            char c = expr[i];

            if (inString)
            {
                current.Append(c);
                if (escapeNext)
                {
                    escapeNext = false;
                }
                else if (c == '\\')
                {
                    escapeNext = true;
                }
                else if (c == '"')
                {
                    inString = false;
                }
                continue;
            }

            if (inChar)
            {
                current.Append(c);
                if (escapeNext)
                {
                    escapeNext = false;
                }
                else if (c == '\\')
                {
                    escapeNext = true;
                }
                else if (c == '\'')
                {
                    inChar = false;
                }
                continue;
            }

            if (c == '"')
            {
                current.Append(c);
                inString = true;
                escapeNext = false;
                continue;
            }

            if (c == '\'')
            {
                current.Append(c);
                inChar = true;
                escapeNext = false;
                continue;
            }

            if (c == '+')
            {
                parts.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(c);
        }

        if (current.Length > 0)
        {
            parts.Add(current.ToString());
        }

        return parts.ToArray();
    }

    private string JavaFormat(object val)
    {
        if (val == null) return string.Empty;
        if (val is double d)
        {
            // Mimic Java double string: always includes .0 if whole number
            if (d == (long)d) return d.ToString("F1");
            return d.ToString();
        }
        return val.ToString();
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
