using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

public enum TerminalTokenType
{
    Identifier,
    Number,
    String,
    Char,
    Symbol,
    EndOfFile
}

public sealed class TerminalToken
{
    public TerminalTokenType Type;
    public string Text;
    public int Position;

    public TerminalToken(TerminalTokenType type, string text, int position)
    {
        Type = type;
        Text = text;
        Position = position;
    }
}

public sealed class TerminalLexer
{
    private readonly string source;
    private int position;

    private static readonly HashSet<string> Keywords = new HashSet<string>(StringComparer.Ordinal)
    {
        "abstract", "assert", "break", "case", "catch", "class", "continue", "default", "do", "else",
        "extends", "final", "finally", "for", "if", "implements", "import", "interface", "new",
        "package", "private", "protected", "public", "return", "static", "switch", "this", "throw",
        "try", "void", "while", "var", "true", "false", "null"
    };

    public TerminalLexer(string source)
    {
        this.source = source ?? string.Empty;
    }

    public List<TerminalToken> Tokenize()
    {
        List<TerminalToken> tokens = new List<TerminalToken>();

        while (!IsAtEnd())
        {
            SkipWhitespaceAndComments();
            if (IsAtEnd())
            {
                break;
            }

            int start = position;
            char current = Advance();

            if (IsIdentifierStart(current))
            {
                while (!IsAtEnd() && IsIdentifierPart(Peek()))
                {
                    Advance();
                }

                string text = source.Substring(start, position - start);
                tokens.Add(new TerminalToken(TerminalTokenType.Identifier, text, start));
                continue;
            }

            if (char.IsDigit(current))
            {
                while (!IsAtEnd() && (char.IsDigit(Peek()) || Peek() == '.'))
                {
                    Advance();
                }

                // Support f, F (float) or L (long) suffix
                if (!IsAtEnd() && (Peek() == 'f' || Peek() == 'F' || Peek() == 'L'))
                {
                    Advance();
                }

                tokens.Add(new TerminalToken(TerminalTokenType.Number, source.Substring(start, position - start), start));
                continue;
            }

            if (current == '"')
            {
                bool escape = false;
                while (!IsAtEnd())
                {
                    char next = Advance();
                    if (escape)
                    {
                        escape = false;
                        continue;
                    }

                    if (next == '\\')
                    {
                        escape = true;
                        continue;
                    }

                    if (next == '"')
                    {
                        break;
                    }
                }

                tokens.Add(new TerminalToken(TerminalTokenType.String, source.Substring(start, position - start), start));
                continue;
            }

            if (current == '\'')
            {
                bool escape = false;
                while (!IsAtEnd())
                {
                    char next = Advance();
                    if (escape)
                    {
                        escape = false;
                        continue;
                    }

                    if (next == '\\')
                    {
                        escape = true;
                        continue;
                    }

                    if (next == '\'')
                    {
                        break;
                    }
                }

                tokens.Add(new TerminalToken(TerminalTokenType.Char, source.Substring(start, position - start), start));
                continue;
            }

            string symbol = current.ToString();
            if (!IsAtEnd())
            {
                char next = Peek();
                string two = symbol + next;
                if (IsMultiCharSymbol(two))
                {
                    Advance();
                    symbol = two;
                }
            }

            tokens.Add(new TerminalToken(TerminalTokenType.Symbol, symbol, start));
        }

        tokens.Add(new TerminalToken(TerminalTokenType.EndOfFile, string.Empty, position));
        return tokens;
    }

    public static bool IsKeyword(string text)
    {
        return Keywords.Contains(text);
    }

    private void SkipWhitespaceAndComments()
    {
        while (!IsAtEnd())
        {
            char current = Peek();
            if (char.IsWhiteSpace(current))
            {
                position++;
                continue;
            }

            if (current == '/' && PeekNext() == '/')
            {
                position += 2;
                while (!IsAtEnd() && Peek() != '\n' && Peek() != '\r')
                {
                    position++;
                }
                continue;
            }

            if (current == '/' && PeekNext() == '*')
            {
                position += 2;
                while (!IsAtEnd())
                {
                    if (Peek() == '*' && PeekNext() == '/')
                    {
                        position += 2;
                        break;
                    }
                    position++;
                }
                continue;
            }

            break;
        }
    }

    private bool IsAtEnd()
    {
        return position >= source.Length;
    }

    private char Peek()
    {
        return position < source.Length ? source[position] : '\0';
    }

    private char PeekNext()
    {
        return position + 1 < source.Length ? source[position + 1] : '\0';
    }

    private char Advance()
    {
        return source[position++];
    }

    private static bool IsIdentifierStart(char value)
    {
        return char.IsLetter(value) || value == '_';
    }

    private static bool IsIdentifierPart(char value)
    {
        return char.IsLetterOrDigit(value) || value == '_';
    }

    private static bool IsMultiCharSymbol(string symbol)
    {
        switch (symbol)
        {
            case "==":
            case "!=":
            case "<=":
            case ">=":
            case "&&":
            case "||":
            case "++":
            case "--":
            case "+=":
            case "-=":
            case "*=":
            case "/=":
            case "%=":
                return true;
            default:
                return false;
        }
    }
}

public abstract class TerminalAstNode
{
}

public abstract class TerminalStatementNode : TerminalAstNode
{
}

public abstract class TerminalExpressionNode : TerminalAstNode
{
}

public sealed class TerminalCompilationUnitNode : TerminalAstNode
{
    public readonly List<TerminalMethodNode> Methods = new List<TerminalMethodNode>();
    public readonly List<TerminalStatementNode> TopLevelStatements = new List<TerminalStatementNode>();
}

public sealed class TerminalMethodNode : TerminalAstNode
{
    public string ReturnType;
    public string Name;
    public readonly List<TerminalParameterNode> Parameters = new List<TerminalParameterNode>();
    public TerminalBlockStatementNode Body;
}

public sealed class TerminalParameterNode : TerminalAstNode
{
    public string TypeName;
    public string Name;
}

public sealed class TerminalBlockStatementNode : TerminalStatementNode
{
    public readonly List<TerminalStatementNode> Statements = new List<TerminalStatementNode>();
}

public sealed class TerminalVariableDeclarationNode : TerminalStatementNode
{
    public string TypeName;
    public string Name;
    public TerminalExpressionNode Initializer;
}

public sealed class TerminalAssignmentNode : TerminalStatementNode
{
    public TerminalExpressionNode Target;
    public TerminalExpressionNode Value;
}

public sealed class TerminalExpressionStatementNode : TerminalStatementNode
{
    public TerminalExpressionNode Expression;
}

public sealed class TerminalIfStatementNode : TerminalStatementNode
{
    public TerminalExpressionNode Condition;
    public TerminalBlockStatementNode ThenBlock;
    public TerminalBlockStatementNode ElseBlock;
}

public sealed class TerminalForStatementNode : TerminalStatementNode
{
    public TerminalStatementNode Initializer;
    public TerminalExpressionNode Condition;
    public TerminalExpressionNode Update;
    public TerminalBlockStatementNode Body;
}

public sealed class TerminalWhileStatementNode : TerminalStatementNode
{
    public TerminalExpressionNode Condition;
    public TerminalBlockStatementNode Body;
}

public sealed class TerminalSwitchStatementNode : TerminalStatementNode
{
    public TerminalExpressionNode Expression;
    public readonly List<TerminalSwitchSectionNode> Sections = new List<TerminalSwitchSectionNode>();
}

public sealed class TerminalSwitchSectionNode : TerminalAstNode
{
    public TerminalExpressionNode Label;
    public bool IsDefault;
    public readonly List<TerminalStatementNode> Statements = new List<TerminalStatementNode>();
}

