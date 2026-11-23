using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Resources/GameBgm.prefab의 루트에 붙인다.
// 게임을 어느 씬에서 실행해도 이 프리팹을 자동 생성한다.
[DisallowMultipleComponent]
public sealed class GameBgmManager : MonoBehaviour
{
    private const string PrefabPath = "GameBgm";
    private static GameBgmManager instance;

    [Header("구간별 배경음악")]
    [SerializeField] private AudioClip introMusic;
    [SerializeField] private AudioClip round1Music;
    [SerializeField] private AudioClip round2Music;

    [Header("재생 설정")]
    [SerializeField, Range(0f, 1f)] private float volume = 0.35f;
    [SerializeField, Min(0f)] private float fadeSeconds = 0.5f;

    private AudioSource musicSource;
    private Coroutine transition;
    private string currentGroup;
    private AudioClip requestedClip;
    private int lastSceneHandle = int.MinValue;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateManager()
    {
        if (instance != null) return;

        GameObject prefab = Resources.Load<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogWarning("BGM: Assets/Resources/GameBgm.prefab을 만들어 주세요.");
            return;
        }

        if (prefab.GetComponent<GameBgmManager>() == null)
        {
            Debug.LogError("BGM: GameBgm 프리팹 루트에 GameBgmManager가 필요합니다.");
            return;
        }

        Instantiate(prefab);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        // 효과음용 AudioSource와 분리된 BGM 전용 소스다.
        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.spatialBlend = 0f;
        musicSource.volume = 0f;
        musicSource.dopplerLevel = 0f;
        musicSource.ignoreListenerPause = true;

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        if (instance == this)
            ApplyScene(SceneManager.GetActiveScene());
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single)
            ApplyScene(scene);
    }

    private void ApplyScene(Scene scene)
    {
        // 첫 로딩에서 sceneLoaded와 Start가 같은 씬을 두 번 처리하지 않도록 한다.
        if (!scene.IsValid() || !scene.isLoaded || lastSceneHandle == scene.handle)
            return;

        lastSceneHandle = scene.handle;

        switch (scene.name)
        {
            case "Bootstrap":
            case "CharacterSelect":
            case "Prologue":
                SelectMusic("Intro", introMusic, false);
                break;

            case "Round_1":
                SelectMusic("Round1", round1Music, true);
                break;

            case "Round_2":
                SelectMusic("Round2", round2Music, true);
                break;

            // 재시작 중간 씬에서는 현재 곡을 유지한다.
            // Round_1에 도착하면 위에서 곡을 처음부터 재생한다.
            case "Round1Reload":
            case "EndingReplay":
                break;
        }
    }

    private void SelectMusic(string group, AudioClip clip, bool restart)
    {
        if (!restart && currentGroup == group && requestedClip == clip)
            return;

        currentGroup = group;
        requestedClip = clip;

        if (clip == null)
            Debug.LogWarning("BGM: " + group + " 구간의 Audio Clip을 연결해 주세요.", this);

        if (transition != null)
            StopCoroutine(transition);

        transition = StartCoroutine(ChangeMusic(clip));
    }

    private IEnumerator ChangeMusic(AudioClip clip)
    {
        if (musicSource.isPlaying)
            yield return FadeTo(0f);

        musicSource.Stop();
        musicSource.clip = clip;
        musicSource.volume = 0f;

        if (clip != null)
        {
            musicSource.Play();
            yield return FadeTo(volume);
        }

        transition = null;
    }

    private IEnumerator FadeTo(float targetVolume)
    {
        float from = musicSource.volume;
        float elapsed = 0f;

        while (elapsed < fadeSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            musicSource.volume = Mathf.Lerp(from, targetVolume, elapsed / fadeSeconds);
            yield return null;
        }

        musicSource.volume = targetVolume;
    }

    private void OnDestroy()
    {
        if (instance != this) return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        instance = null;
    }
}
