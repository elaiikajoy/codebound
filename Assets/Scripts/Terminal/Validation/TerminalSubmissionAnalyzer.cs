using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Linq;

public static class TerminalSubmissionAnalyzer
{
    private sealed class AstFacts
    {
        public int PrintCalls;
        public int PrintlnCalls;
        public int IfStatements;
        public int ElseBlocks;
        public int ForLoops;
        public int WhileLoops;
        public int SwitchStatements;
        public int MethodDeclarations;
        public int VariableDeclarations;
        public int ArrayInitializers;
        public int ArrayIndexUses;
        public int ReturnStatements;
        public readonly List<string> MethodNames = new List<string>();
        public readonly List<string> CallNames = new List<string>();
        public readonly List<string> LiteralValues = new List<string>();
        public readonly List<string> IdentifierNames = new List<string>();
        public readonly List<string> Operators = new List<string>();
        public readonly List<string> DeclaredTypes = new List<string>();
        public int Assignments;
        public int Initializers;
    }

    public static TerminalSubmissionResult Analyze(string submittedCode, LevelData levelData, int sceneLevelNumber, string injectedInput)
    {
        TerminalSubmissionResult result = new TerminalSubmissionResult
        {
            IsValid = false,
            RequiresInput = false,
            Message = string.Empty,
            PredictedOutput = string.Empty,
            Ast = null
        };

        if (string.IsNullOrWhiteSpace(submittedCode))
        {
            result.Message = "Code is empty. Write your solution first.";
            return result;
        }

        List<string> allErrors = new List<string>();

        // ── Syntax pre-check ─────────────────────────────────────────────
        List<string> syntaxErrors = CheckSyntaxErrors(submittedCode);
        if (syntaxErrors.Count > 0)
        {
            allErrors.AddRange(syntaxErrors);
        }
        // ─────────────────────────────────────────────────────────────────

        TerminalLexer lexer = new TerminalLexer(submittedCode);
        List<TerminalToken> tokens = lexer.Tokenize();
        TerminalParser parser = new TerminalParser(tokens);
        TerminalCompilationUnitNode ast = parser.ParseCompilationUnit();
        result.Ast = ast;

        if (!HasExecutableContent(ast))
        {
            if (allErrors.Count == 0)
                allErrors.Add("Parse failed: no executable code was recognized.");

            result.Message = string.Join("\n", allErrors);
            return result;
        }

        AstFacts facts = CollectFacts(ast);
        TerminalRuleSet ruleSet = BuildRuleSet(levelData, sceneLevelNumber);

        List<string> astSymbolErrors = ValidateAstSymbolDiagnostics(ast);
        if (astSymbolErrors.Count > 0)
        {
            allErrors.AddRange(astSymbolErrors);
        }

        string validationMessage;
        List<string> ruleErrors = ValidateAllRules(ruleSet, facts, submittedCode);
        if (ruleErrors.Count > 0)
        {
            allErrors.AddRange(ruleErrors);
        }

        bool hasInjectedInput = !string.IsNullOrWhiteSpace(injectedInput);
        if (!hasInjectedInput && RequiresInteractiveInput(levelData, facts))
        {
            if (allErrors.Count > 0)
            {
                result.Message = string.Join("\n", allErrors);
                return result;
            }
            result.IsValid = true;
            result.RequiresInput = true;
            result.Message = "Interactive input required.";
            return result;
        }

        TerminalInterpreter interpreter = new TerminalInterpreter();
        string predictedOutput = string.Empty;
        try
        {
            predictedOutput = interpreter.Execute(ast, submittedCode, injectedInput ?? string.Empty);
        }
        catch (Exception ex)
        {
            string msg = ex.Message;
            int lineNum = -1;

            if (msg.StartsWith("Variable '") && msg.Contains("' is used"))
            {
                int start = msg.IndexOf('\'') + 1;
                int end = msg.IndexOf('\'', start);
                if (end > start)
                {
                    string varName = msg.Substring(start, end - start);
                    string[] lines = submittedCode.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                    for (int i = 0; i < lines.Length; i++)
                    {
                        if (System.Text.RegularExpressions.Regex.IsMatch(lines[i], @"\b" + System.Text.RegularExpressions.Regex.Escape(varName) + @"\b"))
                        {
                            if (!lines[i].Trim().StartsWith("//"))
                            {
                                lineNum = i + 1;
                                break;
                            }
                        }
                    }
                }
            }

            if (lineNum != -1)
            {
                if (msg.StartsWith("Variable '"))
                {
                    allErrors.Add(string.Format("Line {0}: Compilation Error: {1}", lineNum, msg));
                }
                else
                {
                    allErrors.Add(string.Format("Line {0}: Runtime Error: {1}", lineNum, msg));
                }
            }
            else
            {
                allErrors.Add("Runtime Error: " + msg);
            }
        }

        result.PredictedOutput = predictedOutput;

        if (!ValidateOutput(levelData, ast, interpreter, predictedOutput, submittedCode, injectedInput ?? string.Empty, sceneLevelNumber, out validationMessage))
        {
            allErrors.Add("[Result] " + validationMessage);
        }

        allErrors = allErrors.Distinct().ToList();

        if (allErrors.Count > 0)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine(string.Format("FOUND {0} ERROR(S) IN YOUR CODE:", allErrors.Count));
            sb.AppendLine();
            for (int i = 0; i < allErrors.Count; i++)
            {
                sb.AppendLine(string.Format("[{0}] {1}", i + 1, allErrors[i]));
            }
            result.Message = sb.ToString().TrimEnd();
            return result;
        }