public sealed class TerminalReturnStatementNode : TerminalStatementNode
{
    public TerminalExpressionNode Value;
}

public sealed class TerminalBreakStatementNode : TerminalStatementNode
{
}

public sealed class TerminalContinueStatementNode : TerminalStatementNode
{
}

public sealed class TerminalLiteralExpressionNode : TerminalExpressionNode
{
    public object Value;
    /// <summary>Original source text of the literal token (e.g. "15.0", "500", "true").</summary>
    public string RawText;
}

public sealed class TerminalIdentifierExpressionNode : TerminalExpressionNode
{
    public string Name;
}

public sealed class TerminalBinaryExpressionNode : TerminalExpressionNode
{
    public string OperatorText;
    public TerminalExpressionNode Left;
    public TerminalExpressionNode Right;
}

public sealed class TerminalUnaryExpressionNode : TerminalExpressionNode
{
    public string OperatorText;
    public TerminalExpressionNode Operand;
}

public sealed class TerminalMemberExpressionNode : TerminalExpressionNode
{
    public TerminalExpressionNode Target;
    public string MemberName;
}

public sealed class TerminalCallExpressionNode : TerminalExpressionNode
{
    public TerminalExpressionNode Callee;
    public readonly List<TerminalExpressionNode> Arguments = new List<TerminalExpressionNode>();
}

public sealed class TerminalIndexExpressionNode : TerminalExpressionNode
{
    public TerminalExpressionNode Target;
    public TerminalExpressionNode Index;
}

public sealed class TerminalGroupExpressionNode : TerminalExpressionNode
{
    public TerminalExpressionNode Inner;
}

public sealed class TerminalArrayInitializerExpressionNode : TerminalExpressionNode
{
    public readonly List<TerminalExpressionNode> Elements = new List<TerminalExpressionNode>();
}

public sealed class TerminalNewExpressionNode : TerminalExpressionNode
{
    public string TypeName;
    public readonly List<TerminalExpressionNode> Arguments = new List<TerminalExpressionNode>();
}

public sealed class TerminalCastExpressionNode : TerminalExpressionNode
{
    public string TargetType;
    public TerminalExpressionNode Operand;
}

public sealed class TerminalParser
{
    private readonly List<TerminalToken> tokens;
    private int position;

    private static readonly HashSet<string> ModifierTokens = new HashSet<string>(StringComparer.Ordinal)
    {
        "public", "private", "protected", "internal", "static", "final", "abstract", "async", "virtual", "override"
    };

    private static readonly HashSet<string> TypeTokens = new HashSet<string>(StringComparer.Ordinal)
    {
        "void", "bool", "boolean", "byte", "short", "int", "long", "float", "double", "char", "string",
        "String", "var", "Scanner", "StringBuilder", "Object"
    };

    public TerminalParser(List<TerminalToken> tokens)
    {
        this.tokens = tokens ?? new List<TerminalToken>();
    }

    public TerminalCompilationUnitNode ParseCompilationUnit()
    {
        TerminalCompilationUnitNode unit = new TerminalCompilationUnitNode();

        while (!IsAtEnd())
        {
            if (MatchIdentifier("package") || MatchIdentifier("import"))
            {
                SkipUntilSymbol(";");
                continue;
            }

            if (MatchIdentifier("class"))
            {
                ParseClassBody(unit);
                continue;
            }

            Advance();
        }

        return unit;
    }

    private void ParseClassBody(TerminalCompilationUnitNode unit)
    {
        while (!IsAtEnd() && !CheckSymbol("{"))
        {
            Advance();
        }

        if (!MatchSymbol("{"))
        {
            return;
        }

        while (!IsAtEnd() && !CheckSymbol("}"))
        {
            int before = position;
            TerminalMethodNode method;
            if (TryParseMethodDeclaration(out method))
            {
                unit.Methods.Add(method);
                continue;
            }

            position = before;
            SkipClassMember();
        }

        MatchSymbol("}");
    }

    private void SkipClassMember()
    {
        int braceDepth = 0;
        while (!IsAtEnd())
        {
            if (CheckSymbol("{"))
            {
                braceDepth++;
            }
            else if (CheckSymbol("}"))
            {
                if (braceDepth == 0)
                {
                    return;
                }
                braceDepth--;
            }
            else if (CheckSymbol(";") && braceDepth == 0)
            {
                Advance();
                return;
            }

            Advance();
        }
    }

    private bool TryParseMethodDeclaration(out TerminalMethodNode method)
    {
        method = null;
        int start = position;

        while (MatchAnyIdentifier(ModifierTokens))
        {
        }

        string returnType = ParseTypeName();
        if (string.IsNullOrWhiteSpace(returnType))
        {
            position = start;
            return false;
        }

        if (!CheckIdentifier())
        {
            position = start;
            return false;
        }

        string methodName = Advance().Text;
        if (!CheckSymbol("("))
        {
            position = start;
            return false;
        }

        Advance();
        List<TerminalParameterNode> parameters = ParseParameters();
        if (!MatchSymbol(")"))
        {
            position = start;
            return false;
        }

        if (!CheckSymbol("{"))
        {
            position = start;
            return false;
        }

        TerminalBlockStatementNode body = ParseBlock();
        method = new TerminalMethodNode
        {
            ReturnType = returnType,
            Name = methodName,
            Body = body
        };
        method.Parameters.AddRange(parameters);
        return true;
    }

    private List<TerminalParameterNode> ParseParameters()
    {
        List<TerminalParameterNode> parameters = new List<TerminalParameterNode>();

        while (!IsAtEnd() && !CheckSymbol(")"))
        {
            if (CheckSymbol(","))
            {
                Advance();
                continue;
            }

            string typeName = ParseTypeName();
            if (string.IsNullOrWhiteSpace(typeName))
            {
                SkipUntilParameterBoundary();
                continue;
            }

            if (!CheckIdentifier())
            {
                SkipUntilParameterBoundary();
                continue;
            }

            string name = Advance().Text;
            parameters.Add(new TerminalParameterNode
            {
                TypeName = typeName,
                Name = name
            });

            SkipParameterDefaultValue();
        }

        return parameters;
    }

    private void SkipUntilParameterBoundary()
    {
        while (!IsAtEnd() && !CheckSymbol(",") && !CheckSymbol(")"))
        {
            Advance();
        }
    }

    private void SkipParameterDefaultValue()
    {
        if (!CheckSymbol("="))
        {
            return;
        }

        Advance();
        int depth = 0;
        while (!IsAtEnd())
        {
            if (CheckSymbol(",") && depth == 0)
            {
                return;
            }

            if (CheckSymbol(")") && depth == 0)
            {
                return;
            }

            if (CheckSymbol("(") || CheckSymbol("[") || CheckSymbol("{"))
            {
                depth++;
            }
            else if (CheckSymbol(")") || CheckSymbol("]") || CheckSymbol("}"))
            {
                depth--;
            }

            Advance();
        }
    }

    private TerminalBlockStatementNode ParseBlock()
    {
        TerminalBlockStatementNode block = new TerminalBlockStatementNode();
        MatchSymbol("{");

        while (!IsAtEnd() && !CheckSymbol("}"))
        {
            TerminalStatementNode statement = ParseStatement();
            if (statement != null)
            {
                block.Statements.Add(statement);
                continue;
            }

            Advance();
        }

        MatchSymbol("}");
        return block;
    }

