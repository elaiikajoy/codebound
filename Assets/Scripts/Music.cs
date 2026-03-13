using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;
using UnityEngine.SceneManagement;



public class Music : MonoBehaviour
{
    private const string MasterVolumePrefKey = "MasterVolume";

    public Slider volumeSlider;
    public AudioMixer mixer;
    [SerializeField] private string exposedVolumeParameter = "volume";

    private void Awake()
    {
        if (volumeSlider != null)
        {
            volumeSlider.onValueChanged.AddListener(delegate { SetVolume(); });
        }
    }

    public void SetVolume()
    {
        if (mixer == null || volumeSlider == null)
        {
            return;
        }

        float sliderValue = Mathf.Clamp(volumeSlider.value, 0.0001f, 1f);
        float dB = Mathf.Log10(sliderValue) * 20f;
        mixer.SetFloat(exposedVolumeParameter, dB);
        PlayerPrefs.SetFloat(MasterVolumePrefKey, volumeSlider.value);
        PlayerPrefs.Save();
    }

    void Start()
    {
        if (mixer == null || volumeSlider == null)
        {
            return;
        }

        float savedSliderValue = PlayerPrefs.GetFloat(MasterVolumePrefKey, 1f);
        volumeSlider.value = Mathf.Clamp(savedSliderValue, 0.0001f, 1f);
        SetVolume();
    }

    private void OnDestroy()
    {
        if (volumeSlider != null)
        {
            volumeSlider.onValueChanged.RemoveAllListeners();
        }
    }
}
