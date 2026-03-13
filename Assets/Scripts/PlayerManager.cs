using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class PlayerManager : MonoBehaviour
{
    public static bool isGameOver;
    public GameObject gameOverPanel;
        public static int numberOfCoin;
        public TextMeshProUGUI coinText;

    void Update()
     {

        if (isGameOver)
        {
            gameOverPanel.SetActive(true);
        }
         coinText.text = numberOfCoin.ToString();
     }

     private void Awake()
     {
        isGameOver = false;
         numberOfCoin = PlayerPrefs.GetInt("numberOfCoin", 0);
     }
     public void ReplayLevel()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
    public void PauseGame()
    {
        Time.timeScale = 0;
    }
}