    private TerminalStatementNode ParseStatement()
    {
        if (MatchIdentifier("{") || CheckSymbol("{"))
        {
            return ParseBlock();
        }

        if (MatchIdentifier("if"))
        {
            return ParseIfStatement();
        }

        if (MatchIdentifier("for"))
        {
            return ParseForStatement();
        }

        if (MatchIdentifier("while"))
        {
            return ParseWhileStatement();
        }

        if (MatchIdentifier("switch"))
        {
            return ParseSwitchStatement();
        }

        if (MatchIdentifier("return"))
        {
            TerminalExpressionNode value = null;
            if (!CheckSymbol(";"))
            {
                value = ParseExpression();
            }
            MatchSymbol(";");
            return new TerminalReturnStatementNode { Value = value };
        }

        if (MatchIdentifier("break"))
        {
            MatchSymbol(";");
            return new TerminalBreakStatementNode();
        }

        if (MatchIdentifier("continue"))
        {
            MatchSymbol(";");
            return new TerminalContinueStatementNode();
        }

        if (IsTypeStart())
        {
            TerminalStatementNode declaration = TryParseVariableDeclaration();
            if (declaration != null)
            {
                return declaration;
            }
        }

        TerminalExpressionNode expression = ParseExpression();
        if (expression == null)
        {
            return null;
        }

        if (MatchSymbol("="))
        {
            TerminalExpressionNode value = ParseExpression();
            MatchSymbol(";");
            return new TerminalAssignmentNode
            {
                Target = expression,
                Value = value
            };
        }

        MatchSymbol(";");
        return new TerminalExpressionStatementNode
        {
            Expression = expression
        };
    }

    private TerminalStatementNode TryParseVariableDeclaration()
    {
        int start = position;
        string typeName = ParseTypeName();
        if (string.IsNullOrWhiteSpace(typeName))
        {
            position = start;
            return null;
        }

        if (!CheckIdentifier())
        {
            position = start;
            return null;
        }

        string name = Advance().Text;
        TerminalExpressionNode initializer = null;

        if (MatchSymbol("="))
        {
            if (CheckSymbol("{"))
            {
                initializer = ParseArrayInitializer();
            }
            else
            {
                initializer = ParseExpression();
            }
        }

        MatchSymbol(";");
        return new TerminalVariableDeclarationNode
        {
            TypeName = typeName,
            Name = name,
            Initializer = initializer
        };
    }

    private TerminalStatementNode ParseIfStatement()
    {
        MatchSymbol("(");
        TerminalExpressionNode condition = ParseExpression();
        MatchSymbol(")");

        TerminalBlockStatementNode thenBlock = ParseStatementBlock();
        TerminalBlockStatementNode elseBlock = null;

        if (MatchIdentifier("else"))
        {
            if (MatchIdentifier("if"))
            {
                elseBlock = new TerminalBlockStatementNode();
                elseBlock.Statements.Add(ParseIfStatement());
            }
            else
            {
                elseBlock = ParseStatementBlock();
            }
        }

        return new TerminalIfStatementNode
        {
            Condition = condition,
            ThenBlock = thenBlock,
            ElseBlock = elseBlock
        };
    }

    private TerminalBlockStatementNode ParseStatementBlock()
    {
        if (CheckSymbol("{"))
        {
            return ParseBlock();
        }

        TerminalStatementNode statement = ParseStatement();
        TerminalBlockStatementNode block = new TerminalBlockStatementNode();
        if (statement != null)
        {
            block.Statements.Add(statement);
        }
        return block;
    }

    private TerminalStatementNode ParseWhileStatement()
    {
        MatchSymbol("(");
        TerminalExpressionNode condition = ParseExpression();
        MatchSymbol(")");
        TerminalBlockStatementNode body = ParseStatementBlock();
        return new TerminalWhileStatementNode
        {
            Condition = condition,
            Body = body
        };
    }

    private TerminalStatementNode ParseForStatement()
    {
        MatchSymbol("(");

        TerminalStatementNode initializer = null;
        if (!CheckSymbol(";"))
        {
            if (IsTypeStart())
            {
                initializer = TryParseVariableDeclaration();
            }
            else
            {
                TerminalExpressionNode initExpr = ParseExpression();
                if (MatchSymbol("="))
                {
                    TerminalExpressionNode initValue = ParseExpression();
                    initializer = new TerminalAssignmentNode { Target = initExpr, Value = initValue };
                }
                else if (initExpr != null)
                {
                    initializer = new TerminalExpressionStatementNode { Expression = initExpr };
                }
            }
        }
        MatchSymbol(";");

        TerminalExpressionNode condition = null;
        if (!CheckSymbol(";"))
        {
            condition = ParseExpression();
        }
        MatchSymbol(";");

        TerminalExpressionNode update = null;
        if (!CheckSymbol(")"))
        {
            update = ParseExpression();
        }
        MatchSymbol(")");

        TerminalBlockStatementNode body = ParseStatementBlock();
        return new TerminalForStatementNode
        {
            Initializer = initializer,
            Condition = condition,
            Update = update,
            Body = body
        };
    }

    private TerminalStatementNode ParseSwitchStatement()
    {
        MatchSymbol("(");
        TerminalExpressionNode expression = ParseExpression();
        MatchSymbol(")");

        TerminalSwitchStatementNode switchStatement = new TerminalSwitchStatementNode
        {
            Expression = expression
        };

        TerminalBlockStatementNode body = ParseStatementBlock();
        TerminalSwitchSectionNode currentSection = null;

        foreach (TerminalStatementNode item in body.Statements)
        {
            TerminalExpressionStatementNode expressionStatement = item as TerminalExpressionStatementNode;
            if (expressionStatement != null)
            {
                currentSection = null;
                continue;
            }
        }

        // Re-parse the block at token level to preserve case sections.
        // The parser keeps the statement AST simple, while the analyzer uses
        // the block structure for node-count validation.
        switchStatement.Sections.AddRange(ParseSwitchSections(body));
        return switchStatement;
    }

    private List<TerminalSwitchSectionNode> ParseSwitchSections(TerminalBlockStatementNode body)
    {
        List<TerminalSwitchSectionNode> sections = new List<TerminalSwitchSectionNode>();
        if (body == null)
        {
            return sections;
        }

        TerminalSwitchSectionNode defaultSection = null;
        for (int i = 0; i < body.Statements.Count; i++)
        {
            TerminalStatementNode statement = body.Statements[i];
            TerminalExpressionStatementNode expressionStatement = statement as TerminalExpressionStatementNode;
            if (expressionStatement != null && expressionStatement.Expression is TerminalIdentifierExpressionNode)
            {
                TerminalIdentifierExpressionNode identifier = (TerminalIdentifierExpressionNode)expressionStatement.Expression;
                if (string.Equals(identifier.Name, "default", StringComparison.OrdinalIgnoreCase))
                {
                    defaultSection = new TerminalSwitchSectionNode { IsDefault = true };
                    sections.Add(defaultSection);
                }
            }
        }

        return sections;
    }

    private TerminalExpressionNode ParseArrayInitializer()
    {
        TerminalArrayInitializerExpressionNode array = new TerminalArrayInitializerExpressionNode();
        MatchSymbol("{");
        while (!IsAtEnd() && !CheckSymbol("}"))
        {
            if (CheckSymbol(","))
            {
                Advance();
                continue;
            }

            TerminalExpressionNode element = ParseExpression();
            if (element != null)
            {
                array.Elements.Add(element);
            }
            else
            {
                Advance();
            }
        }
        MatchSymbol("}");
        return array;
    }

