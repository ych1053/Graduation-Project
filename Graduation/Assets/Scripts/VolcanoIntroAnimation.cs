using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class VolcanoIntroAnimation : MonoBehaviour
{
    [Header("화면 오브젝트 연결")]
    public Image introBackground;
    public Image characterArt;
    public GameObject continuePrompt;
    public Image flashImage;
    public GameObject titleScreen;

    [Header("효과음")]
    public AudioClip cannonSound;
    [Range(0f, 1f)] public float soundVolume = 0.8f;

    [Header("연출 설정")]
    public float launchDelay = 0.8f;
    public float flightDuration = 1.3f;
    public Vector2 volcanoPosition = new Vector2(300f, 270f);
    public Vector2 centerPosition = new Vector2(0f, -10f);
    public Vector2 finalArtSize = new Vector2(850f, 650f);

    private AudioSource audioSource;
    private bool canContinue;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();

        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.volume = soundVolume;
    }

    private IEnumerator Start()
    {
        if (titleScreen != null)
            titleScreen.SetActive(false);

        if (continuePrompt != null)
            continuePrompt.SetActive(false);

        if (characterArt != null)
        {
            RectTransform artRect = characterArt.rectTransform;
            artRect.anchorMin = new Vector2(0.5f, 0.5f);
            artRect.anchorMax = new Vector2(0.5f, 0.5f);
            artRect.pivot = new Vector2(0.5f, 0.5f);
            artRect.sizeDelta = finalArtSize;
            artRect.anchoredPosition = volcanoPosition;
            artRect.localScale = Vector3.one * 0.06f;
            characterArt.preserveAspect = true;
            characterArt.gameObject.SetActive(false);
        }

        if (flashImage != null)
        {
            Color color = flashImage.color;
            color.a = 0f;
            flashImage.color = color;
        }

        yield return new WaitForSeconds(launchDelay);

        if (characterArt == null)
            yield break;

        characterArt.gameObject.SetActive(true);

        // 캐릭터가 나오기 시작할 때 대포 소리 재생
        if (cannonSound != null)
            audioSource.PlayOneShot(cannonSound, soundVolume);
        else
            Debug.LogWarning("Cannon Sound가 Inspector에 연결되지 않았습니다.");

        StartCoroutine(FlashOnce());
        yield return StartCoroutine(FlyToCenter());

        if (continuePrompt != null)
            continuePrompt.SetActive(true);

        canContinue = true;
    }

    private void Update()
    {
        if (!canContinue || !Input.anyKeyDown)
            return;

        canContinue = false;

        if (introBackground != null)
            introBackground.gameObject.SetActive(false);

        if (characterArt != null)
            characterArt.gameObject.SetActive(false);

        if (continuePrompt != null)
            continuePrompt.SetActive(false);

        if (titleScreen != null)
            titleScreen.SetActive(true);
    }

    private IEnumerator FlyToCenter()
    {
        RectTransform artRect = characterArt.rectTransform;
        Vector2 start = volcanoPosition;
        Vector2 end = centerPosition;
        Vector2 arcControl = (start + end) * 0.5f + Vector2.up * 240f;

        float elapsed = 0f;

        while (elapsed < flightDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / flightDuration);
            float oneMinusT = 1f - t;

            artRect.anchoredPosition =
                oneMinusT * oneMinusT * start +
                2f * oneMinusT * t * arcControl +
                t * t * end;

            float scale = EaseOutBack(t);
            artRect.localScale = Vector3.one * Mathf.Lerp(0.06f, 1f, scale);
            artRect.localRotation =
                Quaternion.Euler(0f, 0f, Mathf.Lerp(18f, 0f, t));

            yield return null;
        }

        artRect.anchoredPosition = end;
        artRect.localScale = Vector3.one;
        artRect.localRotation = Quaternion.identity;
    }

    private float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;

        return 1f + c3 * Mathf.Pow(t - 1f, 3f)
                   + c1 * Mathf.Pow(t - 1f, 2f);
    }

    private IEnumerator FlashOnce()
    {
        if (flashImage == null)
            yield break;

        const float duration = 0.25f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            Color color = flashImage.color;
            color.a = Mathf.Sin(t * Mathf.PI) * 0.5f;
            flashImage.color = color;

            yield return null;
        }

        Color finalColor = flashImage.color;
        finalColor.a = 0f;
        flashImage.color = finalColor;
    }
}