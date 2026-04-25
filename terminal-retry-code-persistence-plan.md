# Terminal Retry & Code Persistence Plan

## Scope Clarification
This document is only for terminal UX behavior changes in the in-game coding challenge system:
1. Retry button state control
2. Code field persistence on validation errors

This is separate from AST migration and separate from non-terminal systems.

## Role for Coding AI
Act as a **Senior Unity Game Developer (UI/UX + Gameplay Systems)**.

## Problem Statement (Current)
Current behavior in terminal flow:
- Retry button can still be clicked even after a successful/working code run.
- On retry, code input resets to starter code.
- On failed run, code input is hidden first, so user flow feels like terminal reset and user may rewrite code again.

## Target Behavior (Required)
1. **Disable Retry button when code passes** (working output / success state).
2. **Do not reset code field on error**.
3. If run has error, user should still see and continue editing the same code.

## Current Code References
Primary file:
- `Assets/Scripts/TerminalLevelController.cs`

Current methods to change:
- `OnRunPressed()`
- `OnRetryPressed()`
- `HandleRunResult(bool hasCodeErrors, string providedOutput)`
- `OpenForLevel(int levelNumber)` (for state reset per level open)

## Implementation Plan

### A) Add explicit Retry button reference and state
In `TerminalLevelController`:
- Add serialized field:
  - `private UnityEngine.UI.Button retryButton;`
- Add helper method:
  - `private void SetRetryInteractable(bool enabled)`

Rules:
- On level open: Retry enabled = `true`
- On run error: Retry enabled = `true`
- On run success/pass: Retry enabled = `false`

### B) Prevent code reset in Retry
Update `OnRetryPressed()`:
- Remove/reset logic that assigns starter code.
- Keep the existing user code in `codeInputField.text`.
- Retry should only:
  - clear output text,
  - show code editor if hidden,
  - hide console input panel,
  - focus code input field.

### C) Keep code visible/editable after errors
Update `OnRunPressed()` + `HandleRunResult()`:
- Do not force a full “hide editor then wait retry” flow on failed validation.
- If validation fails, keep `codeInputField` visible and interactable.
- Preserve typed code exactly as submitted.

### D) Success state lock
On success path (`hasCodeErrors == false`):
- Disable Retry immediately to avoid unnecessary reset actions during success countdown/sync.
- Keep existing success flow (countdown, progress sync, close terminal) unchanged.

### E) Reset for new level/session
When opening a level (`OpenForLevel`):
- reset retry button back to enabled,
- apply starter code for that level open only (normal behavior),
- preserve current functionality for level initialization.

## Non-Negotiables
- Never wipe user code because of a validation error.
- Retry button must be disabled once run is passed.
- No gameplay/progress sync behavior should break.

## Acceptance Criteria
- Failed run: user code remains intact and editable.
- Retry on failed run: clears output only; does not restore starter code.
- Passed run: Retry button becomes disabled.
- Next level open: terminal initializes correctly and retry is enabled again.

## QA Checklist
1. Enter wrong code -> Run -> see error -> code is still present.
2. Press Retry after error -> output clears, code remains unchanged.
3. Enter correct code -> Run -> success -> Retry button disabled.
4. Open another level -> Retry enabled and starter code for that level appears.

## Copy-Paste Prompt for Claude
Act as a Senior Unity Game Developer (UI/UX + Gameplay Systems).

Implement this terminal behavior update:
1. Disable Retry button once code run is successful.
2. Do not reset code input to starter code on retry after errors.
3. Keep user code visible/editable after failed validation.
4. Preserve existing success countdown/progress sync/close flow.
5. Update only terminal challenge flow files, primarily:
   - `Assets/Scripts/TerminalLevelController.cs`

Return:
- changed file list
- exact logic changes in `OnRunPressed`, `OnRetryPressed`, `HandleRunResult`, and level-open state reset
- quick QA results for the 4 scenarios above.
