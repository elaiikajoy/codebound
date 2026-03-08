using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class CharacterManager : MonoBehaviour
{
    public CharacterDatabase characterDB;

    public TextMeshProUGUI nameText;
    public SpriteRenderer artworkSprite;
    private int selectedOption = 0;
    void Start()
    {

        if(!PlayerPrefs.HasKey("SelectedCharacter"))
        {
            selectedOption = 0;
        }
        else
        {
            Load();
        }
      UpdateCharacter(selectedOption);
      Save();
    }
    public void NextOption()
    {
        selectedOption++;

        if(selectedOption >= characterDB.CharacterCount)
        {
            selectedOption = 0;
        }

        UpdateCharacter(selectedOption);
        Save(); 
    }

    public void BackOption()
    {
        selectedOption--;

        if(selectedOption < 0)
        {
            selectedOption = characterDB.CharacterCount - 1;
        }

        UpdateCharacter(selectedOption);
    }
    private void UpdateCharacter(int selectedOption)
    {
      Characters character = characterDB.GetCharacter(selectedOption);
        artworkSprite.sprite = character.characterSprite;
        nameText.text = character.CharacterName;
    }

    private void Load()

    {
        selectedOption = PlayerPrefs.GetInt("SelectedCharacter");
    }

    private void Save()
    {
        PlayerPrefs.SetInt("SelectedCharacter", selectedOption);
    }

    public void ChangeScene(int sceneID)
    {
        SceneManager.LoadSceneAsync(sceneID);
    }
}