    private string ParseTypeName()
    {
        int start = position;
        List<string> parts = new List<string>();

        while (!IsAtEnd())
        {
            if (CheckSymbol("["))
            {
                parts.Add(Advance().Text);
                if (CheckSymbol("]"))
                {
                    parts.Add(Advance().Text);
                }
                continue;
            }

            if (CheckSymbol("<"))
            {
                int depth = 0;
                do
                {
                    if (CheckSymbol("<")) depth++;
                    if (CheckSymbol(">")) depth--;
                    parts.Add(Advance().Text);
                }
                while (!IsAtEnd() && depth > 0);
                continue;
            }

            if (CheckIdentifier())
            {
                string text = Peek().Text;
                if (!IsTypeToken(text) && !string.Equals(text, "final", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }
                parts.Add(Advance().Text);
                continue;
            }

            break;
        }

        if (parts.Count == 0)
        {
            position = start;
            return string.Empty;
        }

        return string.Join(" ", parts.ToArray());
    }

    private bool IsTypeStart()
    {
        if (CheckIdentifier())
        {
            string text = Peek().Text;
            return IsTypeToken(text);
        }

        return false;
    }

    private bool IsTypeToken(string text)
    {
        return TypeTokens.Contains(text) || TerminalLexer.IsKeyword(text) && !string.Equals(text, "if", StringComparison.OrdinalIgnoreCase) && !string.Equals(text, "for", StringComparison.OrdinalIgnoreCase) && !string.Equals(text, "while", StringComparison.OrdinalIgnoreCase) && !string.Equals(text, "switch", StringComparison.OrdinalIgnoreCase) && !string.Equals(text, "return", StringComparison.OrdinalIgnoreCase) && !string.Equals(text, "class", StringComparison.OrdinalIgnoreCase);
    }

    private TerminalExpressionNode ParseExpression()
    {
        return ParseBinaryExpression(0);
    }

    private TerminalExpressionNode ParseBinaryExpression(int parentPrecedence)
    {
        TerminalExpressionNode left = ParseUnaryExpression();

        while (true)
        {
            string op = PeekBinaryOperator();
            if (string.IsNullOrEmpty(op))
            {
                break;
            }

            int precedence = GetPrecedence(op);
            if (precedence < parentPrecedence)
            {
                break;
            }

            Advance();
            if (op == "&&" || op == "||")
            {
                // already consumed by Advance only if single symbol; multi-char
            }

            TerminalExpressionNode right = ParseBinaryExpression(precedence + (IsRightAssociative(op) ? 0 : 1));
            left = new TerminalBinaryExpressionNode
            {
                Left = left,
                Right = right,
                OperatorText = op
            };
        }

        return left;
    }

    private TerminalExpressionNode ParseUnaryExpression()
    {
        if (CheckSymbol("!") || CheckSymbol("-") || CheckSymbol("+"))
        {
            string op = Advance().Text;
            TerminalExpressionNode operand = ParseUnaryExpression();
            return new TerminalUnaryExpressionNode
            {
                OperatorText = op,
                Operand = operand
            };
        }

        if (CheckSymbol("("))
        {
            int start = position;
            Advance(); // consume (
            if (IsTypeStart())
            {
                string typeName = ParseTypeName();
                if (!string.IsNullOrWhiteSpace(typeName) && MatchSymbol(")"))
                {
                    TerminalExpressionNode operand = ParseUnaryExpression();
                    if (operand != null)
                    {
                        return new TerminalCastExpressionNode
                        {
                            TargetType = typeName,
                            Operand = operand
                        };
                    }
                }
            }
            position = start; // Backtrack if not a valid cast
        }

        return ParsePostfixExpression();
    }

    private TerminalExpressionNode ParsePostfixExpression()
    {
        TerminalExpressionNode expression = ParsePrimaryExpression();
        while (true)
        {
            if (MatchSymbol("("))
            {
                TerminalCallExpressionNode call = new TerminalCallExpressionNode
                {
                    Callee = expression
                };
                if (!CheckSymbol(")"))
                {
                    do
                    {
                        TerminalExpressionNode argument = ParseExpression();
                        if (argument != null)
                        {
                            call.Arguments.Add(argument);
                        }
                    }
                    while (MatchSymbol(","));
                }
                MatchSymbol(")");
                expression = call;
                continue;
            }

            if (MatchSymbol("."))
            {
                if (!CheckIdentifier())
                {
                    break;
                }

                expression = new TerminalMemberExpressionNode
                {
                    Target = expression,
                    MemberName = Advance().Text
                };
                continue;
            }

            if (MatchSymbol("["))
            {
                TerminalExpressionNode index = ParseExpression();
                MatchSymbol("]");
                expression = new TerminalIndexExpressionNode
                {
                    Target = expression,
                    Index = index
                };
                continue;
            }

            break;
        }

        return expression;
    }

    private TerminalExpressionNode ParsePrimaryExpression()
    {
        if (IsAtEnd())
        {
            return null;
        }

        if (MatchSymbol("("))
        {
            TerminalExpressionNode inner = ParseExpression();
            MatchSymbol(")");
            return new TerminalGroupExpressionNode { Inner = inner };
        }

        if (MatchSymbol("{"))
        {
            position--;
            return ParseArrayInitializer();
        }

        TerminalToken token = Advance();
        switch (token.Type)
        {
            case TerminalTokenType.String:
                return new TerminalLiteralExpressionNode
                {
                    Value = UnescapeString(token.Text),
                    RawText = UnescapeString(token.Text)
                };
            case TerminalTokenType.Char:
                return new TerminalLiteralExpressionNode
                {
                    Value = UnescapeChar(token.Text),
                    RawText = token.Text.Length >= 2 ? token.Text.Substring(1, token.Text.Length - 2) : token.Text
                };
            case TerminalTokenType.Number:
                // Store raw text so "15.0" stays "15.0" — not "15" after double parsing
                return new TerminalLiteralExpressionNode
                {
                    Value = ParseNumber(token.Text),
                    RawText = token.Text
                };
            case TerminalTokenType.Identifier:
                if (string.Equals(token.Text, "true", StringComparison.OrdinalIgnoreCase))
                {
                    return new TerminalLiteralExpressionNode { Value = true, RawText = "true" };
                }
                if (string.Equals(token.Text, "false", StringComparison.OrdinalIgnoreCase))
                {
                    return new TerminalLiteralExpressionNode { Value = false, RawText = "false" };
                }
                if (string.Equals(token.Text, "null", StringComparison.OrdinalIgnoreCase))
                {
                    return new TerminalLiteralExpressionNode { Value = null, RawText = "null" };
                }
                if (string.Equals(token.Text, "new", StringComparison.OrdinalIgnoreCase))
                {
                    return ParseNewExpression();
                }
                return new TerminalIdentifierExpressionNode
                {
                    Name = token.Text
                };
            default:
                return null;
        }
    }

    private TerminalExpressionNode ParseNewExpression()
    {
        string typeName = ParseTypeName();
        TerminalNewExpressionNode creation = new TerminalNewExpressionNode
        {
            TypeName = typeName
        };
        if (MatchSymbol("("))
        {
            if (!CheckSymbol(")"))
            {
                do
                {
                    TerminalExpressionNode argument = ParseExpression();
                    if (argument != null)
                    {
                        creation.Arguments.Add(argument);
                    }
                }
                while (MatchSymbol(","));
            }
            MatchSymbol(")");
        }

        if (CheckSymbol("{"))
        {
            TerminalArrayInitializerExpressionNode arrayInitializer = (TerminalArrayInitializerExpressionNode)ParseArrayInitializer();
            return arrayInitializer;
        }

        return creation;
    }

    private string PeekBinaryOperator()
    {
        if (IsAtEnd())
        {
            return string.Empty;
        }

        string text = Peek().Text;
        switch (text)
        {
            case "||":
            case "&&":
            case "==":
            case "!=":
            case "<":
            case "<=":
            case ">":
            case ">=":
            case "+":
            case "-":
            case "*":
            case "/":
            case "%":
                return text;
            default:
                return string.Empty;
        }
    }

    private int GetPrecedence(string op)
    {
        switch (op)
        {
            case "||": return 1;
            case "&&": return 2;
            case "==":
            case "!=": return 3;
            case "<":
            case "<=":
            case ">":
            case ">=": return 4;
            case "+":
            case "-": return 5;
            case "*":
            case "/":
            case "%": return 6;
            default: return 0;
        }
    }

    private bool IsRightAssociative(string op)
    {
        return false;
    }

    private object ParseNumber(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0L;

        string cleanText = text.Trim();
        bool isFloat = cleanText.EndsWith("f", StringComparison.OrdinalIgnoreCase);
        bool isLong = cleanText.EndsWith("L", StringComparison.Ordinal);

        if (isFloat || isLong)
        {
            cleanText = cleanText.Substring(0, cleanText.Length - 1);
        }

        if (isFloat || cleanText.IndexOf('.') >= 0)
        {
            float parsedFloat;
            if (isFloat && float.TryParse(cleanText, NumberStyles.Float, CultureInfo.InvariantCulture, out parsedFloat))
            {
                return parsedFloat;
            }

            double parsedDouble;
            if (double.TryParse(cleanText, NumberStyles.Float, CultureInfo.InvariantCulture, out parsedDouble))
            {
                return isFloat ? (float)parsedDouble : parsedDouble;
            }
        }

        long parsedLong;
        if (long.TryParse(cleanText, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsedLong))
        {
            return parsedLong;
        }

        double fallback;
        if (double.TryParse(cleanText, NumberStyles.Float, CultureInfo.InvariantCulture, out fallback))
        {
            return fallback;
        }

        return 0L;
    }

    private string UnescapeString(string text)
    {
        if (string.IsNullOrEmpty(text) || text.Length < 2)
        {
            return string.Empty;
        }

        string body = text.Substring(1, text.Length - 2);
        return body.Replace("\\\"", "\"").Replace("\\n", "\n").Replace("\\r", "\r").Replace("\\t", "\t").Replace("\\\\", "\\");
    }

    private object UnescapeChar(string text)
    {
        if (string.IsNullOrEmpty(text) || text.Length < 2)
        {
            return '\0';
        }

        string body = text.Substring(1, text.Length - 2);
        string unescaped = body.Replace("\\\"", "\"").Replace("\\n", "\n").Replace("\\r", "\r").Replace("\\t", "\t").Replace("\\'", "'").Replace("\\\\", "\\");

        if (unescaped.Length > 0)
        {
            return unescaped[0];
        }

        return '\0';
    }

    private bool IsAtEnd()
    {
        return position >= tokens.Count || tokens[position].Type == TerminalTokenType.EndOfFile;
    }

    private TerminalToken Peek()
    {
        return position < tokens.Count ? tokens[position] : tokens[tokens.Count - 1];
    }

    private TerminalToken Previous()
    {
        return position > 0 ? tokens[position - 1] : tokens[0];
    }

    private TerminalToken Advance()
    {
        if (!IsAtEnd())
        {
            position++;
        }

        return Previous();
    }

    private bool CheckSymbol(string symbol)
    {
        return !IsAtEnd() && Peek().Type == TerminalTokenType.Symbol && string.Equals(Peek().Text, symbol, StringComparison.Ordinal);
    }

    private bool MatchSymbol(string symbol)
    {
        if (!CheckSymbol(symbol))
        {
            return false;
        }

        Advance();
        return true;
    }

    private bool CheckIdentifier()
    {
        return !IsAtEnd() && Peek().Type == TerminalTokenType.Identifier;
    }

    private bool MatchIdentifier(string identifier)
    {
        if (!CheckIdentifier())
        {
            return false;
        }

        if (!string.Equals(Peek().Text, identifier, StringComparison.Ordinal))
        {
            return false;
        }

        Advance();
        return true;
    }

    private bool MatchAnyIdentifier(HashSet<string> options)
    {
        if (!CheckIdentifier())
        {
            return false;
        }

        if (!options.Contains(Peek().Text))
        {
            return false;
        }

        Advance();
        return true;
    }

    private void SkipUntilSymbol(string symbol)
    {
        while (!IsAtEnd() && !CheckSymbol(symbol))
        {
            Advance();
        }
        MatchSymbol(symbol);
    }
}

public sealed class TerminalInterpreter
{
    private sealed class TerminalScannerRuntime
    {
        private readonly string rawInput;
        private int position = 0;

