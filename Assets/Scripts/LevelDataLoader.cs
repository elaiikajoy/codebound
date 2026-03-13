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
