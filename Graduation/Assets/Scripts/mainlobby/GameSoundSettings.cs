using UnityEngine;

// AudioSource 각각을 건드리지 않고 게임 전체 출력 음량을 제어한다.
public static class GameSoundSettings
{
    private const string MuteKey = "Graduation.Audio.Muted";
    private const string VolumeKey = "Graduation.Audio.VolumeSlider";
    private const float ReferenceVolume = 0.5f;
    private static bool muted;
    private static float sliderValue = 0.5f;

    public static bool IsMuted => muted;
    public static float SliderValue => sliderValue;
    public static float VolumeMultiplier => Mathf.Pow(4f, sliderValue - 0.5f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        muted = PlayerPrefs.GetInt(MuteKey, 0) == 1;
        sliderValue = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 0.5f));
        ApplyVolume();
    }

    public static void SetVolumeSlider(float value)
    {
        sliderValue = Mathf.Clamp01(value);
        ApplyVolume();
        PlayerPrefs.SetFloat(VolumeKey, sliderValue);
        PlayerPrefs.Save();
    }

    public static void SetMuted(bool value)
    {
        muted = value;
        ApplyVolume();
        PlayerPrefs.SetInt(MuteKey, muted ? 1 : 0);
        PlayerPrefs.Save();
    }

    private static void ApplyVolume()
    {
        AudioListener.volume = muted ? 0f : ReferenceVolume * VolumeMultiplier;
    }
}