        public TerminalScannerRuntime(string input)
        {
            rawInput = input ?? string.Empty;
        }

        public string ReadLine()
        {
            if (position >= rawInput.Length) return string.Empty;

            StringBuilder sb = new StringBuilder();
            while (position < rawInput.Length)
            {
                char c = rawInput[position];
                // Check all possible line-break variations including Unity's soft break
                if (c == '\n' || c == '\r' || c == '\v' || c == '\u2028' || c == '\u2029')
                {
                    position++;
                    // Handle Windows \r\n pair
                    if (c == '\r' && position < rawInput.Length && rawInput[position] == '\n')
                    {
                        position++;
                    }
                    break;
                }
                sb.Append(c);
                position++;
            }
            return sb.ToString();
        }

        public string ReadToken()
        {
            // Skip leading whitespace
            while (position < rawInput.Length && char.IsWhiteSpace(rawInput[position]))
            {
                position++;
            }

            if (position >= rawInput.Length) return string.Empty;

            int start = position;
            while (position < rawInput.Length && !char.IsWhiteSpace(rawInput[position]))
            {
                position++;
            }

            return rawInput.Substring(start, position - start);
        }
    }

    private sealed class ExecutionState
    {
        public readonly Dictionary<string, object> Variables = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, TerminalMethodNode> Methods = new Dictionary<string, TerminalMethodNode>(StringComparer.OrdinalIgnoreCase);
        public readonly StringBuilder Output = new StringBuilder();
        public readonly string RawInput;
        public readonly TerminalScannerRuntime Scanner;
        public bool ReturnTriggered;
        public object ReturnValue;

        public ExecutionState(string rawInput)
        {
            RawInput = rawInput ?? string.Empty;
            Scanner = new TerminalScannerRuntime(RawInput);
        }
    }

    public string Execute(TerminalCompilationUnitNode unit, string code, string injectedInput)
    {
        if (unit == null)
        {
            return string.Empty;
        }

        ExecutionState state = new ExecutionState(injectedInput);

        for (int i = 0; i < unit.Methods.Count; i++)
        {
            TerminalMethodNode method = unit.Methods[i];
            if (!state.Methods.ContainsKey(method.Name))
            {
                state.Methods.Add(method.Name, method);
            }
        }

        TerminalMethodNode mainMethod = FindMainMethod(unit);
        if (mainMethod != null)
        {
            ExecuteBlock(mainMethod.Body, state);
        }
        else
        {
            ExecuteStatements(unit.TopLevelStatements, state);
        }

        return state.Output.ToString().TrimEnd();
    }

    private TerminalMethodNode FindMainMethod(TerminalCompilationUnitNode unit)
    {
        for (int i = 0; i < unit.Methods.Count; i++)
        {
            if (string.Equals(unit.Methods[i].Name, "main", StringComparison.OrdinalIgnoreCase))
            {
                return unit.Methods[i];
            }
        }

        return unit.Methods.Count > 0 ? unit.Methods[0] : null;
    }

    private void ExecuteStatements(List<TerminalStatementNode> statements, ExecutionState state)
    {
        if (statements == null)
        {
            return;
        }

        for (int i = 0; i < statements.Count; i++)
        {
            if (state.ReturnTriggered)
            {
                return;
            }

            ExecuteStatement(statements[i], state);
        }
    }

