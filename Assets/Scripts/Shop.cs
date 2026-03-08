using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;

public class Shop : MonoBehaviour
{
    public CharacterDatabase characterDB;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI costText;
    public TextMeshProUGUI tokenText;
    public SpriteRenderer artworkSprite;
    public Button buyButton;

    private int selectedOption = 0;
    private int playerTokens;

    void Start()
    {
        LoadTokens();
        UpdateCharacterDisplay(selectedOption);
    }

    public void NextOption()
    {
        selectedOption++;
        if (selectedOption >= characterDB.CharacterCount)
        {
            selectedOption = 0;
        }
        UpdateCharacterDisplay(selectedOption);
    }

    public void BackOption()
    {
        selectedOption--;
        if (selectedOption < 0)
        {
            selectedOption = characterDB.CharacterCount - 1;
        }
        UpdateCharacterDisplay(selectedOption);
    }

    private void UpdateCharacterDisplay(int index)
    {
        Characters character = characterDB.GetCharacter(index);
        artworkSprite.sprite = character.characterSprite;
        nameText.text = character.CharacterName;

        // Check if character is already owned
        bool isOwned = IsCharacterOwned(index);

        if (character.cost == 0 || isOwned)
        {
            // Free character or already owned
            costText.text = isOwned ? "OWNED" : "FREE";
            buyButton.interactable = false;
        }
        else
        {
            // Character needs to be purchased
            costText.text = "Cost: " + character.cost + " Tokens";
            buyButton.interactable = !isOwned && playerTokens >= character.cost;
        }

        tokenText.text = "Tokens: " + playerTokens;
    }

    public void BuyCharacter()
    {
        Characters character = characterDB.GetCharacter(selectedOption);

        // Check if player has enough tokens
        if (playerTokens >= character.cost && !IsCharacterOwned(selectedOption))
        {
            // Deduct tokens
            playerTokens -= character.cost;
            SaveTokens();

            // Mark character as owned
            PlayerPrefs.SetInt("Character_" + selectedOption, 1);
            PlayerPrefs.Save();

            // Update display
            UpdateCharacterDisplay(selectedOption);
        }
    }

    private bool IsCharacterOwned(int index)
    {
        // Character at index 0 is always free/owned by default
        if (index == 0) return true;

        // Check if character is free
        Characters character = characterDB.GetCharacter(index);
        if (character.cost == 0) return true;

        // Check if purchased
        return PlayerPrefs.GetInt("Character_" + index, 0) == 1;
    }

    private void LoadTokens()
    {
        playerTokens = PlayerPrefs.GetInt("PlayerTokens", 0);
    }

    private void SaveTokens()
    {
        PlayerPrefs.SetInt("PlayerTokens", playerTokens);
        PlayerPrefs.Save();
    }

    public void ExitShop()
    {
        // Return to Main scene
        SceneManager.LoadSceneAsync("Main");
    }
}
