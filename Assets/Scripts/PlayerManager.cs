using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerManager : MonoBehaviour
{
        public static int numberOfCoin;
        public TextMeshProUGUI coinText;

    void Update()
     {
         coinText.text = numberOfCoin.ToString();
     }

     private void Awake()
     {
         numberOfCoin = PlayerPrefs.GetInt("numberOfCoin", 0);
     }
}
