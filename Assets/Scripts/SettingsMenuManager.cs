using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;
using TMPro;

public class SettingsMenuManager : MonoBehaviour
{
    private const string MasterVolumePrefKey = "MasterVolume";
    private const string MusicVolumePrefKey = "MusicVolume";

    public Slider volumeSlider;
    public Slider musicSlider;
    public AudioMixer audioMixer;

    [SerializeField] private string masterExposedParameter = "volume";
    [SerializeField] private string musicExposedParameter = "volume";

    private void Awake()
    {
        if (volumeSlider != null)
        {
            volumeSlider.onValueChanged.AddListener(delegate { ChangeMasterVolume(); });
        }

        if (musicSlider != null)
        {
            musicSlider.onValueChanged.AddListener(delegate { ChangeMusicVolume(); });
        }
    }

    public void ChangeMasterVolume()
    {
        if (audioMixer == null || volumeSlider == null)
        {
            return;
        }

        float sliderValue = Mathf.Clamp(volumeSlider.value, 0.0001f, 1f);
        float dB = Mathf.Log10(sliderValue) * 20f;
        audioMixer.SetFloat(masterExposedParameter, dB);
        PlayerPrefs.SetFloat(MasterVolumePrefKey, volumeSlider.value);
        PlayerPrefs.Save();
    }

    public void ChangeMusicVolume()
    {
        if (audioMixer == null || musicSlider == null)
        {
            return;
        }

        float sliderValue = Mathf.Clamp(musicSlider.value, 0.0001f, 1f);
        float dB = Mathf.Log10(sliderValue) * 20f;
        audioMixer.SetFloat(musicExposedParameter, dB);
        PlayerPrefs.SetFloat(MusicVolumePrefKey, musicSlider.value);
        PlayerPrefs.Save();

        // Directly update BG music AudioSource volume (no mixer group needed)
        BGMusicController bgMusic = FindObjectOfType<BGMusicController>();
        if (bgMusic != null)
        {
            bgMusic.ApplyVolume(musicSlider.value);
        }
    }
    void Start()
    {
        if (volumeSlider != null)
        {
            volumeSlider.value = Mathf.Clamp(PlayerPrefs.GetFloat(MasterVolumePrefKey, 1f), 0.0001f, 1f);
            ChangeMasterVolume();
        }

        if (musicSlider != null)
        {
            musicSlider.value = Mathf.Clamp(PlayerPrefs.GetFloat(MusicVolumePrefKey, 1f), 0.0001f, 1f);
            ChangeMusicVolume();
        }
    }

    // Update is called once per frame
    void Update()
    {

    }

    private void OnDestroy()
    {
        if (volumeSlider != null)
        {
            volumeSlider.onValueChanged.RemoveAllListeners();
        }

        if (musicSlider != null)
        {
            musicSlider.onValueChanged.RemoveAllListeners();
        }
    }
}