    private void ExecuteBlock(TerminalBlockStatementNode block, ExecutionState state)
    {
        if (block == null)
        {
            return;
        }

        ExecuteStatements(block.Statements, state);
    }

    private void ExecuteStatement(TerminalStatementNode statement, ExecutionState state)
    {
        if (statement == null || state.ReturnTriggered)
        {
            return;
        }

        TerminalBlockStatementNode block = statement as TerminalBlockStatementNode;
        if (block != null)
        {
            ExecuteBlock(block, state);
            return;
        }

        TerminalVariableDeclarationNode variableDeclaration = statement as TerminalVariableDeclarationNode;
        if (variableDeclaration != null)
        {
            object value = variableDeclaration.Initializer != null ? EvaluateExpression(variableDeclaration.Initializer, state) : GetDefaultValue(variableDeclaration.TypeName);
            if (state.Variables.ContainsKey(variableDeclaration.Name))
            {
                state.Variables[variableDeclaration.Name] = value;
            }
            else
            {
                state.Variables.Add(variableDeclaration.Name, value);
            }
            return;
        }

        TerminalAssignmentNode assignment = statement as TerminalAssignmentNode;
        if (assignment != null)
        {
            AssignValue(assignment.Target, assignment.Value, state);
            return;
        }

        TerminalExpressionStatementNode expressionStatement = statement as TerminalExpressionStatementNode;
        if (expressionStatement != null)
        {
            EvaluateExpression(expressionStatement.Expression, state);
            return;
        }

        TerminalIfStatementNode ifStatement = statement as TerminalIfStatementNode;
        if (ifStatement != null)
        {
            if (ToBoolean(EvaluateExpression(ifStatement.Condition, state)))
            {
                ExecuteBlock(ifStatement.ThenBlock, state);
            }
            else if (ifStatement.ElseBlock != null)
            {
                ExecuteBlock(ifStatement.ElseBlock, state);
            }
            return;
        }

        TerminalWhileStatementNode whileStatement = statement as TerminalWhileStatementNode;
        if (whileStatement != null)
        {
            int guard = 0;
            while (ToBoolean(EvaluateExpression(whileStatement.Condition, state)))
            {
                ExecuteBlock(whileStatement.Body, state);
                guard++;
                if (guard > 2000 || state.ReturnTriggered)
                {
                    break;
                }
            }
            return;
        }

        TerminalForStatementNode forStatement = statement as TerminalForStatementNode;
        if (forStatement != null)
        {
            if (forStatement.Initializer != null)
            {
                ExecuteStatement(forStatement.Initializer, state);
            }

            int guard = 0;
            while (forStatement.Condition == null || ToBoolean(EvaluateExpression(forStatement.Condition, state)))
            {
                ExecuteBlock(forStatement.Body, state);
                if (state.ReturnTriggered)
                {
                    return;
                }

                if (forStatement.Update != null)
                {
                    EvaluateExpression(forStatement.Update, state);
                }

                guard++;
                if (guard > 4000)
                {
                    break;
                }
            }
            return;
        }

        TerminalSwitchStatementNode switchStatement = statement as TerminalSwitchStatementNode;
        if (switchStatement != null)
        {
            object switchValue = EvaluateExpression(switchStatement.Expression, state);
            bool executed = false;
            for (int i = 0; i < switchStatement.Sections.Count; i++)
            {
                TerminalSwitchSectionNode section = switchStatement.Sections[i];
                if (section.IsDefault || AreEquivalent(switchValue, EvaluateExpression(section.Label, state)))
                {
                    ExecuteStatements(section.Statements, state);
                    executed = true;
                    break;
                }
            }

            if (!executed)
            {
                for (int i = 0; i < switchStatement.Sections.Count; i++)
                {
                    if (switchStatement.Sections[i].IsDefault)
                    {
                        ExecuteStatements(switchStatement.Sections[i].Statements, state);
                        break;
                    }
                }
            }
            return;
        }

        TerminalReturnStatementNode returnStatement = statement as TerminalReturnStatementNode;
        if (returnStatement != null)
        {
            state.ReturnValue = returnStatement.Value != null ? EvaluateExpression(returnStatement.Value, state) : null;
            state.ReturnTriggered = true;
            return;
        }
    }

    private object EvaluateExpression(TerminalExpressionNode expression, ExecutionState state)
    {
        if (expression == null)
        {
            return null;
        }

        TerminalGroupExpressionNode group = expression as TerminalGroupExpressionNode;
        if (group != null)
        {
            return EvaluateExpression(group.Inner, state);
        }

        TerminalLiteralExpressionNode literal = expression as TerminalLiteralExpressionNode;
        if (literal != null)
        {
            return literal.Value;
        }

