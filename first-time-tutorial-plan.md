# First-Time Player Tutorial Plan

## Scope Clarification
This document is for onboarding/tutorial flow only:
- detect first-time player
- show tutorial before gameplay starts
- mark tutorial as completed

This is separate from AST migration and separate from terminal validation logic.

## Role for Coding AI
Act as a **Senior Unity Game Developer (Onboarding UX + Backend Integration)**.

## Decision: Video vs In-Game Tutorial
Use **in-game interactive tutorial UI** as primary implementation.

Reason:
- faster to build and update
- no video-editing dependency
- localized text is easier
- lower app size impact
- can be skipped/replayed

Final direction: **no video tutorial path**. Keep onboarding fully inside game UI.

## Product Requirement
When user is newly registered (or first login), show tutorial before entering normal game flow.

## Current Code Analysis (Integration Findings)
Based on current auth/network flow:
- Login/Register parse `AuthResponse` and `AuthData` (`user`, `token`) only.
- Session restore parses `SessionResponse` (`user`) only.
- No tutorial field currently exists in `UserData`, `AuthData`, `AuthResponse`, or `SessionResponse`.
- Auth success routes are already centralized through:
   - `GameApiManager.Login()`
   - `GameApiManager.Register()`
   - `GameApiManager.TryRestoreSession()`
   - `AuthUIManager` success/session handlers

Meaning:
- We already have good trigger points.
- We still need tutorial state from backend or local per-user fallback.

## Detection Strategy (Recommended)
### Preferred (Backend-driven)
Backend returns one of these in auth/session payload:
- `isNewUser: true/false`
- or `tutorialCompleted: true/false`
- or `createdAt` plus server-side first-session logic

### Database Requirement (Recommended)
Add a persistent field on users table/document:
- `tutorialCompleted` (boolean)

Recommended defaults:
- Existing users (already in DB before rollout): `tutorialCompleted = true`
- Newly created users after rollout: `tutorialCompleted = false`

This prevents old users from unexpectedly seeing first-time tutorial.

### Fallback (Client-driven)
If backend flag is not available, store per-user key in PlayerPrefs:
- key format: `tutorial_seen_{userId}`
- if missing -> show tutorial
- when completed -> set to `1`

Use versioned key to support future tutorial updates:
- `tutorial_v1_seen_{userId}`

## Trigger Points
Show tutorial check after:
- successful Register
- successful Login
- successful Session Restore

Integrate in auth flow manager so tutorial appears before main gameplay starts.

## Old User vs New User Safety Rules
1. Backend decision is source of truth when available.
2. Old user must not be treated as first-time user after deployment.
3. New user must see tutorial exactly once by default.
4. If backend field is missing, fallback to per-user local key.
5. Never use one global device key for tutorial state.
6. Do not clear tutorial completion key on normal logout.

## Backend/API Contract Needed
At least one of these must be returned in both login and session responses:
- `isNewUser`
- `tutorialCompleted`

Minimum recommended contract:
- `user.tutorialCompleted` in auth/session payload

Optional endpoint:
- `POST /users/tutorial-complete`
   - sets `tutorialCompleted = true`
   - idempotent (safe if called multiple times)

## Rollout / Migration Plan (To Avoid Bugs)
1. Add `tutorialCompleted` column/field in DB.
2. Backfill existing users to `true`.
3. Set creation default for new users to `false`.
4. Expose field in login/session responses.
5. Update Unity `ApiTypes` models to include the new field.
6. Apply client fallback only when field is absent.
7. Test with:
    - old existing account
    - brand new account
    - session restore case

## UX Flow
1. User logs in/registers.
2. System checks tutorial state.
3. If first-time:
   - open tutorial overlay/modal
   - lock gameplay input while tutorial is active
4. User taps Next/Back through steps.
5. On Finish:
   - save tutorial completion state
   - continue normal game flow.

Optional controls:
- Skip button
- Replay tutorial from Settings/Profile

## UI Implementation Guide (What to Build in Unity)
Use a **guided overlay tutorial** inside the game UI.

