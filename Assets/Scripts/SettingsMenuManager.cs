using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;
using TMPro;

public class SettingsMenuManager : MonoBehaviour
{

    public Slider volumeSlider;
    public Slider musicSlider;
    public AudioMixer audioMixer;

    public void ChangeMasterVolume()
    {
        float dB = Mathf.Lerp(-80f, 0f, volumeSlider.value);
        audioMixer.SetFloat("Volume", dB);
    }

    public void ChangeMusicVolume()
    {
        float dB = Mathf.Lerp(-80f, 0f, musicSlider.value);
        audioMixer.SetFloat("Music", dB);
    }
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }
}
