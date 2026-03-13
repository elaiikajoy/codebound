using UnityEngine;
using UnityEngine.SceneManagement;

public class BGMusicController : MonoBehaviour
{
    private const string MusicVolumePrefKey = "MusicVolume";

    private AudioSource audioSource;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
    }

    void Start()
    {
        ApplySavedVolume();
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplySavedVolume();
    }

    // Called directly by SettingsMenuManager for real-time update
    public void ApplyVolume(float sliderValue)
    {
        if (audioSource == null) return;
        audioSource.volume = Mathf.Clamp(sliderValue, 0f, 1f);
    }

    private void ApplySavedVolume()
    {
        float saved = PlayerPrefs.GetFloat(MusicVolumePrefKey, 1f);
        ApplyVolume(saved);
    }
}
