using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Audio;

public class Main : MonoBehaviour
{
    public Slider soundSlider;
    public AudioMixer mixer;

    public void BackToMainMenuFromLevelPanel()
    {
        Debug.Log("BackToMainMenuFromLevelPanel called");
        SaveGameData();
        SceneManager.LoadSceneAsync("Main");
    }

    public void SetSound()
    {
        mixer.SetFloat("Volume", soundSlider.value);
    }

    void Start()
    {
        // Load saved data when Main scene starts
        LoadGameData();
    }

    public void PlayGame()
    {
        // Save data before changing scenes
        SaveGameData();
        SceneManager.LoadSceneAsync("LevelPanel");
    }

    public void OpenShop()
    {
        // Save data before changing scenes
        SaveGameData();
        SceneManager.LoadSceneAsync("Shop");
    }

    public void SaveGameData()
    {
        // Save your data here using PlayerPrefs
        // Example: PlayerPrefs.SetInt("Score", score);
        // Example: PlayerPrefs.SetInt("Level", currentLevel);
        PlayerPrefs.Save();
    }

    public void LoadGameData()
    {
        // Load your data here using PlayerPrefs
        // Example: score = PlayerPrefs.GetInt("Score", 0);
        // Example: currentLevel = PlayerPrefs.GetInt("Level", 1);
    }

    public void ResetGameData()
    {
        // Call this to reset everything to 0
        PlayerPrefs.DeleteAll();
    }

    public void BackToMainMenu()
    {
        // Save data before changing scenes
        SaveGameData();
        SceneManager.LoadSceneAsync("Main");
    }
}
