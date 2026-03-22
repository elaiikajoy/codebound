// ============================================================
// 1. Script Name: LevelDataLoader.cs
// 2. Purpose: Defines the JSON data structure for IDE levels and handles parsing the static JSON files into usable C# objects.
// 3. Unity Setup Instructions:
//    - Attach to: None (This is a static utility class / serializable data classes).
//    - Required Components: None.
//    - Note: Level JSON files must be stored exactly at 'Resources/LevelData/level_001.json', etc.
// ============================================================

using UnityEngine;

[System.Serializable]
public class LevelTestCase
{
    public string input;
    public string expectedOutput;
    public string[] requiredKeywords;
}

[System.Serializable]
public class LevelData
{
    public int levelNumber;
    public string levelName;
    public string category;
    public int difficulty;
    public string puzzleDescription;
    public string objective;
    public string expectedOutput;
    public string starterCode;
    public string[] hints;
    public LevelTestCase[] testCases;
    public string[] requiredKeywords;
    public string[] forbiddenKeywords;
    public string[] unlockedAchievements;
    public string[] requiredMechanics;
    public int baseTokenReward;
    public int perfectBonus;
    public int speedBonus;
    public string sceneName;
    public int tokensToCollect;
    public bool isLocked;
    public int requiredLevel;
    // When true, the player should not be allowed to fall off the bottom
    // of the playable area for this level (matches behavior of early levels).
    public bool preventFall;
}

public static class LevelDataLoader
{
    public static LevelData LoadLevel(int levelNumber)
    {
        string resourcePath = $"LevelData/level_{levelNumber:000}";
        TextAsset jsonAsset = Resources.Load<TextAsset>(resourcePath);

        if (jsonAsset == null)
        {
            Debug.LogError($"[LevelDataLoader] Missing level file at Resources/{resourcePath}.json");
            return null;
        }

        try
        {
            return JsonUtility.FromJson<LevelData>(jsonAsset.text);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[LevelDataLoader] Failed to parse level {levelNumber}: {ex.Message}");
            return null;
        }
    }
}
