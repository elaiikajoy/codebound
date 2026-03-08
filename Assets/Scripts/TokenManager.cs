using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TokenManager : MonoBehaviour
{
    // Call this method to give tokens to the player
    public static void AddTokens(int amount)
    {
        int currentTokens = PlayerPrefs.GetInt("PlayerTokens", 0);
        currentTokens += amount;
        PlayerPrefs.SetInt("PlayerTokens", currentTokens);
        PlayerPrefs.Save();
    }
    
    // Call this to get current token count
    public static int GetTokens()
    {
        return PlayerPrefs.GetInt("PlayerTokens", 0);
    }
    
    // Use this for testing - gives 100 tokens
    public void GiveTestTokens()
    {
        AddTokens(100);
        Debug.Log("Added 100 tokens! Total: " + GetTokens());
    }
    
    // Reset tokens to zero
    public void ResetTokens()
    {
        PlayerPrefs.SetInt("PlayerTokens", 0);
        PlayerPrefs.Save();
    }
}
