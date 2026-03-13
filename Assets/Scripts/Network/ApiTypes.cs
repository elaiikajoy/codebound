// ============================================================
// ApiTypes.cs
// Purpose: All serializable request / response data types for
//          the CodeBound API.  Used by all Network/ services.
//
// No MonoBehaviour — pure C# data classes.
// ============================================================

// ─── Game Config (matches StreamingAssets/game.config.json) ──────────────────
[System.Serializable]
public class GameConfig
{
    public string backendBaseUrl;
    public string apiKey;
    public string gameVersion;
    public bool debugMode;
}

// ─── Shared base response ─────────────────────────────────────────────────────
[System.Serializable]
public class ApiBaseResponse
{
    public bool success;
    public string message;
}

// ─── Auth ─────────────────────────────────────────────────────────────────────
[System.Serializable]
public class LoginRequest
{
    public string email;
    public string password;
}

[System.Serializable]
public class RegisterRequest
{
    public string username;
    public string email;
    public string password;
}

[System.Serializable]
public class UserProgressData
{
    public int currentLevel;
    public int highestLevel;
    public int totalTokens;
    public float totalPlayTime;
    public string equippedCharacter;
}

[System.Serializable]
public class UserData
{
    public string id;
    public string username;
    public string email;
    public string avatar;
    public UserProgressData progress;
}

[System.Serializable]
public class AuthData
{
    public string token;
    public UserData user;
}

[System.Serializable]
public class AuthResponse
{
    public bool success;
    public string message;
    public AuthData data;
}

[System.Serializable]
public class SessionData
{
    public UserData user;
}

[System.Serializable]
public class SessionResponse
{
    public bool success;
    public string message;
    public SessionData data;
}

// ─── Progress ─────────────────────────────────────────────────────────────────
[System.Serializable]
public class ProgressUpdateRequest
{
    public int levelCompleted;
    public int tokensEarned;
    public float timeSpent;
    public int hintsUsed;
    public bool isPerfect;
    public bool hasCodeErrors;
}

[System.Serializable]
public class ProgressResponse
{
    public bool success;
    public string message;
    public UserProgressData data;
}

[System.Serializable]
public class ProgressStats
{
    public int currentLevel;
    public int highestLevel;
    public int totalTokens;
    public float averageTimePerLevel;
    public int perfectLevels;
    public int totalLevelsCompleted;
}

[System.Serializable]
public class ProgressStatsResponse
{
    public bool success;
    public string message;
    public ProgressStats data;
}

// ─── Leaderboard ──────────────────────────────────────────────────────────────
[System.Serializable]
public class LeaderboardEntry
{
    public int rank;
    public string username;
    public int levelReached;
    public int tokensEarned;
    public float totalPlayTime;
}

[System.Serializable]
public class LeaderboardPagination
{
    public int total;
    public bool hasMore;
}

[System.Serializable]
public class LeaderboardData
{
    // Array nested inside "data" object — JsonUtility handles this correctly.
    public LeaderboardEntry[] players;
    public LeaderboardPagination pagination;
}

[System.Serializable]
public class LeaderboardResponse
{
    public bool success;
    public string message;
    public LeaderboardData data;
}

[System.Serializable]
public class PlayerRankData
{
    public int rank;
}

[System.Serializable]
public class PlayerRankResponse
{
    public bool success;
    public string message;
    public PlayerRankData data;
}

// ─── Characters ───────────────────────────────────────────────────────────────
[System.Serializable]
public class CharacterItem
{
    public string id;
    public string name;
    public int tokenCost;
    public bool isDefault;
}

[System.Serializable]
public class EquipCharacterRequest
{
    public string characterId;
}

[System.Serializable]
public class EquipCharacterData
{
    public string equippedCharacter;
}

[System.Serializable]
public class EquipCharacterResponse
{
    public bool success;
    public string message;
    public EquipCharacterData data;
}

// ─── JsonUtility workaround: arrays in "data" field ──────────────────────────
// JsonUtility cannot parse top-level arrays, so SkinService wraps them.
[System.Serializable]
public class CharacterItemArrayWrapper
{
    public CharacterItem[] items;
}
