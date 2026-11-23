using UnityEngine;
using UnityEngine.UI;

// TitleScreen에 붙인다. 숨기는 SettingsPanel 자체에는 붙이지 않는다.
public sealed class TitleSoundSettingsUI : MonoBehaviour
{
    [SerializeField] private Button settingsButton;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private Toggle muteToggle;
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private Button closeButton;

    private bool ready;

    private void Awake()
    {
        if (settingsButton == null || settingsPanel == null || muteToggle == null || volumeSlider == null)
        {
            Debug.LogError("Settings Button, Settings Panel, Mute Toggle, Volume Slider를 연결하세요.", this);
            enabled = false;
            return;
        }

        ready = true;
        settingsButton.onClick.AddListener(OpenPanel);
        muteToggle.onValueChanged.AddListener(OnMuteChanged);
        volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
        if (closeButton != null)
            closeButton.onClick.AddListener(ClosePanel);

        muteToggle.toggleTransition = Toggle.ToggleTransition.None;
        volumeSlider.wholeNumbers = false;
        volumeSlider.minValue = 0f;
        volumeSlider.maxValue = 1f;
        volumeSlider.SetValueWithoutNotify(GameSoundSettings.SliderValue);
        settingsPanel.SetActive(false);
    }

    private void OnEnable()
    {
        if (ready)
        {
            muteToggle.SetIsOnWithoutNotify(GameSoundSettings.IsMuted);
            volumeSlider.SetValueWithoutNotify(GameSoundSettings.SliderValue);
        }
    }

    public void OpenPanel()
    {
        if (!ready) return;
        muteToggle.SetIsOnWithoutNotify(GameSoundSettings.IsMuted);
        volumeSlider.SetValueWithoutNotify(GameSoundSettings.SliderValue);
        settingsPanel.SetActive(true);
        muteToggle.Select();
    }

    public void ClosePanel()
    {
        if (!ready) return;
        settingsPanel.SetActive(false);
        if (settingsButton.gameObject.activeInHierarchy)
            settingsButton.Select();
    }

    private void OnMuteChanged(bool isChecked)
    {
        // 체크 표시 있음 = 음소거 켜짐.
        GameSoundSettings.SetMuted(isChecked);
    }

    private void OnVolumeChanged(float value)
    {
        GameSoundSettings.SetVolumeSlider(value);
    }

    private void OnDisable()
    {
        if (ready && settingsPanel != null)
            settingsPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (!ready) return;
        if (settingsButton != null)
            settingsButton.onClick.RemoveListener(OpenPanel);
        if (muteToggle != null)
            muteToggle.onValueChanged.RemoveListener(OnMuteChanged);
        if (volumeSlider != null)
            volumeSlider.onValueChanged.RemoveListener(OnVolumeChanged);
        if (closeButton != null)
            closeButton.onClick.RemoveListener(ClosePanel);
    }
}
