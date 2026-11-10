using Photon.Pun;
using UnityEngine;

public class PrologueDirector : MonoBehaviour
{
    [Header("화면 이동")]
    public RectTransform storyRoot;
    public RectTransform centralCharacter;

    [Header("5~10번")]
    public CanvasGroup rangerReveal;
    public CanvasGroup treasureReveal;
    public CanvasGroup holdingChestCharacter;
    public CanvasGroup image09;
    public CanvasGroup image10;

    [Header("12~19번")]
    public CanvasGroup image12;
    public CanvasGroup image13;
    public CanvasGroup image15;
    public CanvasGroup fallingCharacter;
    public RectTransform fallingCharacterRect;
    public CanvasGroup fadeOverlay;

    private double sharedStartAt;
    private float localStartAt;
    private float fallingCharacterY;
    private bool loadingStage;

    private void Start()
    {
        localStartAt = Time.unscaledTime;
        fallingCharacterY = fallingCharacterRect.anchoredPosition.y;

        if (PhotonNetwork.InRoom &&
            PhotonNetwork.CurrentRoom.CustomProperties
                .ContainsKey("prologueStartAt"))
        {
            sharedStartAt = (double)PhotonNetwork.CurrentRoom
                .CustomProperties["prologueStartAt"];
        }

        storyRoot.anchoredPosition = Vector2.zero;
        centralCharacter.localScale = Vector3.one * 0.35f;

        SetHidden(rangerReveal);
        SetHidden(treasureReveal);
        SetHidden(holdingChestCharacter);
        SetHidden(image09);
        SetHidden(image10);
        SetHidden(image12);
        SetHidden(image13);
        SetHidden(image15);
        SetHidden(fallingCharacter);
        SetHidden(fadeOverlay);
    }

    private void Update()
    {
        if (loadingStage)
            return;

        float t = sharedStartAt > 0
            ? (float)(PhotonNetwork.Time - sharedStartAt)
            : Time.unscaledTime - localStartAt;

        if (t < 0f)
            return;

        // 1~3: 중앙 캐릭터가 커짐
        float zoom = Mathf.Clamp01(t / 3.5f);
        centralCharacter.localScale =
            Vector3.one * Mathf.Lerp(0.1f, 4f, zoom);

        // 4, 7, 11, 14, 16: 화면이 순서대로 내려감
        float pan1 = Mathf.Clamp01((t - 3f) / 1.3f);
        float pan2 = Mathf.Clamp01((t - 7f) / 1.3f);
        float pan3 = Mathf.Clamp01((t - 12f) / 1.3f);
        float pan4 = Mathf.Clamp01((t - 16f) / 1.3f);
        float pan5 = Mathf.Clamp01((t - 19f) / 1.3f);

        float screenY =
            1080f * (pan1 + pan2 + pan3 + pan4 + pan5);

        storyRoot.anchoredPosition = new Vector2(0f, screenY);

        // 5~6: Ranger, 보물상자 등장
        FadeIn(rangerReveal, t, 4.5f, 0.6f);
        FadeIn(treasureReveal, t, 5.5f, 0.6f);

        // 8~10: 보물상자를 든 캐릭터와 이미지 등장
        FadeIn(holdingChestCharacter, t, 8.5f, 0.6f);
        FadeIn(image09, t, 9.5f, 0.5f);
        FadeIn(image10, t, 10.5f, 0.5f);

        // 12~13: 두 이미지가 순서대로 등장
        FadeIn(image12, t, 13.5f, 0.6f);
        FadeIn(image13, t, 14.5f, 0.6f);

        // 15: 가로로 긴 이미지 등장
        FadeIn(image15, t, 17.5f, 0.8f);

        // 16: 마지막 캐릭터 등장
        FadeIn(fallingCharacter, t, 20.5f, 0.6f);

        // 17: 캐릭터가 위아래로 흔들림
        float shake = t >= 21.5f && t < 23.5f
            ? Mathf.Sin((t - 21.5f) * 15f) * 105f
            : 0f;

        // 18: 캐릭터가 아래로 떨어짐
        float fall =
            Mathf.Clamp01((t - 23.5f) / 1.3f) * 1800f;

        fallingCharacterRect.anchoredPosition = new Vector2(
            fallingCharacterRect.anchoredPosition.x,
            fallingCharacterY + shake - fall
        );

        // 19: 화면 암전
        fadeOverlay.alpha =
            Mathf.Clamp01((t - 24.8f) / 2f);

        // 20: 두 플레이어가 Round_1로 이동
        if (t >= 27f &&
            PhotonNetwork.InRoom &&
            PhotonNetwork.IsMasterClient)
        {
            loadingStage = true;
            //PhotonNetwork.LoadLevel("Round_1");
            PrologueSkipVote.LoadRound1Once();
        }
    }

    private void SetHidden(CanvasGroup group)
    {
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
    }

    private void FadeIn(
        CanvasGroup group,
        float elapsed,
        float startTime,
        float duration)
    {
        group.alpha =
            Mathf.Clamp01((elapsed - startTime) / duration);
    }
}