        result.IsValid = true;
        result.Message = "Success.";
        return result;
    }

    // -------------------------------------------------------------------------
    // Syntax pre-checker
    // Collects ALL syntax errors found in the code and returns them together
    // so the student can see every mistake at once, not just the first one.
    // Works for all levels — no level-specific logic here.
    // -------------------------------------------------------------------------
    private static List<string> CheckSyntaxErrors(string code)
    {
        List<string> errors = new List<string>();

        // ── Pass 1: character-level scan ─────────────────────────────────────
        // Tracks brace/paren balance and detects unclosed string literals.
        int braceDepth = 0;
        int parenDepth = 0;
        bool inString = false;
        bool inChar = false;
        bool escape = false;
        int lineNum = 1;

        for (int i = 0; i < code.Length; i++)
        {
            char c = code[i];

            if (c == '\n') { lineNum++; }

            if (escape) { escape = false; continue; }
            if (c == '\\' && (inString || inChar)) { escape = true; continue; }

            // Skip single-line comments
            if (!inString && !inChar && c == '/' && i + 1 < code.Length && code[i + 1] == '/')
            {
                while (i < code.Length && code[i] != '\n') i++;
                continue;
            }

            // Skip block comments
            if (!inString && !inChar && c == '/' && i + 1 < code.Length && code[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < code.Length && !(code[i] == '*' && code[i + 1] == '/')) i++;
                i++;
                continue;
            }

            // Toggle string / char literals
            if (!inChar && c == '"') { inString = !inString; continue; }
            if (!inString && c == '\'') { inChar = !inChar; continue; }

            if (inString || inChar) continue;

            if (c == '{') braceDepth++;
            else if (c == '}') braceDepth--;
            else if (c == '(') parenDepth++;
            else if (c == ')') parenDepth--;
        }

        if (inString)
            errors.Add("Unclosed string literal — you opened a '\"' but never closed it.");

        if (braceDepth > 0)
            errors.Add(string.Format("Missing closing '}}' — you have {0} unclosed brace(s). Check every '{{' has a matching '}}'.", braceDepth));
        else if (braceDepth < 0)
            errors.Add(string.Format("Extra closing '}}' — you have {0} more '}}' than '{{'. Check your braces.", -braceDepth));

        if (parenDepth > 0)
            errors.Add(string.Format("Missing closing ')' — you have {0} unclosed parenthesis(es).", parenDepth));
        else if (parenDepth < 0)
            errors.Add(string.Format("Extra ')' found — you have {0} more ')' than '('. Check your parentheses.", -parenDepth));

        // ── Pass 2: collect all declared variable names from the code ─────────
        string[] lines = code.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
        HashSet<string> declaredVars = CollectDeclaredNames(lines);

        // ── Pass 3: line-by-line checks ──────────────────────────────────────
        for (int li = 0; li < lines.Length; li++)
        {
            string rawLine = lines[li];

            // For structural checks (semicolons, braces, keywords) we want the line
            // with comments removed but string/char literal content preserved so we
            // can still match println("...") patterns correctly.
            string lineNoComment = StripCommentsOnly(rawLine);
            string trimmed = lineNoComment.Trim();

            if (string.IsNullOrWhiteSpace(trimmed)) continue;
            if (trimmed.StartsWith("//")) continue;
            if (trimmed.StartsWith("import ", StringComparison.Ordinal) || trimmed.StartsWith("package ", StringComparison.Ordinal)) continue;
            if (trimmed.StartsWith("/*", StringComparison.Ordinal) || trimmed.StartsWith("*", StringComparison.Ordinal)) continue;

            int displayLine = li + 1;

            // ── Check A: unquoted text inside println() / print() ───────────
            // Rule: argument starts with a letter AND contains spaces with no
            //       operators or dots  ⟹ it's plain text, not a variable.
            //       Single-word identifiers (variable names) are allowed.
            int printIdx = trimmed.IndexOf("println(", StringComparison.Ordinal);
            if (printIdx < 0) printIdx = trimmed.IndexOf("print(", StringComparison.Ordinal);

            if (printIdx >= 0)
            {
                int openParen = trimmed.IndexOf('(', printIdx);
                if (openParen >= 0 && openParen + 1 < trimmed.Length)
                {
                    int argStart = openParen + 1;
                    while (argStart < trimmed.Length && trimmed[argStart] == ' ') argStart++;

                    if (argStart < trimmed.Length)
                    {
                        char firstArg = trimmed[argStart];
                        bool startsWithLetter = char.IsLetter(firstArg);
                        bool isQuoted = firstArg == '"' || firstArg == '\'';
                        bool isDigitOrSign = char.IsDigit(firstArg) || firstArg == '-';
                        bool isClosing = firstArg == ')';

                        if (startsWithLetter && !isQuoted && !isDigitOrSign && !isClosing)
                        {
                            int closeParen = trimmed.IndexOf(')', argStart);
                            if (closeParen > argStart)
                            {
                                string argText = trimmed.Substring(argStart, closeParen - argStart).Trim();
                                bool hasSpaces = argText.Contains(" ");
                                bool hasOperator = argText.Contains("+") || argText.Contains("-") ||
                                                   argText.Contains("*") || argText.Contains("/") ||
                                                   argText.Contains(".") || argText.Contains("(");

                                // Only flag if multi-word AND no operators (so variables pass through)
                                if (hasSpaces && !hasOperator)
                                {
                                    errors.Add(string.Format(
                                        "Line {0}: Text inside println() must be in quotes. Did you mean println(\"{1}\")?",
                                        displayLine, argText));
                                }
                            }
                        }
                    }
                }
            }

            // ── Check B: missing semicolon ──────────────────────────────────
            // Heuristic: a statement line ending with ')' that is NOT a
            // control-flow header, method declaration, or followed by '{'.
            if (trimmed.EndsWith(")"))
            {
                bool isControlFlow = trimmed.StartsWith("if") || trimmed.StartsWith("else if") ||
                                     trimmed.StartsWith("for") || trimmed.StartsWith("while") ||
                                     trimmed.StartsWith("switch") || trimmed.StartsWith("catch") ||
                                     trimmed.StartsWith("else");
                bool isMethodDecl = trimmed.Contains("void ") || trimmed.Contains("static ") ||
                                     trimmed.Contains("public ") || trimmed.Contains("private ") ||
                                     trimmed.Contains("protected ");

                if (!isControlFlow && !isMethodDecl)
                {
                    // Look ahead: if the next non-blank line opens a block, it's fine
                    string nextNonBlank = string.Empty;
                    for (int ni = li + 1; ni < lines.Length; ni++)
                    {
                        string nl = lines[ni].Trim();
                        if (!string.IsNullOrWhiteSpace(nl)) { nextNonBlank = nl; break; }
                    }

                    if (!nextNonBlank.StartsWith("{"))
                    {
                        errors.Add(string.Format(
                            "Line {0}: Missing semicolon ';' at the end of the statement.",
                            displayLine));
                    }
                }
            }

            // ── Check C: statement ending without semicolon (catches other forms) ─
            // Lines that end with a string literal closing quote or a number/identifier
            // and are clearly statements (not declarations with {, not control flow).
            bool endsWithQuote = trimmed.EndsWith("\"") || trimmed.EndsWith("'");
            bool endsWithLetterOrDigit = trimmed.Length > 0 && char.IsLetterOrDigit(trimmed[trimmed.Length - 1]);

            if ((endsWithQuote || endsWithLetterOrDigit) && !trimmed.EndsWith(";") && !trimmed.EndsWith(":") && !trimmed.EndsWith("{") && !trimmed.EndsWith("}"))
            {
                bool isComment = trimmed.StartsWith("//") || trimmed.StartsWith("*") || trimmed.StartsWith("/*");
                bool isConcatLine = trimmed.EndsWith("+") || trimmed.EndsWith("-") || trimmed.EndsWith("=") || trimmed.EndsWith("(");
                bool isControlFlow = trimmed.StartsWith("if") || trimmed.StartsWith("else") ||
                                     trimmed.StartsWith("for") || trimmed.StartsWith("while") ||
                                     trimmed.StartsWith("switch") || trimmed.StartsWith("catch") ||
                                     trimmed.StartsWith("public ") || trimmed.StartsWith("private ") ||
                                     trimmed.StartsWith("protected ") || trimmed.StartsWith("class ") ||
                                     trimmed.StartsWith("static ") || trimmed.StartsWith("void ") ||
                                     trimmed == "do";
                bool isCaseOrDefault = trimmed.StartsWith("case ") || trimmed.StartsWith("default:");

                if (!isComment && !isConcatLine && !isControlFlow && !isCaseOrDefault)
                {
                    // Look ahead for opening brace in case it's a method/class split across lines
                    string nextNonBlank = string.Empty;
                    for (int ni = li + 1; ni < lines.Length; ni++)
                    {
                        string nl = lines[ni].Trim();
                        if (!string.IsNullOrWhiteSpace(nl)) { nextNonBlank = nl; break; }
                    }

                    if (!nextNonBlank.StartsWith("{"))
                    {
                        errors.Add(string.Format(
                            "Line {0}: Missing semicolon ';' at the end of the statement.",
                            displayLine));
                    }
                }
            }

            // ── Check D: type mismatch on variable declaration ───────────────
            // Catches: String n = 1;   (numeric literal assigned to String)
            //          int n = "hi";   (string literal assigned to numeric type)
            // Uses a simple token-scan — no regex, no compiler needed.
            {
                // Identify the declared type keyword at the start of the line
                string[] numericTypes = { "int", "double", "float", "long", "short", "byte" };
                bool lineIsStringDecl = false;
                bool lineIsNumericDecl = false;
                string declaredType = string.Empty;

                if (trimmed.StartsWith("String ") || trimmed.StartsWith("String\t"))
                {
                    lineIsStringDecl = true;
                    declaredType = "String";
                }
                else
                {
                    for (int t = 0; t < numericTypes.Length; t++)
                    {
                        string numType = numericTypes[t];
                        if (trimmed.StartsWith(numType + " ") || trimmed.StartsWith(numType + "\t"))
                        {
                            lineIsNumericDecl = true;
                            declaredType = numType;
                            break;
                        }
                    }
                }

                // Find the '=' initializer value (skip past type name and variable name)
                if ((lineIsStringDecl || lineIsNumericDecl) && trimmed.Contains("="))
                {
                    int eqIdx = trimmed.IndexOf('=');
                    // Avoid '==' comparisons
                    if (eqIdx >= 0 && eqIdx + 1 < trimmed.Length && trimmed[eqIdx + 1] != '=')
                    {
                        string rhs = trimmed.Substring(eqIdx + 1).Trim();
                        // Strip trailing semicolon for inspection
                        if (rhs.EndsWith(";")) rhs = rhs.Substring(0, rhs.Length - 1).Trim();

                        bool rhsIsNumeric = rhs.Length > 0 &&
                                            (char.IsDigit(rhs[0]) || (rhs[0] == '-' && rhs.Length > 1 && char.IsDigit(rhs[1])));
                        bool rhsIsString = rhs.StartsWith("\"") || rhs.StartsWith("'");

                        if (lineIsStringDecl && rhsIsNumeric)
                        {
                            errors.Add(string.Format(
                                "Line {0}: Type error — cannot assign a number ({1}) to a String variable. " +
                                "Use String n = \"{1}\"; to store it as text, or change the type to int.",
                                displayLine, rhs));
                        }
                        else if (lineIsNumericDecl && rhsIsString)
                        {
                            errors.Add(string.Format(
                                "Line {0}: Type error — cannot assign a string literal ({1}) to a {2} variable. " +
                                "Remove the quotes to use a number, or change the type to String.",
                                displayLine, rhs, declaredType));
                        }
                    }
                }
            }

            // ── Check E: colon used instead of semicolon ─────────────────────
            // Catches: String name = "Elaika":
            if (trimmed.EndsWith(":"))
            {
                bool isSwitchCase = trimmed.StartsWith("case ") || trimmed == "default:" || trimmed.StartsWith("default:");
                bool isLabel = !trimmed.Contains(" ") && !trimmed.Contains("=") && !trimmed.Contains("(");
                if (!isSwitchCase && !isLabel)
                {
                    errors.Add(string.Format(
                        "Line {0}: Statement ends with ':' instead of ';'. Did you accidentally type a colon?",
                        displayLine));
                }
            }

            // ── Check F: comma instead of dot in method/field chain ───────────
            // Catches: System,out.println — comma between identifiers with no space
            {
                bool inStr = false; bool inChr = false;
                for (int ci = 1; ci < trimmed.Length - 1; ci++)
                {
                    char ch = trimmed[ci];
                    if (!inChr && ch == '"') { inStr = !inStr; continue; }
                    if (!inStr && ch == '\'') { inChr = !inChr; continue; }
                    if (inStr || inChr) continue;

                    if (ch == ',' && char.IsLetterOrDigit(trimmed[ci - 1]) && char.IsLetter(trimmed[ci + 1]))
                    {
                        // Make sure it's not inside parentheses (function args use commas legitimately)
                        int parensBefore = 0;
                        for (int pi = 0; pi < ci; pi++)
                        {
                            if (trimmed[pi] == '(') parensBefore++;
                            else if (trimmed[pi] == ')') parensBefore--;
                        }
                        if (parensBefore == 0) // not inside a call's arg list
                        {
                            int wStart = ci - 1;
                            while (wStart > 0 && char.IsLetterOrDigit(trimmed[wStart - 1])) wStart--;
                            int wEnd = ci + 1;
                            while (wEnd < trimmed.Length - 1 && char.IsLetterOrDigit(trimmed[wEnd + 1])) wEnd++;
                            string before = trimmed.Substring(wStart, ci - wStart);
                            string after = trimmed.Substring(ci + 1, wEnd - ci);
                            errors.Add(string.Format(
                                "Line {0}: Found '{1},{2}' — did you use a comma instead of a dot? Try '{1}.{2}'.",
                                displayLine, before, after));
                            break;
                        }
                    }
                }
            }

            // ── Check H: common Java class/method name typos ──────────────────
            {
                // lowercase 'system' instead of 'System'
                if (trimmed.Contains("system.out") || trimmed.Contains("system.Out"))
                    errors.Add(string.Format(
                        "Line {0}: 'system' should be 'System' (capital S) — Java is case-sensitive.",
                        displayLine));

                // Wrong println capitalisation
                if (trimmed.Contains(".printLine(") || trimmed.Contains(".PrintLn(") ||
                    trimmed.Contains(".Println(") || trimmed.Contains(".PRINTLN("))
                    errors.Add(string.Format(
                        "Line {0}: Wrong method name — use 'println' (all lowercase).",
                        displayLine));

                // int/double/float written with uppercase (common beginner mistake)
                if (trimmed.StartsWith("Int ") || trimmed.StartsWith("Int\t"))
                    errors.Add(string.Format("Line {0}: 'Int' should be 'int' (lowercase) in Java.", displayLine));
                if (trimmed.StartsWith("Double ") || trimmed.StartsWith("Float ") || trimmed.StartsWith("Boolean "))
                {
                    string used = trimmed.Split(' ')[0];
                    errors.Add(string.Format(
                        "Line {0}: '{1}' is a wrapper class — if you just want a number, use '{2}' (lowercase) instead.",
                        displayLine, used, used.ToLower()));
                }
            }



            // ── Check H: undeclared identifiers anywhere in the line ────────
            // This catches typos in assignments and expressions, not just print calls.
            {
                string scanLine = RemoveCommentsAndLiterals(trimmed);
                List<string> usedIds = ExtractIdentifiers(scanLine);
                HashSet<string> builtins = GetJavaBuiltins();

                foreach (string uid in usedIds)
                {
                    if (uid.Length == 0) continue;
                    if (char.IsDigit(uid[0])) continue;
                    if (builtins.Contains(uid)) continue;
                    if (declaredVars.Contains(uid)) continue;
                    if (TerminalLexer.IsKeyword(uid)) continue;

                    string closest = FindClosestName(uid, declaredVars);
                    if (!string.IsNullOrEmpty(closest))
                    {
                        errors.Add(string.Format(
                            "Line {0}: Variable '{1}' is not declared. Did you mean '{2}'?",
                            displayLine, uid, closest));
                    }
                    else
                    {
                        errors.Add(string.Format(
                            "Line {0}: Variable '{1}' is used but was never declared.",
                            displayLine, uid));
                    }
                }
            }
        }


        return errors.Distinct().ToList();
    }

    // ── Helpers for CheckSyntaxErrors ────────────────────────────────────────

    /// <summary>
    /// Strips ONLY single-line (//) and block (/* */) comments from a line.
    /// String and char literal content is preserved so println("Length: ") etc. remain intact.
    /// Used for structural syntax checks (missing semicolons, bracket balancing, etc.).
    /// </summary>
    private static string StripCommentsOnly(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        StringBuilder builder = new StringBuilder();
        bool inString = false;
        bool inChar = false;
        bool escape = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (escape) { builder.Append(c); escape = false; continue; }
            if (c == '\\' && (inString || inChar)) { builder.Append(c); escape = true; continue; }
            if (!inChar && c == '"') { inString = !inString; builder.Append(c); continue; }
            if (!inString && c == '\'') { inChar = !inChar; builder.Append(c); continue; }
            // Single-line comment outside literal — stop here
            if (!inString && !inChar && c == '/' && i + 1 < text.Length && text[i + 1] == '/')
                break;
            // Block comment outside literal — skip until end
            if (!inString && !inChar && c == '/' && i + 1 < text.Length && text[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < text.Length && !(text[i] == '*' && text[i + 1] == '/')) i++;
                i++; // consume closing /
                continue;
            }
            builder.Append(c);
        }
        return builder.ToString();
    }

    private static HashSet<string> CollectDeclaredNames(string[] lines)
    {
        HashSet<string> declared = new HashSet<string>(StringComparer.Ordinal);
        string[] typeKws = {
            "int", "double", "float", "long", "short", "byte", "char", "boolean",
            "String", "var", "Scanner", "StringBuilder", "Object"
        };
        string[] modifiers = { "public ", "private ", "protected ", "static ", "final " };

        foreach (string line in lines)
        {
            string t = line.Trim();
            if (string.IsNullOrWhiteSpace(t) || t.StartsWith("//")) continue;

            // Strip access modifiers
            string stripped = t;
            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (string mod in modifiers)
                {
                    if (stripped.StartsWith(mod)) { stripped = stripped.Substring(mod.Length); changed = true; }
                }
            }

            // Match: typeKw [[][]] identifier
            foreach (string kw in typeKws)
            {
                if (!stripped.StartsWith(kw + " ") && !stripped.StartsWith(kw + "[") && !stripped.StartsWith(kw + "\t")) continue;
                int ns = kw.Length;
                while (ns < stripped.Length && (stripped[ns] == '[' || stripped[ns] == ']')) ns++;
                while (ns < stripped.Length && stripped[ns] == ' ') ns++;
                if (ns >= stripped.Length || (!char.IsLetter(stripped[ns]) && stripped[ns] != '_')) break;
                int ne = ns;
                while (ne < stripped.Length && (char.IsLetterOrDigit(stripped[ne]) || stripped[ne] == '_')) ne++;
                string varName = stripped.Substring(ns, ne - ns);
                if (!string.IsNullOrEmpty(varName)) declared.Add(varName);
                break;
            }

            // Also catch for-loop variables: for (type name
            int forIdx = t.IndexOf("for (", StringComparison.Ordinal);
            if (forIdx < 0) forIdx = t.IndexOf("for(", StringComparison.Ordinal);
            if (forIdx >= 0)
            {
                int op2 = t.IndexOf('(', forIdx);
                if (op2 >= 0)
                {
                    string inside = t.Substring(op2 + 1).TrimStart();
                    foreach (string kw in typeKws)
                    {
                        if (!inside.StartsWith(kw + " ") && !inside.StartsWith(kw + "[")) continue;
                        int ns2 = kw.Length;
                        while (ns2 < inside.Length && (inside[ns2] == '[' || inside[ns2] == ']')) ns2++;
                        while (ns2 < inside.Length && inside[ns2] == ' ') ns2++;
                        int ne2 = ns2;
                        while (ne2 < inside.Length && (char.IsLetterOrDigit(inside[ne2]) || inside[ne2] == '_')) ne2++;
                        string v = inside.Substring(ns2, ne2 - ns2);
                        if (!string.IsNullOrEmpty(v)) declared.Add(v);
                        break;
                    }
                }
            }
        }
        return declared;
    }

    private static List<string> ExtractIdentifiers(string expr)
    {
        List<string> result = new List<string>();
        int i = 0;
        while (i < expr.Length)
        {
            if (char.IsLetter(expr[i]) || expr[i] == '_')
            {
                int start = i;
                while (i < expr.Length && (char.IsLetterOrDigit(expr[i]) || expr[i] == '_')) i++;
                result.Add(expr.Substring(start, i - start));
            }
            else { i++; }
        }
        return result;
    }

    /// <summary>
    /// Strips both comments AND the contents of string/char literals, leaving only
    /// identifiers, operators, and structural tokens.  Used for undeclared-identifier
    /// scanning so that text inside strings (e.g. "Length: ") is never mistaken for
    /// a variable reference.
    /// </summary>
    private static string RemoveCommentsAndLiterals(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder();
        bool inString = false;
        bool inChar = false;
        bool escape = false;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];

            if (escape)
            {
                escape = false;
                continue; // skip escaped char content
            }

            if (c == '\\' && (inString || inChar))
            {
                escape = true;
                continue;
            }

            if (!inChar && c == '"')
            {
                inString = !inString;
                continue; // don't emit the quote delimiter either
            }

            if (!inString && c == '\'')
            {
                inChar = !inChar;
                continue;
            }

            // Single-line comment: strip the rest of the line
            if (!inString && !inChar && c == '/' && i + 1 < text.Length && text[i + 1] == '/')
                break;

            // Block comment start: skip until end
            if (!inString && !inChar && c == '/' && i + 1 < text.Length && text[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < text.Length && !(text[i] == '*' && text[i + 1] == '/'))
                    i++;
                i++; // consume closing /
                continue;
            }

            if (inString || inChar)
                continue; // skip literal content

            builder.Append(c);
        }

        return builder.ToString();
    }

    private static HashSet<string> GetJavaBuiltins()
    {
        return new HashSet<string>(StringComparer.Ordinal)
        {
            "System","out","in","err","println","print","printf","format",
            "Math","abs","max","min","pow","sqrt","random","PI","E","floor","ceil","round",
            "Scanner","nextInt","nextDouble","nextFloat","nextBoolean","nextLong","nextLine","next","hasNext",
            "String","StringBuilder","length","charAt","substring","indexOf",
            "contains","equals","equalsIgnoreCase","toUpperCase","toLowerCase",
            "trim","split","replace","append","toString",
            "Integer","Double","Float","Long","Short","Byte","Character","Boolean",
            "parseInt","parseDouble","parseFloat","parseLong","valueOf",
            "int","double","float","long","short","byte","char","boolean","void",
            "true","false","null","this","super","new",
            "args","main","Solution","Object"
        };
    }

    private static string FindClosestName(string used, HashSet<string> declared)
    {
        // Returns a declared name that differs by at most 2 characters (edit-distance heuristic)
        string best = string.Empty;
        int bestDist = int.MaxValue;
        foreach (string d in declared)
        {
            int dist = SimpleEditDistance(used, d);
            if (dist < bestDist && dist <= 2) { bestDist = dist; best = d; }
        }
        return best;
    }

    private static int SimpleEditDistance(string a, string b)
    {
        if (a == b) return 0;
        if (a.Length == 0) return b.Length;
        if (b.Length == 0) return a.Length;
        int[,] d = new int[a.Length + 1, b.Length + 1];
        for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (int j = 0; j <= b.Length; j++) d[0, j] = j;
        for (int i = 1; i <= a.Length; i++)
            for (int j = 1; j <= b.Length; j++)
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                                   d[i - 1, j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
        return d[a.Length, b.Length];
    }

    private static bool RequiresInteractiveInput(LevelData levelData, AstFacts facts)
    {
        if (levelData == null)
        {
            return false;
        }

        if (facts != null && facts.CallNames.Count > 0)
        {
            return facts.CallNames.Contains("next") || facts.CallNames.Contains("nextInt") || facts.CallNames.Contains("nextDouble") || facts.CallNames.Contains("nextFloat") || facts.CallNames.Contains("nextLine");
        }

        if (levelData.testCases != null)
        {
            for (int i = 0; i < levelData.testCases.Length; i++)
            {
                LevelTestCase testCase = levelData.testCases[i];
                if (testCase == null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(testCase.input) && testCase.input != "*")
                {
                    return true;
                }
            }
        }

        int levelNumber = levelData.levelNumber > 0 ? levelData.levelNumber : 0;
        return levelNumber >= 21 && levelNumber <= 30;
    }

    private static bool HasExecutableContent(TerminalCompilationUnitNode ast)
    {
        if (ast == null)
        {
            return false;
        }

        if (ast.Methods != null && ast.Methods.Count > 0)
        {
            return true;
        }

        return ast.TopLevelStatements != null && ast.TopLevelStatements.Count > 0;
    }

    private static AstFacts CollectFacts(TerminalCompilationUnitNode ast)
    {
        AstFacts facts = new AstFacts();

        if (ast == null)
        {
            return facts;
        }

        for (int i = 0; i < ast.Methods.Count; i++)
        {
            TerminalMethodNode method = ast.Methods[i];
            if (method == null)
            {
                continue;
            }

            facts.MethodDeclarations++;
            if (!string.IsNullOrWhiteSpace(method.Name))
            {
                facts.MethodNames.Add(method.Name);
            }
            if (method.Body != null)
            {
                CollectFactsFromBlock(method.Body, facts);
            }
        }

        for (int i = 0; i < ast.TopLevelStatements.Count; i++)
        {
            CollectFactsFromStatement(ast.TopLevelStatements[i], facts);
        }

        return facts;
    }

    private static void CollectFactsFromBlock(TerminalBlockStatementNode block, AstFacts facts)
    {
        if (block == null)
        {
            return;
        }

        for (int i = 0; i < block.Statements.Count; i++)
        {
            CollectFactsFromStatement(block.Statements[i], facts);
        }
    }

    private static void CollectFactsFromStatement(TerminalStatementNode statement, AstFacts facts)
    {
        if (statement == null)
        {
            return;
        }

        facts.IdentifierNames.Add(statement.GetType().Name);

        TerminalBlockStatementNode block = statement as TerminalBlockStatementNode;
        if (block != null)
        {
            CollectFactsFromBlock(block, facts);
            return;
        }

        TerminalVariableDeclarationNode variableDeclaration = statement as TerminalVariableDeclarationNode;
        if (variableDeclaration != null)
        {
            facts.VariableDeclarations++;
            if (!string.IsNullOrWhiteSpace(variableDeclaration.TypeName))
            {
                facts.DeclaredTypes.Add(variableDeclaration.TypeName);
            }
            if (variableDeclaration.Initializer != null)
            {
                facts.Initializers++;
            }
            if (variableDeclaration.Initializer is TerminalArrayInitializerExpressionNode)
            {
                facts.ArrayInitializers++;
            }
            if (!string.IsNullOrWhiteSpace(variableDeclaration.Name))
            {
                facts.IdentifierNames.Add(variableDeclaration.Name);
            }
            CollectFactsFromExpression(variableDeclaration.Initializer, facts);
            return;
        }

        TerminalAssignmentNode assignment = statement as TerminalAssignmentNode;
        if (assignment != null)
        {
            facts.Assignments++;
            CollectFactsFromExpression(assignment.Target, facts);
            CollectFactsFromExpression(assignment.Value, facts);
            return;
        }

        TerminalExpressionStatementNode expressionStatement = statement as TerminalExpressionStatementNode;
        if (expressionStatement != null)
        {
            CollectFactsFromExpression(expressionStatement.Expression, facts);
            return;
        }

        TerminalIfStatementNode ifStatement = statement as TerminalIfStatementNode;
        if (ifStatement != null)
        {
            facts.IfStatements++;
            CollectFactsFromExpression(ifStatement.Condition, facts);
            CollectFactsFromBlock(ifStatement.ThenBlock, facts);
            if (ifStatement.ElseBlock != null)
            {
                facts.ElseBlocks++;
                CollectFactsFromBlock(ifStatement.ElseBlock, facts);
            }
            return;
        }

        TerminalForStatementNode forStatement = statement as TerminalForStatementNode;
        if (forStatement != null)
        {
            facts.ForLoops++;
            CollectFactsFromStatement(forStatement.Initializer, facts);
            CollectFactsFromExpression(forStatement.Condition, facts);
            CollectFactsFromExpression(forStatement.Update, facts);
            CollectFactsFromBlock(forStatement.Body, facts);
            return;
        }

        TerminalWhileStatementNode whileStatement = statement as TerminalWhileStatementNode;
        if (whileStatement != null)
        {
            facts.WhileLoops++;
            CollectFactsFromExpression(whileStatement.Condition, facts);
            CollectFactsFromBlock(whileStatement.Body, facts);
            return;
        }

        TerminalSwitchStatementNode switchStatement = statement as TerminalSwitchStatementNode;
        if (switchStatement != null)
        {
            facts.SwitchStatements++;
            CollectFactsFromExpression(switchStatement.Expression, facts);
            for (int i = 0; i < switchStatement.Sections.Count; i++)
            {
                TerminalSwitchSectionNode section = switchStatement.Sections[i];
                CollectFactsFromExpression(section.Label, facts);
                for (int j = 0; j < section.Statements.Count; j++)
                {
                    CollectFactsFromStatement(section.Statements[j], facts);
                }
            }
            return;
        }

        TerminalReturnStatementNode returnStatement = statement as TerminalReturnStatementNode;
        if (returnStatement != null)
        {
            facts.ReturnStatements++;
            CollectFactsFromExpression(returnStatement.Value, facts);
            return;
        }
    }

    private static void CollectFactsFromExpression(TerminalExpressionNode expression, AstFacts facts)
    {
        if (expression == null)
        {
            return;
        }

        TerminalLiteralExpressionNode literal = expression as TerminalLiteralExpressionNode;
        if (literal != null)
        {
            // Always add RawText (e.g. "15.0", "500", "true") so rules like containsLiteral: "15.0" match correctly.
            string rawText = literal.RawText ?? Convert.ToString(literal.Value, CultureInfo.InvariantCulture) ?? string.Empty;
            facts.LiteralValues.Add(rawText);
            // For string literals also add the unescaped string body separately, so
            // containsLiteral rules that reference substrings (e.g. "Length:") match.
            if (literal.Value is string)
            {
                string strVal = (string)literal.Value;
                if (!string.Equals(strVal, rawText, StringComparison.Ordinal))
                {
                    facts.LiteralValues.Add(strVal);
                }
            }
            return;
        }

        TerminalIdentifierExpressionNode identifier = expression as TerminalIdentifierExpressionNode;
        if (identifier != null)
        {
            facts.IdentifierNames.Add(identifier.Name);
            return;
        }

        TerminalUnaryExpressionNode unary = expression as TerminalUnaryExpressionNode;
        if (unary != null)
        {
            if (!string.IsNullOrWhiteSpace(unary.OperatorText)) facts.Operators.Add(unary.OperatorText);
            CollectFactsFromExpression(unary.Operand, facts);
            return;
        }

        TerminalBinaryExpressionNode binary = expression as TerminalBinaryExpressionNode;
        if (binary != null)
        {
            if (!string.IsNullOrWhiteSpace(binary.OperatorText)) facts.Operators.Add(binary.OperatorText);
            CollectFactsFromExpression(binary.Left, facts);
            CollectFactsFromExpression(binary.Right, facts);
            return;
        }

        TerminalMemberExpressionNode member = expression as TerminalMemberExpressionNode;
        if (member != null)
        {
            CollectFactsFromExpression(member.Target, facts);
            if (!string.IsNullOrWhiteSpace(member.MemberName))
            {
                facts.CallNames.Add(member.MemberName);
            }
            return;
        }

        TerminalCallExpressionNode call = expression as TerminalCallExpressionNode;
        if (call != null)
        {
            string callName = GetCallName(call.Callee);
            if (!string.IsNullOrWhiteSpace(callName))
            {
                facts.CallNames.Add(callName);
                if (string.Equals(callName, "println", StringComparison.OrdinalIgnoreCase) || string.Equals(callName, "print", StringComparison.OrdinalIgnoreCase))
                {
                    facts.PrintCalls++;
                    if (string.Equals(callName, "println", StringComparison.OrdinalIgnoreCase))
                    {
                        facts.PrintlnCalls++;
                    }
                }
            }

            CollectFactsFromExpression(call.Callee, facts);
            for (int i = 0; i < call.Arguments.Count; i++)
            {
                CollectFactsFromExpression(call.Arguments[i], facts);
            }
            return;
        }

        TerminalIndexExpressionNode index = expression as TerminalIndexExpressionNode;
        if (index != null)
        {
            facts.ArrayIndexUses++;
            CollectFactsFromExpression(index.Target, facts);
            CollectFactsFromExpression(index.Index, facts);
            return;
        }

        TerminalGroupExpressionNode group = expression as TerminalGroupExpressionNode;
        if (group != null)
        {
            CollectFactsFromExpression(group.Inner, facts);
            return;
        }

        TerminalArrayInitializerExpressionNode arrayInitializer = expression as TerminalArrayInitializerExpressionNode;
        if (arrayInitializer != null)
        {
            facts.ArrayInitializers++;
            for (int i = 0; i < arrayInitializer.Elements.Count; i++)
            {
                CollectFactsFromExpression(arrayInitializer.Elements[i], facts);
            }
            return;
        }

        TerminalNewExpressionNode creation = expression as TerminalNewExpressionNode;
        if (creation != null)
        {
            if (string.Equals(creation.TypeName, "Scanner", StringComparison.OrdinalIgnoreCase))
            {
                facts.CallNames.Add("Scanner");
            }
            for (int i = 0; i < creation.Arguments.Count; i++)
            {
                CollectFactsFromExpression(creation.Arguments[i], facts);
            }
        }
    }

    private static List<string> ValidateAstSymbolDiagnostics(TerminalCompilationUnitNode ast)
    {
        List<string> errors = new List<string>();
        if (ast == null)
        {
            return errors;
        }

        Dictionary<string, TerminalMethodNode> methods = new Dictionary<string, TerminalMethodNode>(StringComparer.OrdinalIgnoreCase);
        if (ast.Methods != null)
        {
            for (int i = 0; i < ast.Methods.Count; i++)
            {
                TerminalMethodNode method = ast.Methods[i];
                if (method == null || string.IsNullOrWhiteSpace(method.Name))
                {
                    continue;
                }

                if (methods.ContainsKey(method.Name))
                {
                    errors.Add(string.Format("Duplicate declaration: method '{0}' is defined more than once.", method.Name));
                    continue;
                }

                methods[method.Name] = method;
            }

            for (int i = 0; i < ast.Methods.Count; i++)
            {
                ValidateMethodSymbolDiagnostics(ast.Methods[i], methods, errors);
            }
        }

        if (ast.TopLevelStatements != null && ast.TopLevelStatements.Count > 0)
        {
            HashSet<string> visibleNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Stack<HashSet<string>> scopeStack = new Stack<HashSet<string>>();
            scopeStack.Push(new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            ValidateStatementListForSymbolDiagnostics(ast.TopLevelStatements, methods, errors, visibleNames, scopeStack, null);
        }

        return errors;
    }

    private static void ValidateMethodSymbolDiagnostics(TerminalMethodNode method, Dictionary<string, TerminalMethodNode> methods, List<string> errors)
    {
        if (method == null)
        {
            return;
        }

        HashSet<string> visibleNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Stack<HashSet<string>> scopeStack = new Stack<HashSet<string>>();
        HashSet<string> methodScope = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        scopeStack.Push(methodScope);

        if (method.Parameters != null)
        {
            for (int i = 0; i < method.Parameters.Count; i++)
            {
                TerminalParameterNode parameter = method.Parameters[i];
                if (parameter == null || string.IsNullOrWhiteSpace(parameter.Name))
                {
                    continue;
                }

                if (visibleNames.Contains(parameter.Name))
                {
                    errors.Add(string.Format("Duplicate declaration: parameter '{0}' is repeated in method '{1}'.", parameter.Name, method.Name));
                    continue;
                }

                visibleNames.Add(parameter.Name);
                methodScope.Add(parameter.Name);
            }
        }

        ValidateBlockForSymbolDiagnostics(method != null ? method.Body : null, methods, errors, visibleNames, scopeStack, method);
        scopeStack.Pop();
    }

    private static void ValidateBlockForSymbolDiagnostics(TerminalBlockStatementNode block, Dictionary<string, TerminalMethodNode> methods, List<string> errors, HashSet<string> visibleNames, Stack<HashSet<string>> scopeStack, TerminalMethodNode currentMethod)
    {
        if (block == null)
        {
            return;
        }

        HashSet<string> blockScope = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        scopeStack.Push(blockScope);

        if (block.Statements != null)
        {
            ValidateStatementListForSymbolDiagnostics(block.Statements, methods, errors, visibleNames, scopeStack, currentMethod);
        }

        scopeStack.Pop();
        foreach (string name in blockScope)
        {
            visibleNames.Remove(name);
        }
    }

    private static void ValidateStatementListForSymbolDiagnostics(List<TerminalStatementNode> statements, Dictionary<string, TerminalMethodNode> methods, List<string> errors, HashSet<string> visibleNames, Stack<HashSet<string>> scopeStack, TerminalMethodNode currentMethod)
    {
        if (statements == null)
        {
            return;
        }

        for (int i = 0; i < statements.Count; i++)
        {
            ValidateStatementForSymbolDiagnostics(statements[i], methods, errors, visibleNames, scopeStack, currentMethod);
        }
    }

    private static void ValidateStatementForSymbolDiagnostics(TerminalStatementNode statement, Dictionary<string, TerminalMethodNode> methods, List<string> errors, HashSet<string> visibleNames, Stack<HashSet<string>> scopeStack, TerminalMethodNode currentMethod)
    {
        if (statement == null)
        {
            return;
        }

        TerminalBlockStatementNode block = statement as TerminalBlockStatementNode;
        if (block != null)
        {
            ValidateBlockForSymbolDiagnostics(block, methods, errors, visibleNames, scopeStack, currentMethod);
            return;
        }

        TerminalVariableDeclarationNode variableDeclaration = statement as TerminalVariableDeclarationNode;
        if (variableDeclaration != null)
        {
            if (!string.IsNullOrWhiteSpace(variableDeclaration.Name))
            {
                if (visibleNames.Contains(variableDeclaration.Name))
                {
                    errors.Add(string.Format("Duplicate declaration: variable '{0}' is already defined in this scope.", variableDeclaration.Name));
                }
                else
                {
                    visibleNames.Add(variableDeclaration.Name);
                    if (scopeStack.Count > 0)
                    {
                        scopeStack.Peek().Add(variableDeclaration.Name);
                    }
                }
            }

            ValidateExpressionForSymbolDiagnostics(variableDeclaration.Initializer, methods, errors, visibleNames, currentMethod);
            return;
        }

        TerminalAssignmentNode assignment = statement as TerminalAssignmentNode;
        if (assignment != null)
        {
            ValidateExpressionForSymbolDiagnostics(assignment.Target, methods, errors, visibleNames, currentMethod);
            ValidateExpressionForSymbolDiagnostics(assignment.Value, methods, errors, visibleNames, currentMethod);
            return;
        }

        TerminalExpressionStatementNode expressionStatement = statement as TerminalExpressionStatementNode;
        if (expressionStatement != null)
        {
            ValidateExpressionForSymbolDiagnostics(expressionStatement.Expression, methods, errors, visibleNames, currentMethod);
            return;
        }

        TerminalIfStatementNode ifStatement = statement as TerminalIfStatementNode;
        if (ifStatement != null)
        {
            ValidateExpressionForSymbolDiagnostics(ifStatement.Condition, methods, errors, visibleNames, currentMethod);
            ValidateBlockForSymbolDiagnostics(ifStatement.ThenBlock, methods, errors, visibleNames, scopeStack, currentMethod);
            if (ifStatement.ElseBlock != null)
            {
                ValidateBlockForSymbolDiagnostics(ifStatement.ElseBlock, methods, errors, visibleNames, scopeStack, currentMethod);
            }
            return;
        }

        TerminalForStatementNode forStatement = statement as TerminalForStatementNode;
        if (forStatement != null)
        {
            HashSet<string> loopScope = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            scopeStack.Push(loopScope);

            ValidateStatementForSymbolDiagnostics(forStatement.Initializer, methods, errors, visibleNames, scopeStack, currentMethod);
            ValidateExpressionForSymbolDiagnostics(forStatement.Condition, methods, errors, visibleNames, currentMethod);
            ValidateExpressionForSymbolDiagnostics(forStatement.Update, methods, errors, visibleNames, currentMethod);
            ValidateBlockForSymbolDiagnostics(forStatement.Body, methods, errors, visibleNames, scopeStack, currentMethod);

            scopeStack.Pop();
            foreach (string name in loopScope)
            {
                visibleNames.Remove(name);
            }
            return;
        }

        TerminalWhileStatementNode whileStatement = statement as TerminalWhileStatementNode;
        if (whileStatement != null)
        {
            ValidateExpressionForSymbolDiagnostics(whileStatement.Condition, methods, errors, visibleNames, currentMethod);
            ValidateBlockForSymbolDiagnostics(whileStatement.Body, methods, errors, visibleNames, scopeStack, currentMethod);
            return;
        }

        TerminalSwitchStatementNode switchStatement = statement as TerminalSwitchStatementNode;
        if (switchStatement != null)
        {
            ValidateExpressionForSymbolDiagnostics(switchStatement.Expression, methods, errors, visibleNames, currentMethod);
            if (switchStatement.Sections != null)
            {
                for (int i = 0; i < switchStatement.Sections.Count; i++)
                {
                    TerminalSwitchSectionNode section = switchStatement.Sections[i];
                    if (section == null)
                    {
                        continue;
                    }

                    ValidateExpressionForSymbolDiagnostics(section.Label, methods, errors, visibleNames, currentMethod);
                    ValidateStatementListForSymbolDiagnostics(section.Statements, methods, errors, visibleNames, scopeStack, currentMethod);
                }
            }
            return;
        }

        TerminalReturnStatementNode returnStatement = statement as TerminalReturnStatementNode;
        if (returnStatement != null)
        {
            if (currentMethod != null)
            {
                bool isVoidMethod = string.Equals(currentMethod.ReturnType, "void", StringComparison.OrdinalIgnoreCase);
                if (isVoidMethod && returnStatement.Value != null)
                {
                    errors.Add(string.Format("Wrong return type: method '{0}' is void but returns a value.", currentMethod.Name));
                }
                else if (!isVoidMethod && returnStatement.Value == null)
                {
                    errors.Add(string.Format("Wrong return type: method '{0}' must return a value of type '{1}'.", currentMethod.Name, currentMethod.ReturnType));
                }
            }

            ValidateExpressionForSymbolDiagnostics(returnStatement.Value, methods, errors, visibleNames, currentMethod);
            return;
        }

        TerminalBreakStatementNode breakStatement = statement as TerminalBreakStatementNode;
        if (breakStatement != null)
        {
            return;
        }

        TerminalContinueStatementNode continueStatement = statement as TerminalContinueStatementNode;
        if (continueStatement != null)
        {
            return;
        }
    }

    private static void ValidateExpressionForSymbolDiagnostics(TerminalExpressionNode expression, Dictionary<string, TerminalMethodNode> methods, List<string> errors, HashSet<string> visibleNames, TerminalMethodNode currentMethod)
    {
        if (expression == null)
        {
            return;
        }

        TerminalUnaryExpressionNode unary = expression as TerminalUnaryExpressionNode;
        if (unary != null)
        {
            ValidateExpressionForSymbolDiagnostics(unary.Operand, methods, errors, visibleNames, currentMethod);
            return;
        }

        TerminalBinaryExpressionNode binary = expression as TerminalBinaryExpressionNode;
        if (binary != null)
        {
            ValidateExpressionForSymbolDiagnostics(binary.Left, methods, errors, visibleNames, currentMethod);
            ValidateExpressionForSymbolDiagnostics(binary.Right, methods, errors, visibleNames, currentMethod);
            return;
        }

        TerminalMemberExpressionNode member = expression as TerminalMemberExpressionNode;
        if (member != null)
        {
            ValidateExpressionForSymbolDiagnostics(member.Target, methods, errors, visibleNames, currentMethod);
            return;
        }

        TerminalCallExpressionNode call = expression as TerminalCallExpressionNode;
        if (call != null)
        {
            ValidateCallForSymbolDiagnostics(call, methods, errors, visibleNames, currentMethod);
            return;
        }

        TerminalIndexExpressionNode index = expression as TerminalIndexExpressionNode;
        if (index != null)
        {
            ValidateExpressionForSymbolDiagnostics(index.Target, methods, errors, visibleNames, currentMethod);
            ValidateExpressionForSymbolDiagnostics(index.Index, methods, errors, visibleNames, currentMethod);
            return;
        }

        TerminalGroupExpressionNode group = expression as TerminalGroupExpressionNode;
        if (group != null)
        {
            ValidateExpressionForSymbolDiagnostics(group.Inner, methods, errors, visibleNames, currentMethod);
            return;
        }

        TerminalArrayInitializerExpressionNode arrayInitializer = expression as TerminalArrayInitializerExpressionNode;
        if (arrayInitializer != null)
        {
            for (int i = 0; i < arrayInitializer.Elements.Count; i++)
            {
                ValidateExpressionForSymbolDiagnostics(arrayInitializer.Elements[i], methods, errors, visibleNames, currentMethod);
            }
            return;
        }

        TerminalNewExpressionNode creation = expression as TerminalNewExpressionNode;
        if (creation != null)
        {
            for (int i = 0; i < creation.Arguments.Count; i++)
            {
                ValidateExpressionForSymbolDiagnostics(creation.Arguments[i], methods, errors, visibleNames, currentMethod);
            }
            return;
        }
    }

    private static void ValidateCallForSymbolDiagnostics(TerminalCallExpressionNode call, Dictionary<string, TerminalMethodNode> methods, List<string> errors, HashSet<string> visibleNames, TerminalMethodNode currentMethod)
    {
        if (call == null)
        {
            return;
        }

        TerminalIdentifierExpressionNode identifier = call.Callee as TerminalIdentifierExpressionNode;
        if (identifier != null)
        {
            string callName = identifier.Name;
            if (!string.IsNullOrWhiteSpace(callName) && !IsBuiltinCallName(callName))
            {
                TerminalMethodNode method;
                if (!methods.TryGetValue(callName, out method))
                {
                    errors.Add(string.Format("Method not found: '{0}'.", callName));
                }
                else if (method.Parameters.Count != call.Arguments.Count)
                {
                    errors.Add(string.Format("Wrong parameters for method '{0}'. Expected {1}, found {2}.", callName, method.Parameters.Count, call.Arguments.Count));
                }
            }
        }

        ValidateExpressionForSymbolDiagnostics(call.Callee, methods, errors, visibleNames, currentMethod);
        for (int i = 0; i < call.Arguments.Count; i++)
        {
            ValidateExpressionForSymbolDiagnostics(call.Arguments[i], methods, errors, visibleNames, currentMethod);
        }
    }

    private static bool IsBuiltinCallName(string callName)
    {
        if (string.IsNullOrWhiteSpace(callName))
        {
            return true;
        }

        HashSet<string> builtins = GetJavaBuiltins();
        return builtins.Contains(callName);
    }

    private static string GetCallName(TerminalExpressionNode callee)
    {
        TerminalIdentifierExpressionNode identifier = callee as TerminalIdentifierExpressionNode;
        if (identifier != null)
        {
            return identifier.Name;
        }

        TerminalMemberExpressionNode member = callee as TerminalMemberExpressionNode;
        if (member != null)
        {
            return member.MemberName;
        }

        return string.Empty;
    }

    private sealed class TerminalRuleSet
    {
        public readonly List<TerminalAstRule> RequiredRules = new List<TerminalAstRule>();
        public readonly List<TerminalAstRule> ForbiddenRules = new List<TerminalAstRule>();
    }

    private static TerminalRuleSet BuildRuleSet(LevelData levelData, int sceneLevelNumber)
    {
        TerminalRuleSet ruleSet = new TerminalRuleSet();

        if (levelData != null)
        {
            if (levelData.requiredAstRules != null)
            {
                for (int i = 0; i < levelData.requiredAstRules.Length; i++)
                {
                    if (levelData.requiredAstRules[i] != null)
                    {
                        ruleSet.RequiredRules.Add(levelData.requiredAstRules[i]);
                    }
                }
            }

            if (levelData.forbiddenAstRules != null)
            {
                for (int i = 0; i < levelData.forbiddenAstRules.Length; i++)
                {
                    if (levelData.forbiddenAstRules[i] != null)
                    {
                        ruleSet.ForbiddenRules.Add(levelData.forbiddenAstRules[i]);
                    }
                }
            }
        }

        int levelNumber = levelData != null && levelData.levelNumber > 0 ? levelData.levelNumber : sceneLevelNumber;

        // --- Legacy JSON Keyword to AST Rule Mapping ---
        if (levelData != null)
        {
            if (levelData.requiredPrintlnCount > 0)
            {
                ruleSet.RequiredRules.Add(new TerminalAstRule { type = "countCall", value = "println", count = levelData.requiredPrintlnCount });
            }

            if (levelData.requiredKeywords != null)
            {
                for (int i = 0; i < levelData.requiredKeywords.Length; i++)
                {
                    string kw = levelData.requiredKeywords[i];
                    if (string.IsNullOrWhiteSpace(kw)) continue;

                    string trimmedKw = kw.Trim();

                    if (trimmedKw.Contains("println"))
                    {
                        ruleSet.RequiredRules.Add(new TerminalAstRule { type = "containsCall", value = "println" });
                        int start = trimmedKw.IndexOf('(');
                        int end = trimmedKw.LastIndexOf(')');
                        if (start >= 0 && end > start)
                        {
                            string inner = trimmedKw.Substring(start + 1, end - start - 1).Trim();
                            if (!string.IsNullOrEmpty(inner))
                            {
                                if (inner.StartsWith("\"") || inner.StartsWith("'") || char.IsDigit(inner[0]))
                                {
                                    ruleSet.RequiredRules.Add(new TerminalAstRule { type = "containsLiteral", value = inner.Trim(' ', '"', '\'') });
                                }
                                else
                                {
                                    ruleSet.RequiredRules.Add(new TerminalAstRule { type = "containsIdentifier", value = inner });
                                }
                            }
                        }
                    }
                    else if (trimmedKw.Contains("print"))
                    {
                        ruleSet.RequiredRules.Add(new TerminalAstRule { type = "containsCall", value = "print" });
                        int start = trimmedKw.IndexOf('(');
                        int end = trimmedKw.LastIndexOf(')');
                        if (start >= 0 && end > start)
                        {
                            string inner = trimmedKw.Substring(start + 1, end - start - 1).Trim();
                            if (!string.IsNullOrEmpty(inner))
                            {
                                if (inner.StartsWith("\"") || inner.StartsWith("'") || char.IsDigit(inner[0]))
                                {
                                    ruleSet.RequiredRules.Add(new TerminalAstRule { type = "containsLiteral", value = inner.Trim(' ', '"', '\'') });
                                }
                                else
                                {
                                    ruleSet.RequiredRules.Add(new TerminalAstRule { type = "containsIdentifier", value = inner });
                                }
                            }
                        }
                    }
                    else if (trimmedKw.StartsWith("\"") && trimmedKw.EndsWith("\""))
                    {
                        ruleSet.RequiredRules.Add(new TerminalAstRule { type = "containsLiteral", value = trimmedKw.Trim('"') });
                    }
                    else if (trimmedKw.StartsWith("'") && trimmedKw.EndsWith("'"))
                    {
                        ruleSet.RequiredRules.Add(new TerminalAstRule { type = "containsLiteral", value = trimmedKw.Trim('\'') });
                    }
                    else if (trimmedKw.Contains("Scanner")) ruleSet.RequiredRules.Add(new TerminalAstRule { type = "containsNode", value = "scanner" });
                    else if (trimmedKw.Contains("if")) ruleSet.RequiredRules.Add(new TerminalAstRule { type = "containsNode", value = "if" });
                    else if (trimmedKw.Contains("else")) ruleSet.RequiredRules.Add(new TerminalAstRule { type = "containsNode", value = "else" });
                    else if (trimmedKw.Contains("for")) ruleSet.RequiredRules.Add(new TerminalAstRule { type = "containsNode", value = "for" });
                    else if (trimmedKw.Contains("while")) ruleSet.RequiredRules.Add(new TerminalAstRule { type = "containsNode", value = "while" });
                    else if (trimmedKw.Contains("switch")) ruleSet.RequiredRules.Add(new TerminalAstRule { type = "containsNode", value = "switch" });
                    else if (trimmedKw.Contains("[]")) ruleSet.RequiredRules.Add(new TerminalAstRule { type = "containsNode", value = "array" });
                    else if (trimmedKw.StartsWith("String ") || trimmedKw.StartsWith("int ") || trimmedKw.StartsWith("double ") || trimmedKw.StartsWith("boolean ") || trimmedKw.StartsWith("float ") || trimmedKw.StartsWith("char ") || trimmedKw.StartsWith("long "))
                    {
                        ruleSet.RequiredRules.Add(new TerminalAstRule { type = "containsNode", value = "variable" });
                        string typeKw = trimmedKw.Substring(0, trimmedKw.IndexOf(' '));
                        ruleSet.RequiredRules.Add(new TerminalAstRule { type = "containsDeclaredType", value = typeKw });
                    }
                    else if (trimmedKw == "+" || trimmedKw == "-" || trimmedKw == "*" || trimmedKw == "/" || trimmedKw == "%" || trimmedKw == "==" || trimmedKw == "!=" || trimmedKw == "&&" || trimmedKw == "||")
                        ruleSet.RequiredRules.Add(new TerminalAstRule { type = "containsOperator", value = trimmedKw });
                    else if (trimmedKw.EndsWith("=") || trimmedKw.Contains(" = ") || trimmedKw == "=")
                        ruleSet.RequiredRules.Add(new TerminalAstRule { type = "containsNode", value = "assignment" });
                }
            }

            if (!string.IsNullOrWhiteSpace(levelData.requiredCodePattern))
            {
                // Extract string literals from regex pattern for AST validation
                string pattern = levelData.requiredCodePattern;
                int idx = 0;
                while ((idx = pattern.IndexOf("\"", idx)) != -1)
                {
                    int start = idx + 1;
                    int end = pattern.IndexOf("\"", start);
                    if (end != -1)
                    {
                        string literal = pattern.Substring(start, end - start);
                        if (!string.IsNullOrWhiteSpace(literal))
                        {
                            // Unescape common regex characters from the literal
                            literal = literal.Replace("\\.", ".").Replace("\\?", "?").Replace("\\+", "+").Replace("\\*", "*");
                            ruleSet.RequiredRules.Add(new TerminalAstRule { type = "containsLiteral", value = literal });
                        }
                        idx = end + 1;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            if (levelData.forbiddenKeywords != null)
            {
                for (int i = 0; i < levelData.forbiddenKeywords.Length; i++)
                {
                    string kw = levelData.forbiddenKeywords[i];
                    if (string.IsNullOrWhiteSpace(kw)) continue;

                    string trimmedKw = kw.Trim();
                    if (trimmedKw.Contains("println")) ruleSet.ForbiddenRules.Add(new TerminalAstRule { type = "containsCall", value = "println" });
                    else if (trimmedKw.Contains("print")) ruleSet.ForbiddenRules.Add(new TerminalAstRule { type = "containsCall", value = "print" });
                    else if (trimmedKw.Contains("for")) ruleSet.ForbiddenRules.Add(new TerminalAstRule { type = "containsNode", value = "for" });
                    else if (trimmedKw.Contains("while")) ruleSet.ForbiddenRules.Add(new TerminalAstRule { type = "containsNode", value = "while" });
                    else if (trimmedKw.Contains("if")) ruleSet.ForbiddenRules.Add(new TerminalAstRule { type = "containsNode", value = "if" });
                    else if (trimmedKw.Contains("else")) ruleSet.ForbiddenRules.Add(new TerminalAstRule { type = "containsNode", value = "else" });
                    else if (trimmedKw.Contains("switch")) ruleSet.ForbiddenRules.Add(new TerminalAstRule { type = "containsNode", value = "switch" });
                    else if (trimmedKw == "int" || trimmedKw == "double" || trimmedKw == "String" || trimmedKw == "boolean" || trimmedKw == "float" || trimmedKw == "char" || trimmedKw == "long")
                        ruleSet.ForbiddenRules.Add(new TerminalAstRule { type = "containsDeclaredType", value = trimmedKw });
                    else if (trimmedKw == "+" || trimmedKw == "-" || trimmedKw == "*" || trimmedKw == "/")
                        ruleSet.ForbiddenRules.Add(new TerminalAstRule { type = "containsOperator", value = trimmedKw });
                    else
                        ruleSet.ForbiddenRules.Add(new TerminalAstRule { type = "containsLiteral", value = trimmedKw });
                }
            }
        }
        // -----------------------------------------------

        if (levelNumber >= 1 && levelNumber <= 10)
        {
            if (levelData == null || levelData.requiredPrintlnCount == 0)
            {
                ruleSet.RequiredRules.Add(new TerminalAstRule { type = "requireOutputPrefix" });
            }
        }
        else if (levelNumber >= 11 && levelNumber <= 20)
        {
            ruleSet.RequiredRules.Add(new TerminalAstRule { type = "containsNode", value = "variable", count = 1 });
            if (levelData == null || levelData.requiredPrintlnCount == 0)
            {
                ruleSet.RequiredRules.Add(new TerminalAstRule { type = "requireOutputPrefix" });
            }
        }
        else if (levelNumber >= 21 && levelNumber <= 30)
        {
            ruleSet.RequiredRules.Add(new TerminalAstRule { type = "containsNode", value = "scanner", count = 1 });
            ruleSet.RequiredRules.Add(new TerminalAstRule { type = "containsCall", value = "next", count = 1 });
        }
        else if (levelNumber >= 31 && levelNumber <= 40)
        {
            ruleSet.RequiredRules.Add(new TerminalAstRule { type = "containsNode", value = "if", count = 1 });
            ruleSet.RequiredRules.Add(new TerminalAstRule { type = "containsNode", value = "else", count = 1 });
        }
        else if (levelNumber >= 41 && levelNumber <= 50)
        {
            ruleSet.RequiredRules.Add(new TerminalAstRule { type = "containsNode", value = "switch", count = 1 });
        }
        else if (levelNumber >= 51 && levelNumber <= 60)
        {
            ruleSet.RequiredRules.Add(new TerminalAstRule { type = "containsNode", value = "loop", count = 1 });
        }
        else if (levelNumber >= 61 && levelNumber <= 70)
        {
            ruleSet.RequiredRules.Add(new TerminalAstRule { type = "containsNode", value = "array", count = 1 });
        }
        else if (levelNumber >= 71 && levelNumber <= 80)
        {
            ruleSet.RequiredRules.Add(new TerminalAstRule { type = "containsNode", value = "string", count = 1 });
        }
        else if (levelNumber >= 81 && levelNumber <= 90)
        {
            ruleSet.RequiredRules.Add(new TerminalAstRule { type = "containsNode", value = "method", count = 1 });
        }

        return ruleSet;
    }

    private static List<string> ValidateAllRules(TerminalRuleSet ruleSet, AstFacts facts, string submittedCode)
    {
        List<string> errors = new List<string>();
        if (ruleSet == null)
        {
            return errors;
        }

        string message;
        for (int i = 0; i < ruleSet.RequiredRules.Count; i++)
        {
            if (!ValidateRule(ruleSet.RequiredRules[i], facts, true, out message))
            {
                errors.Add("[Task] " + message);
            }
        }

        string[] lines = string.IsNullOrWhiteSpace(submittedCode) ? new string[0] : submittedCode.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

        for (int i = 0; i < ruleSet.ForbiddenRules.Count; i++)
        {
            if (!ValidateRule(ruleSet.ForbiddenRules[i], facts, false, out message))
            {
                // Try to find where the forbidden rule was broken
                string value = ruleSet.ForbiddenRules[i].value ?? string.Empty;
                int lineNum = -1;

                if (!string.IsNullOrWhiteSpace(value))
                {
                    for (int j = 0; j < lines.Length; j++)
                    {
                        if (lines[j].Contains(value) && !lines[j].Trim().StartsWith("//"))
                        {
                            lineNum = j + 1;
                            break;
                        }
                    }
                }

                if (lineNum != -1)
                {
                    errors.Add(string.Format("Line {0}: {1}", lineNum, message));
                }
                else
                {
                    errors.Add("[Forbidden] " + message);
                }
            }
        }

        return errors;
    }

    private static bool ValidateRule(TerminalAstRule rule, AstFacts facts, bool required, out string message)
    {
        message = string.Empty;
        if (rule == null || facts == null)
        {
            return true;
        }

        string type = (rule.type ?? string.Empty).Trim();
        string value = (rule.value ?? string.Empty).Trim();
        int count = rule.count <= 0 ? 1 : rule.count;

        bool satisfied = false;

        switch (type)
        {
            case "countCall":
                satisfied = GetCallCount(facts, value) >= count;
                if (required && !satisfied)
                {
                    message = string.Format("Expected at least {0} call(s) to {1}.", count, value);
                }
                break;
            case "containsCall":
                satisfied = ContainsCall(facts, value);
                if (required && !satisfied)
                {
                    message = string.Format("Missing required call: {0}.", value);
                }
                else if (!required && satisfied)
                {
                    message = string.Format("Forbidden call used: {0}.", value);
                }
                break;
            case "containsLiteral":
                satisfied = ContainsLiteral(facts, value);
                if (required && !satisfied)
                {
                    message = string.Format("Missing required literal: {0}.", value);
                }
                else if (!required && satisfied)
                {
                    message = string.Format("Forbidden literal used: {0}.", value);
                }
                break;
            case "containsNode":
                satisfied = ContainsNode(facts, value);
                if (required && !satisfied)
                {
                    message = string.Format("Missing required structure: {0}.", value);
                }
                else if (!required && satisfied)
                {
                    message = string.Format("Forbidden structure used: {0}.", value);
                }
                break;
            case "containsIdentifier":
                satisfied = facts.IdentifierNames.Contains(value);
                if (required && !satisfied)
                {
                    message = string.Format("Missing required identifier: {0}.", value);
                }
                else if (!required && satisfied)
                {
                    message = string.Format("Forbidden identifier used: {0}.", value);
                }
                break;
            case "requireOutputPrefix":
                satisfied = facts.PrintlnCalls > 0 || facts.PrintCalls > 0;
                if (required && !satisfied)
                {
                    message = "Expected a call to System.out.print() or System.out.println().";
                }
                break;
            case "containsOperator":
                satisfied = facts.Operators.Contains(value);
                if (required && !satisfied)
                {
                    message = string.Format("Missing required operator: '{0}'.", value);
                }
                else if (!required && satisfied)
                {
                    message = string.Format("Forbidden operator used: '{0}'.", value);
                }
                break;
            case "containsDeclaredType":
                satisfied = facts.DeclaredTypes.Contains(value);
                if (required && !satisfied)
                {
                    message = string.Format("Missing required data type: {0}.", value);
                }
                else if (!required && satisfied)
                {
                    message = string.Format("Forbidden data type used: {0}.", value);
                }
                break;
            default:
                satisfied = true;
                break;
        }

        if (!required)
        {
            satisfied = !satisfied;
        }

        return satisfied;
    }

    private static int GetCallCount(AstFacts facts, string value)
    {
        if (facts == null || string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < facts.CallNames.Count; i++)
        {
            if (string.Equals(facts.CallNames[i], value, StringComparison.OrdinalIgnoreCase))
            {
                count++;
            }
        }

        if (string.Equals(value, "println", StringComparison.OrdinalIgnoreCase))
        {
            return facts.PrintlnCalls;
        }

        if (string.Equals(value, "print", StringComparison.OrdinalIgnoreCase))
        {
            return facts.PrintCalls;
        }

        return count;
    }

    private static bool ContainsCall(AstFacts facts, string value)
    {
        if (facts == null || string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        foreach (string callName in facts.CallNames)
        {
            if (callName.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsLiteral(AstFacts facts, string value)
    {
        if (facts == null || string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        bool isValueNumeric = double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out double numValue);

        foreach (string literal in facts.LiteralValues)
        {
            if (isValueNumeric)
            {
                if (double.TryParse(literal, NumberStyles.Any, CultureInfo.InvariantCulture, out double numLiteral))
                {
                    if (Math.Abs(numLiteral - numValue) < 0.000001)
                    {
                        return true;
                    }
                }
            }
            else
            {
                if (literal.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool ContainsNode(AstFacts facts, string value)
    {
        if (facts == null || string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        switch (value.ToLowerInvariant())
        {
            case "if":
                return facts.IfStatements > 0;
            case "else":
                return facts.ElseBlocks > 0;
            case "for":
            case "while":
            case "loop":
                return facts.ForLoops > 0 || facts.WhileLoops > 0;
            case "switch":
                return facts.SwitchStatements > 0;
            case "method":
                return facts.MethodDeclarations > 0;
            case "variable":
                return facts.VariableDeclarations > 0;
            case "assignment":
                return facts.Assignments > 0 || facts.Initializers > 0;
            case "array":
                return facts.ArrayInitializers > 0 || facts.ArrayIndexUses > 0;
            case "scanner":
                return facts.CallNames.Contains("Scanner") || facts.CallNames.Contains("nextInt") || facts.CallNames.Contains("nextLine") || facts.CallNames.Contains("nextDouble") || facts.CallNames.Contains("nextFloat");
            case "string":
                return facts.CallNames.Contains("length") || facts.CallNames.Contains("charAt") || facts.CallNames.Contains("substring") || facts.CallNames.Contains("indexOf") || ContainsLiteral(facts, " ");
            case "update":
                return facts.CallNames.Contains("nextInt") || facts.CallNames.Contains("nextLine") || facts.CallNames.Contains("nextDouble") || facts.CallNames.Contains("nextFloat");
            default:
                return facts.IdentifierNames.Contains(value) || facts.CallNames.Contains(value);
        }
    }

    private static string Normalize(string text)
    {
        if (text == null)
        {
            return string.Empty;
        }

        return text.Replace("\r\n", "\n").Replace("\r", "\n").Trim();
    }

    private static bool ValidateOutput(LevelData levelData, TerminalCompilationUnitNode ast, TerminalInterpreter interpreter, string predictedOutput, string submittedCode, string injectedInput, int sceneLevelNumber, out string message)
    {
        message = string.Empty;
        if (levelData == null)
        {
            return true;
        }

        // 1. MD Rule Compliance: "no regex output checks -> AST evaluator or testcase-based output checks"
        // 2. Usability: Allow user's prompts (e.g. System.out.print("Enter name:")) by checking if output CONTAINS the expected string.

        // For dynamic interactive levels like Level 22, checking exact string equality of a single testcase is problematic if the player
        // is meant to inject their OWN custom terminal input rather than matching a hidden predefined input.
        // We will validate if they did the work by building a 'Target Expected String' using the actual injectedInput they typed.
        if (levelData.levelNumber == 22 && !string.IsNullOrWhiteSpace(injectedInput))
        {
            // Level 22 Rule: "Output: '[Name] loves coding in [Language]!'"
            string[] parts = injectedInput.Split(new char[] { '\n', '\r', '\v' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                string expectedDynamicParams = string.Format("{0} loves coding in {1}!", parts[0].Trim(), parts[1].Trim()).ToLowerInvariant();
                string predictedNormDynamic = Normalize(predictedOutput).ToLowerInvariant();

                if (!predictedNormDynamic.Contains(expectedDynamicParams))
                {
                    message = string.Format("Output mismatch with your input.\nExpected it to contain: '{0} loves coding in {1}!'\nOutput Found:\n{2}", parts[0].Trim(), parts[1].Trim(), predictedOutput);
                    return false;
                }
                return true;
            }
        }

        if (levelData.levelNumber == 23 && !string.IsNullOrWhiteSpace(injectedInput))
        {
            string[] parts = injectedInput.Split(new char[] { '\n', '\r', '\v' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                double price;
                int quantity;

                if (!double.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out price))
                {
                    message = "Invalid price input. Expected a numeric value for the first line.";
                    return false;
                }

                if (!int.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out quantity))
                {
                    message = "Invalid quantity input. Expected an integer value for the second line.";
                    return false;
                }

                double expectedTotal = (price * quantity) + 50.0d;
                string normalizedOutput = Normalize(predictedOutput);

                if (normalizedOutput.IndexOf("Total Bill:", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    message = string.Format("Missing required label 'Total Bill:' in output.\nOutput Found:\n{0}", predictedOutput);
                    return false;
                }

                double foundTotal;
                if (!TryExtractNumberAfterLabel(normalizedOutput, "Total Bill:", out foundTotal))
                {
                    message = string.Format("Could not parse numeric value after 'Total Bill:'.\nOutput Found:\n{0}", predictedOutput);
                    return false;
                }

                if (Math.Abs(foundTotal - expectedTotal) > 0.0001d)
                {
                    message = string.Format("Output mismatch with your input.\nExpected total: {0}\nFound total: {1}\nOutput Found:\n{2}", expectedTotal.ToString("0.####", CultureInfo.InvariantCulture), foundTotal.ToString("0.####", CultureInfo.InvariantCulture), predictedOutput);
                    return false;
                }

                return true;
            }
        }

        if (levelData.testCases != null && levelData.testCases.Length > 0)
        {
            bool hasValidTestCases = false;
            for (int i = 0; i < levelData.testCases.Length; i++)
            {
                LevelTestCase testCase = levelData.testCases[i];
                if (testCase == null || (string.IsNullOrWhiteSpace(testCase.expectedOutput) && string.IsNullOrWhiteSpace(testCase.output)))
                {
                    continue;
                }

                hasValidTestCases = true;
                string tcExpectedRaw = !string.IsNullOrWhiteSpace(testCase.expectedOutput) ? testCase.expectedOutput : testCase.output;
                string tcExpected = Normalize(tcExpectedRaw).ToLowerInvariant();
                string tcInput = testCase.input != null ? testCase.input.Replace("\\n", "\n") : string.Empty;

                string tcOutputRaw = predictedOutput;

                // Evaluate AST specifically for this test case input (if necessary)
                if (!string.IsNullOrWhiteSpace(tcInput) && tcInput != "*")
                {
                    try
                    {
                        tcOutputRaw = interpreter.Execute(ast, submittedCode, tcInput);
                    }
                    catch (Exception)
                    {
                        tcOutputRaw = string.Empty;
                    }
                }

                string tcOutputNorm = Normalize(tcOutputRaw).ToLowerInvariant();

                string tcExpectedStripped = System.Text.RegularExpressions.Regex.Replace(tcExpected, @"\s+", "");
                string tcOutputStripped = System.Text.RegularExpressions.Regex.Replace(tcOutputNorm, @"\s+", "");

                if (!tcOutputStripped.Contains(tcExpectedStripped))
                {
                    message = string.Format("Failed testcase input\nExpected it to contain: '{0}'\nOutput Found:\n{1}", tcExpectedRaw, tcOutputRaw);
                    return false;
                }
            }

            if (hasValidTestCases)
            {
                return true;
            }
        }

        // Fallback for levels that ONLY have string expectedOutput (no testcases)
        string fallbackExpectedRaw = GetExpectedOutput(levelData);
        if (!string.IsNullOrWhiteSpace(fallbackExpectedRaw))
        {
            string fallbackExpected = Normalize(fallbackExpectedRaw).ToLowerInvariant();
            string predictedNorm = Normalize(predictedOutput).ToLowerInvariant();

            if (!predictedNorm.Contains(fallbackExpected))
            {
                message = string.Format("Output mismatch.\nExpected it to contain: '{0}'\nOutput Found:\n{1}", fallbackExpectedRaw, predictedOutput);
                return false;
            }
        }

        return true;
    }

    private static bool TryExtractNumberAfterLabel(string output, string label, out double value)
    {
        value = 0d;
        if (string.IsNullOrWhiteSpace(output) || string.IsNullOrWhiteSpace(label))
        {
            return false;
        }

        int labelIndex = output.IndexOf(label, StringComparison.OrdinalIgnoreCase);
        if (labelIndex < 0)
        {
            return false;
        }

        int i = labelIndex + label.Length;
        while (i < output.Length && char.IsWhiteSpace(output[i]))
        {
            i++;
        }

        if (i >= output.Length)
        {
            return false;
        }

        StringBuilder token = new StringBuilder();
        bool hasDigit = false;
        while (i < output.Length)
        {
            char c = output[i];
            bool isNumberChar = char.IsDigit(c) || c == '.' || c == '-' || c == '+' || c == 'e' || c == 'E';
            if (!isNumberChar)
            {
                break;
            }

            if (char.IsDigit(c))
            {
                hasDigit = true;
            }

            token.Append(c);
            i++;
        }

        if (!hasDigit)
        {
            return false;
        }

        return double.TryParse(token.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static string GetExpectedOutput(LevelData levelData)
    {
        if (levelData == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(levelData.expectedOutput))
        {
            return levelData.expectedOutput;
        }

        if (levelData.testCases != null)
        {
            for (int i = 0; i < levelData.testCases.Length; i++)
            {
                LevelTestCase testCase = levelData.testCases[i];
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
}
