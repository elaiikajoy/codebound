# Achievement Integration Plan

## Goal
Enable achievement claim buttons per user based on backend progress, then claim reward tokens once and persist the claim in the database.

## Data Source
- `highestLevel` controls all level-based achievements.
- `totalTokens` controls all token milestone achievements.
- `user_achievements.claimedAt` marks whether the reward was already claimed.

## Backend Flow
1. Player logs in.
2. Unity requests `GET /achievements/progress`.
3. Backend returns all achievements with:
   - `isUnlocked`
   - `isClaimed`
   - `canClaim`
4. Unity enables only the claim buttons where `canClaim = true`.
5. When the player clicks claim, Unity sends `POST /achievements/claim` with `achievementId`.
6. Backend verifies eligibility, increments `totalTokens`, sets `claimedAt`, and returns the updated progress.
7. Unity refreshes the panel and syncs local token UI.

## Unity Setup
- Attach `AchievementService` to the persistent `GameAPI` object.
- Attach `AchievementPanelController` to the achievement panel root.
- Bind each row's:
  - `achievementId`
  - title text
  - description text
  - reward text
  - requirement text
  - status text
  - claim button

## Required DB Change
- Add `claimedAt` to `user_achievements`.

## Suggested Validation
- Login as a new user and verify only the welcome gift becomes claimable when eligible.
- Reach Level 10, 20, 30, etc. and confirm each row unlocks.
- Claim each reward once and confirm the button disables after refresh.
- Raise token total to 10,000, 100,000, and 1,000,000 and verify token milestones unlock.