        TerminalIdentifierExpressionNode identifier = expression as TerminalIdentifierExpressionNode;
        if (identifier != null)
        {
            object value;
            if (state.Variables.TryGetValue(identifier.Name, out value))
            {
                return value;
            }

            // Exclude common Java built-ins from throwing undeclared variable errors.
            if (string.Equals(identifier.Name, "System", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(identifier.Name, "out", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(identifier.Name, "Scanner", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(identifier.Name, "String", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(identifier.Name, "Math", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(identifier.Name, "in", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            throw new Exception(string.Format("Variable '{0}' is used but was never declared or is out of scope.", identifier.Name));
        }

        TerminalUnaryExpressionNode unary = expression as TerminalUnaryExpressionNode;
        if (unary != null)
        {
            object operand = EvaluateExpression(unary.Operand, state);
            switch (unary.OperatorText)
            {
                case "-":
                    if (IsDoubleLike(operand)) return -ToDouble(operand);
                    return -ToLong(operand);
                case "+":
                    return operand;
                case "!":
                    return !ToBoolean(operand);
            }
        }

        TerminalBinaryExpressionNode binary = expression as TerminalBinaryExpressionNode;
        if (binary != null)
        {
            object left = EvaluateExpression(binary.Left, state);
            object right = EvaluateExpression(binary.Right, state);
            return ApplyBinaryOperator(left, right, binary.OperatorText);
        }

        TerminalMemberExpressionNode member = expression as TerminalMemberExpressionNode;
        if (member != null)
        {
            object target = EvaluateExpression(member.Target, state);
            return ResolveMember(target, member.MemberName, state);
        }

        TerminalCallExpressionNode call = expression as TerminalCallExpressionNode;
        if (call != null)
        {
            return InvokeCall(call, state);
        }

        TerminalIndexExpressionNode index = expression as TerminalIndexExpressionNode;
        if (index != null)
        {
            object target = EvaluateExpression(index.Target, state);
            object indexValue = EvaluateExpression(index.Index, state);
            return ResolveIndex(target, indexValue);
        }

        TerminalArrayInitializerExpressionNode arrayInitializer = expression as TerminalArrayInitializerExpressionNode;
        if (arrayInitializer != null)
        {
            List<object> values = new List<object>();
            for (int i = 0; i < arrayInitializer.Elements.Count; i++)
            {
                values.Add(EvaluateExpression(arrayInitializer.Elements[i], state));
            }
            return values;
        }

        TerminalCastExpressionNode cast = expression as TerminalCastExpressionNode;
        if (cast != null)
        {
            object value = EvaluateExpression(cast.Operand, state);
            return CastValue(value, cast.TargetType);
        }

        TerminalNewExpressionNode creation = expression as TerminalNewExpressionNode;
        if (creation != null)
        {
            if (string.Equals(creation.TypeName, "Scanner", StringComparison.OrdinalIgnoreCase))
            {
                return new TerminalScannerRuntime(state.RawInput);
            }

            if (string.Equals(creation.TypeName, "int[]", StringComparison.OrdinalIgnoreCase) || string.Equals(creation.TypeName, "long[]", StringComparison.OrdinalIgnoreCase) || string.Equals(creation.TypeName, "double[]", StringComparison.OrdinalIgnoreCase) || string.Equals(creation.TypeName, "float[]", StringComparison.OrdinalIgnoreCase) || string.Equals(creation.TypeName, "String[]", StringComparison.OrdinalIgnoreCase))
            {
                List<object> arrayValues = new List<object>();
                for (int i = 0; i < creation.Arguments.Count; i++)
                {
                    arrayValues.Add(EvaluateExpression(creation.Arguments[i], state));
                }
                return arrayValues;
            }
        }

        return null;
    }

    private object InvokeCall(TerminalCallExpressionNode call, ExecutionState state)
    {
        TerminalMemberExpressionNode member = call.Callee as TerminalMemberExpressionNode;
        if (member != null)
        {
            object target = EvaluateExpression(member.Target, state);
            string memberName = member.MemberName;

            if (IsConsoleOutTarget(target, member.Target))
            {
                if (string.Equals(memberName, "println", StringComparison.OrdinalIgnoreCase) || string.Equals(memberName, "print", StringComparison.OrdinalIgnoreCase))
                {
                    string text = call.Arguments.Count > 0 ? ConvertToString(EvaluateExpression(call.Arguments[0], state)) : string.Empty;
                    if (string.Equals(memberName, "println", StringComparison.OrdinalIgnoreCase))
                    {
                        state.Output.AppendLine(text);
                    }
                    else
                    {
                        state.Output.Append(text);
                    }
                    return null;
                }
            }

            TerminalScannerRuntime scanner = target as TerminalScannerRuntime;
            if (scanner != null)
            {
                if (string.Equals(memberName, "next", StringComparison.OrdinalIgnoreCase))
                {
                    return scanner.ReadToken();
                }

                if (string.Equals(memberName, "nextInt", StringComparison.OrdinalIgnoreCase) || string.Equals(memberName, "nextLong", StringComparison.OrdinalIgnoreCase))
                {
                    string token = scanner.ReadToken();
                    long parsedLong;
                    if (long.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsedLong))
                    {
                        return parsedLong;
                    }
                    double parsedDouble;
                    if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out parsedDouble))
                    {
                        return parsedDouble;
                    }
                    return 0L;
                }

                if (string.Equals(memberName, "nextDouble", StringComparison.OrdinalIgnoreCase) || string.Equals(memberName, "nextFloat", StringComparison.OrdinalIgnoreCase))
                {
                    string token = scanner.ReadToken();
                    double parsedDouble;
                    if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out parsedDouble))
                    {
                        return parsedDouble;
                    }
                    return 0d;
                }

                if (string.Equals(memberName, "nextLine", StringComparison.OrdinalIgnoreCase))
                {
                    return scanner.ReadLine();
                }
            }

            string asString = target as string;
            if (asString != null)
            {
                if (string.Equals(memberName, "length", StringComparison.OrdinalIgnoreCase))
                {
                    return asString.Length;
                }

                if (string.Equals(memberName, "charAt", StringComparison.OrdinalIgnoreCase) && call.Arguments.Count > 0)
                {
                    int index = ToInt(EvaluateExpression(call.Arguments[0], state));
                    if (index >= 0 && index < asString.Length)
                    {
                        return asString[index].ToString();
                    }
                    return string.Empty;
                }

                if (string.Equals(memberName, "substring", StringComparison.OrdinalIgnoreCase) && call.Arguments.Count > 0)
                {
                    int start = ToInt(EvaluateExpression(call.Arguments[0], state));
                    int end = call.Arguments.Count > 1 ? ToInt(EvaluateExpression(call.Arguments[1], state)) : asString.Length;
                    start = Math.Max(0, Math.Min(start, asString.Length));
                    end = Math.Max(start, Math.Min(end, asString.Length));
                    return asString.Substring(start, end - start);
                }

                if (string.Equals(memberName, "indexOf", StringComparison.OrdinalIgnoreCase) && call.Arguments.Count > 0)
                {
                    string search = ConvertToString(EvaluateExpression(call.Arguments[0], state));
                    return asString.IndexOf(search, StringComparison.Ordinal);
                }

                if (string.Equals(memberName, "equals", StringComparison.OrdinalIgnoreCase) && call.Arguments.Count > 0)
                {
                    string other = ConvertToString(EvaluateExpression(call.Arguments[0], state));
                    return string.Equals(asString, other, StringComparison.Ordinal);
                }
            }

            List<object> array = target as List<object>;
            if (array != null)
            {
                if (string.Equals(memberName, "length", StringComparison.OrdinalIgnoreCase))
                {
                    return array.Count;
                }
            }

            if (string.Equals(memberName, "parseInt", StringComparison.OrdinalIgnoreCase) && call.Arguments.Count > 0)
            {
                int parsedInt;
                if (int.TryParse(ConvertToString(EvaluateExpression(call.Arguments[0], state)), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsedInt))
                {
                    return parsedInt;
                }
                return 0;
            }

            if (string.Equals(memberName, "parseDouble", StringComparison.OrdinalIgnoreCase) && call.Arguments.Count > 0)
            {
                double parsed;
                if (double.TryParse(ConvertToString(EvaluateExpression(call.Arguments[0], state)), NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
                {
                    return parsed;
                }
                return 0d;
            }
        }

        TerminalIdentifierExpressionNode identifier = call.Callee as TerminalIdentifierExpressionNode;
        if (identifier != null)
        {
            TerminalMethodNode method;
            if (state.Methods.TryGetValue(identifier.Name, out method))
            {
                ExecutionState childState = new ExecutionState(state.RawInput);
                childState.Methods.Clear();
                foreach (KeyValuePair<string, TerminalMethodNode> pair in state.Methods)
                {
                    childState.Methods[pair.Key] = pair.Value;
                }

                for (int i = 0; i < method.Parameters.Count && i < call.Arguments.Count; i++)
                {
                    childState.Variables[method.Parameters[i].Name] = EvaluateExpression(call.Arguments[i], state);
                }

                ExecuteBlock(method.Body, childState);
                if (childState.ReturnTriggered)
                {
                    return childState.ReturnValue;
                }

                return null;
            }
        }

        return null;
    }

    private bool IsConsoleOutTarget(object target, TerminalExpressionNode targetNode)
    {
        TerminalMemberExpressionNode member = targetNode as TerminalMemberExpressionNode;
        if (member == null)
        {
            return false;
        }

        TerminalIdentifierExpressionNode identifier = member.Target as TerminalIdentifierExpressionNode;
        return identifier != null && string.Equals(identifier.Name, "System", StringComparison.OrdinalIgnoreCase) && string.Equals(member.MemberName, "out", StringComparison.OrdinalIgnoreCase);
    }

    private object ResolveMember(object target, string memberName, ExecutionState state)
    {
        List<object> array = target as List<object>;
        if (array != null && string.Equals(memberName, "length", StringComparison.OrdinalIgnoreCase))
        {
            return array.Count;
        }

        return null;
    }

    private object ResolveIndex(object target, object indexValue)
    {
        List<object> array = target as List<object>;
        if (array != null)
        {
            int index = ToInt(indexValue);
            if (index >= 0 && index < array.Count)
            {
                return array[index];
            }
            return null;
        }

        string text = target as string;
        if (text != null)
        {
            int index = ToInt(indexValue);
            if (index >= 0 && index < text.Length)
            {
                return text[index].ToString();
            }
            return string.Empty;
        }

        return null;
    }

    private void AssignValue(TerminalExpressionNode target, TerminalExpressionNode valueExpression, ExecutionState state)
    {
        object value = EvaluateExpression(valueExpression, state);

        TerminalIdentifierExpressionNode identifier = target as TerminalIdentifierExpressionNode;
        if (identifier != null)
        {
            state.Variables[identifier.Name] = value;
            return;
        }

        TerminalIndexExpressionNode index = target as TerminalIndexExpressionNode;
        if (index != null)
        {
            object arrayTarget = EvaluateExpression(index.Target, state);
            List<object> array = arrayTarget as List<object>;
            if (array != null)
            {
                int idx = ToInt(EvaluateExpression(index.Index, state));
                if (idx >= 0 && idx < array.Count)
                {
                    array[idx] = value;
                }
            }
        }
    }

    private object GetDefaultValue(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            return null;
        }

        string lowered = typeName.Trim().ToLowerInvariant();
        if (lowered.IndexOf("bool") >= 0)
        {
            return false;
        }

        if (lowered.IndexOf("double") >= 0 || lowered.IndexOf("float") >= 0)
        {
            return 0d;
        }

        if (lowered.IndexOf("char") >= 0)
        {
            return string.Empty;
        }

        if (lowered.EndsWith("[]", StringComparison.Ordinal))
        {
            return new List<object>();
        }

        if (lowered.IndexOf("int") >= 0 || lowered.IndexOf("long") >= 0 || lowered.IndexOf("short") >= 0 || lowered.IndexOf("byte") >= 0)
        {
            return 0L;
        }

        if (lowered.IndexOf("string") >= 0)
        {
            return string.Empty;
        }

        return null;
    }

    private object ApplyBinaryOperator(object left, object right, string op)
    {
        switch (op)
        {
            case "+":
                if (left is string || right is string)
                {
                    return ConvertToString(left) + ConvertToString(right);
                }

                if (IsDoubleLike(left) || IsDoubleLike(right))
                {
                    return ToDouble(left) + ToDouble(right);
                }

                return ToLong(left) + ToLong(right);
            case "-":
                if (IsDoubleLike(left) || IsDoubleLike(right)) return ToDouble(left) - ToDouble(right);
                return ToLong(left) - ToLong(right);
            case "*":
                if (IsDoubleLike(left) || IsDoubleLike(right)) return ToDouble(left) * ToDouble(right);
                return ToLong(left) * ToLong(right);
            case "/":
                if (IsDoubleLike(left) || IsDoubleLike(right)) return ToDouble(left) / ToDouble(right);
                return ToLong(left) / Math.Max(1L, ToLong(right));
            case "%":
                if (IsDoubleLike(left) || IsDoubleLike(right)) return ToDouble(left) % ToDouble(right);
                return ToLong(left) % Math.Max(1L, ToLong(right));
            case "<": return Compare(left, right) < 0;
            case "<=": return Compare(left, right) <= 0;
            case ">": return Compare(left, right) > 0;
            case ">=": return Compare(left, right) >= 0;
            case "==": return AreEquivalent(left, right);
            case "!=": return !AreEquivalent(left, right);
            case "&&": return ToBoolean(left) && ToBoolean(right);
            case "||": return ToBoolean(left) || ToBoolean(right);
            default:
                return null;
        }
    }

    private int Compare(object left, object right)
    {
        if (left == null && right == null) return 0;
        if (left == null) return -1;
        if (right == null) return 1;

        if (IsNumericLike(left) || IsNumericLike(right))
        {
            if (IsDoubleLike(left) || IsDoubleLike(right))
            {
                return ToDouble(left).CompareTo(ToDouble(right));
            }

            return ToLong(left).CompareTo(ToLong(right));
        }

        string leftString = ConvertToString(left);
        string rightString = ConvertToString(right);
        return string.Compare(leftString, rightString, StringComparison.Ordinal);
    }

    private bool AreEquivalent(object left, object right)
    {
        if (left == null || right == null)
        {
            return left == null && right == null;
        }

        if (IsNumericLike(left) || IsNumericLike(right))
        {
            if (IsDoubleLike(left) || IsDoubleLike(right))
            {
                return Math.Abs(ToDouble(left) - ToDouble(right)) < 0.000001d;
            }

            return ToLong(left) == ToLong(right);
        }

        return string.Equals(ConvertToString(left), ConvertToString(right), StringComparison.Ordinal);
    }

    private bool IsNumericLike(object value)
    {
        return value is long || value is int || value is short || value is byte || value is double || value is float;
    }

    private bool ToBoolean(object value)
    {
        if (value == null) return false;
        if (value is bool) return (bool)value;
        if (value is string) return !string.IsNullOrWhiteSpace((string)value);
        if (value is long) return (long)value != 0L;
        if (value is int) return (int)value != 0;
        if (value is double) return Math.Abs((double)value) > double.Epsilon;
        return true;
    }

    private bool IsDoubleLike(object value)
    {
        return value is double || value is float;
    }

    private double ToDouble(object value)
    {
        if (value == null) return 0d;
        if (value is double) return (double)value;
        if (value is float) return (float)value;
        if (value is int) return (int)value;
        if (value is long) return (long)value;
        if (value is char) return (double)((char)value);
        double parsed;
        if (double.TryParse(ConvertToString(value), NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
        {
            return parsed;
        }
        return 0d;
    }

    private long ToLong(object value)
    {
        if (value == null) return 0L;
        if (value is long) return (long)value;
        if (value is int) return (int)value;
        if (value is char) return (long)((char)value);
        if (value is double) return (long)((double)value);
        if (value is float) return (long)((float)value);
        long parsed;
        if (long.TryParse(ConvertToString(value), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
        {
            return parsed;
        }
        return 0L;
    }

    private int ToInt(object value)
    {
        return (int)ToLong(value);
    }

    private string ConvertToString(object value)
    {
        if (value == null) return string.Empty;
        if (value is string) return (string)value;
        if (value is char) return ((char)value).ToString();
        if (value is bool) return ((bool)value).ToString().ToLowerInvariant();
        if (value is double)
        {
            double number = (double)value;
            if (Math.Abs(number - Math.Round(number)) < 0.000001d)
            {
                return number.ToString("F1", CultureInfo.InvariantCulture);
            }
            return number.ToString(CultureInfo.InvariantCulture);
        }
        if (value is float)
        {
            float number = (float)value;
            if (Math.Abs(number - Math.Round(number)) < 0.000001f)
            {
                return number.ToString("F1", CultureInfo.InvariantCulture);
            }
            return number.ToString(CultureInfo.InvariantCulture);
        }
        if (value is long)
        {
            return ((long)value).ToString(CultureInfo.InvariantCulture);
        }
        if (value is int)
        {
            return ((int)value).ToString(CultureInfo.InvariantCulture);
        }
        if (value is List<object>)
        {
            List<object> list = (List<object>)value;
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < list.Count; i++)
            {
                if (i > 0) builder.Append(", ");
                builder.Append(ConvertToString(list[i]));
            }
            return builder.ToString();
        }
        return value.ToString();
    }

    private object CastValue(object value, string targetType)
    {
        if (value == null) return GetDefaultValue(targetType);

        string type = (targetType ?? string.Empty).ToLowerInvariant();
        if (type == "int") return ToInt(value);
        if (type == "long") return ToLong(value);
        if (type == "double") return ToDouble(value);
        if (type == "float") return (float)ToDouble(value);
        if (type == "string") return ConvertToString(value);
        if (type == "char") return (char)ToLong(value);
        if (type == "boolean" || type == "bool") return ToBoolean(value);

        return value;
    }
}

public sealed class TerminalSubmissionResult
{
    public bool IsValid;
    public bool RequiresInput;
    public string Message;
    public string PredictedOutput;
    public TerminalCompilationUnitNode Ast;
}
