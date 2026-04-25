# Codebound Terminal AST Final Plan

## Scope Clarification (Important)
This document is for the **coding-challenge system** of the in-game terminal:
- challenge structure per level
- AST-based validation rules
- dynamic coding outputs/feedback
- JSON level data for challenge metadata and rule config

This is **separate** from other non-terminal game coding tasks (UI, movement, scenes, audio, etc.).

When this file is given to Claude, the implementation focus must be the **terminal coding challenges pipeline** only.

## Study Title
**Codebound: Logic and Challenges Coding**

## Role for Coding AI
Act as a **Senior Game Developer with Compiler/Language-Tools expertise**.

## Final Decision
1. We will replace regex-based terminal validation with **AST-based validation**.
2. We will **create** a custom lightweight AST pipeline for this project (not install a full external compiler as core solution).
3. We will keep the game-friendly implementation simple and maintainable.
4. New implementation policy: **no regex in the core terminal validation path**.

## Current Code Audit (Double-Check)
Based on the current Unity scripts and level files:
- Core validation is still text/regex-driven (`Contains`, `Regex.IsMatch`, `Regex.Matches`).
- `requiredCodePattern` and `expectedOutputPattern` are regex-dependent in several levels.
- Most level checks are still keyword-based (`requiredKeywords`) and can feel static.

Meaning: the AST migration plan is necessary and correctly scoped.

## Main Objective
Make the coding terminal less static and more dynamic by validating code structure and logic through AST, while preserving easy-to-understand level progression.

## JSON: Keep or Remove?
**Keep JSON.**

Reason:
- JSON is still useful for level metadata, prompts, hints, category, examples, and difficulty.
- AST is for parsing and validation logic.
- These two are complementary: **JSON defines the problem, AST validates the solution**.

## JSON Migration Map (Recommended)
### Keep as-is
- `levelNumber`, `levelName`, `category`, `difficulty`
- `puzzleDescription`, `objective`, `starterCode`, `hints`
- rewards/mechanics/scene fields
- `testCases` (for input/output expectations)

### Gradually Deprecate from core validation
- `requiredCodePattern` (regex)
- `expectedOutputPattern` (regex)
- keyword-only validation as primary (`requiredKeywords`, `forbiddenKeywords`)

### Add for AST-based validation
- `requiredAstRules`
- `forbiddenAstRules`
- optional `outputMode` (e.g., `exact`, `testcases`, `ast-simulated`)

## Dynamic Terminal Direction
The terminal should no longer rely on fragile text matching.

New behavior:
- Parse player code into AST.
- Validate required constructs by node traversal.
- Support flexible formatting and equivalent code styles.
- Show clearer syntax/logic feedback.
- Keep examples simple, dynamic, and per category.

## Level Category Pattern (Simple Progression)
Use these category groups for level design:

- **1-10:** Output
- **11-20:** Variables
- **21-30:** Input
- **31-40:** Conditions
- **41-50:** Switch
- **51-60:** Loops
- **61-70:** Arrays
- **71-80:** Strings
- **81-90:** Methods

## Category Alignment Note
Current dataset includes levels beyond 90 (e.g., up to 100), while this study pattern ends at 90.
Recommended handling:
- 1-90 follow the study categories strictly.
- 91-100 can be marked as advanced/capstone extension levels (outside core study scope), or remapped if required by your paper.

## Scope (Simple and Practical)
### In Scope
- AST lexer/tokenizer
- AST parser for the supported subset
- AST validator rules per level category
- Integration with terminal controller
- Backward-compatible use of existing level JSON where possible

### Out of Scope
- Full language compiler
- Complex runtime execution engine
- Major UI redesign

## Supported Syntax Subset
Only implement what the game currently needs:
- declarations and assignments
- `if` / `else`
- `switch`
- `for` / `while`
- arrays and indexing
- string operations used by levels
- method definitions/calls for simple method levels
- `System.out.println`

## Recommended Rule Model in JSON
Keep existing fields and add AST fields only when needed:
- `requiredAstRules`
- `forbiddenAstRules`

Suggested rule structure:
- `type` (e.g., `containsNode`, `containsCall`, `containsLoop`, `countCall`, `forbidNode`)
- `value` (node/call/symbol)
- `count` (optional)

## Files to Update
- `Assets/Scripts/TerminalLevelController.cs`
- `Assets/Scripts/LevelDataLoader.cs`
- new AST/Validation files under `Assets/Scripts/Terminal/`

Suggested folders:
- `Assets/Scripts/Terminal/Ast/`
- `Assets/Scripts/Terminal/Validation/`

## Engineering Rules
- No regex in core validation, syntax checking, or structural rule checks.
- Keep logic modular: lexer, parser, AST nodes, validator.
- Keep code simple and readable.
- Return clear errors for parse failures.
- Preserve current player flow (open terminal, run code, feedback, complete level).

## Regex-to-AST Replacement Checklist
Replace all old regex/text checks with AST logic:
- `requiredKeywords` (string contains) -> AST symbol/node usage checks
- `forbiddenKeywords` -> AST forbidden node/call checks
- `requiredCodePattern` (regex) -> AST structural rule set
- `requiredPrintlnCount` (regex count) -> AST print-node count
- syntax heuristics -> parser diagnostics
- regex output pattern checks -> AST evaluator or testcase-based output checks

Rule: if any legacy regex remains, it must be temporary compatibility code only and not part of core pass/fail validation.

## Acceptance Criteria
- Core terminal validation is AST-based.
- Levels remain playable and aligned with category progression.
- JSON level data still works.
- Output/examples are dynamic enough to avoid static feel.
- The system is defendable academically under the study title.
- No pass/fail decision depends on regex in the final core validation flow.

## Copy-Paste Prompt for Claude
Act as a Senior Game Developer with Compiler/Language-Tools expertise.

Implement this plan in the Unity project:
1. Replace regex-driven terminal validation with AST-driven validation.
2. Build a project-owned lightweight lexer/parser/AST/validator pipeline.
3. Keep JSON for level metadata and optional AST rule configuration.
4. Keep gameplay flow unchanged from player perspective.
5. Implement level-category-aligned validations for:
	- 1-10 Output
	- 11-20 Variables
	- 21-30 Input
	- 31-40 Conditions
	- 41-50 Switch
	- 51-60 Loops
	- 61-70 Arrays
	- 71-80 Strings
	- 81-90 Methods
6. Keep implementation simple (no full compiler).
7. Return a summary of changed files and how each category uses AST checks.
