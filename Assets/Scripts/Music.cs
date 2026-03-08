using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class Music : MonoBehaviour
{
    [SerializeField] private AudioMixer audioMixer;
    [SerializeField] private Slider musicSlider;

    void Start()
    {
        // Initialize slider value from mixer
        if (musicSlider != null)
        {
            musicSlider.onValueChanged.AddListener(SetMusicVolume);
            // Set default slider value (1 = max volume)
            musicSlider.value = 1f;
        }
    }

    public void SetMusicVolume(float volume)
    {
        if (audioMixer != null)
        {
            // Convert linear slider value (0-1) to decibels (-80 to 0)
            float dB = volume > 0.0001f ? Mathf.Log10(volume) * 20f : -80f;
            audioMixer.SetFloat("Music", dB);
        }
    }
}