### Minimal Mode (Final for this release)
Use only this:
- Fullscreen dark panel (alpha ~0.55 to 0.7)
- Instruction card in front with step text
- Buttons: Next / Back / Skip / Finish
- Step counter text (e.g., 2/4)

No spotlight hole, no shader mask, no advanced animation for now.

This is the fastest and lowest-bug implementation.

## Suggested Unity Hierarchy
- Canvas
   - TutorialRoot (inactive by default)
      - DimOverlay (Image, full screen, dark)
      - InstructionCard
         - TitleText
         - BodyText
         - BackButton
         - NextButton
         - SkipButton
         - FinishButton
         - StepCounterText (ex: 2/4)

## Basic UI Setup Steps
1. Create `TutorialRoot` under your main UI canvas.
2. Set `TutorialRoot` active only when tutorial starts.
3. `DimOverlay` should block gameplay clicks while active.
4. Instruction card should stay readable on all screen sizes.
5. Add a script (`FirstTimeTutorialController`) that:
    - stores step definitions,
    - updates title/body/counter,
    - handles Next/Back/Skip/Finish.
6. On Finish/Skip, hide tutorial and save completion state.

## Step Content Mapping (Recommended)
For each step define:
- `title`
- `description`
- `allowNextWithoutClick` (true/false)

This makes tutorial data-driven and easy to maintain.

## Bug-Risk Guardrails (Keep Simple)
- Use one boolean gate: `isTutorialActive`.
- Disable gameplay input while tutorial is active; always restore on Skip/Finish.
- Save completion per user key: `tutorial_seen_{userId}`.
- Do not depend on network response timing for showing/hiding tutorial UI.

## UX Rules
- Keep each step text short (1-2 sentences).
- One instruction per step.
- Do not overwhelm with long paragraphs.
- Always provide Skip.
- Add Replay in Settings/Profile.

## Suggested Tutorial Steps (Simple)
1. Movement basics
2. Terminal interaction (how to open terminal)
3. Run/Retry behavior
4. Goal: solve coding challenge and unlock progress

## Files Likely To Change
- `Assets/Scripts/AuthUIManager.cs`
- `Assets/Scripts/Network/GameApiManager.cs`
- `Assets/Scripts/Network/ApiTypes.cs` (if backend adds tutorial flags)
- New onboarding script(s), e.g.:
  - `Assets/Scripts/Tutorial/FirstTimeTutorialController.cs`
  - `Assets/Scripts/Tutorial/TutorialStepView.cs`

## Optional API Additions
If backend support is available, add endpoints/fields:
- Auth/session response includes `isNewUser` or `tutorialCompleted`
- optional endpoint: `POST /users/tutorial-complete`

## Engineering Rules
- Do not block login success; only gate gameplay entry until tutorial decision is resolved.
- Tutorial must be per-user, not per-device-global.
- Handle missing network gracefully using local fallback.
- Keep tutorial skippable and replayable.
- Keep tutorial completion idempotent (multiple Finish calls should not break state).

## Acceptance Criteria
- New user sees tutorial automatically before gameplay.
- Returning user does not see tutorial repeatedly.
- Tutorial completion persists per user.
- If backend flag is missing, client fallback still works.

## QA Checklist
1. Register brand new account -> tutorial appears.
2. Logout/login same account -> tutorial does not auto-show again.
3. New second account on same device -> tutorial appears for that account.
4. Skip tutorial -> gameplay opens; state saved.
5. Replay tutorial manually from menu works.

## Copy-Paste Prompt for Claude
Act as a Senior Unity Game Developer (Onboarding UX + Backend Integration).

Implement first-time tutorial flow in this Unity project:
1. Detect first-time users (backend flag preferred; PlayerPrefs per-user fallback).
2. Show tutorial overlay before normal gameplay for first-time users.
3. Persist tutorial completion per user.
4. Keep flow non-breaking for login/session restore.
5. Add skip + replay support.

Return:
- changed file list
- detection logic used (backend + fallback)
- where tutorial is triggered in auth/session flow
- QA results for first-time vs returning users.
