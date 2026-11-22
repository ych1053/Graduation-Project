using System;
using System.Collections;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DefaultExecutionOrder(-200)]
public class CoopGameHUD : MonoBehaviour
{
    [SerializeField] private TMP_FontAsset koreanFont;
    [SerializeField] private Round2EndingChest ending;
    private SessionChat chat;
    private TMP_Text timer, history;
    private TMP_InputField input;
    private ScrollRect scroll;
    private RectTransform root;
    private bool typing;
    private int openedFrame;
    private string lastComposition = "";
    private CursorLockMode savedLock;
    private bool savedVisible;
    private bool scrollPending;

    private void Awake()
    {
        if (!koreanFont) { Debug.LogError("CoopGameHUD에 한글 TMP Font Asset을 연결하세요.", this); enabled = false; return; }
        chat = SessionChat.Ensure();
        BuildUI();
        chat.Changed += RefreshHistory;
        RefreshHistory();
        if (!scrollPending) StartCoroutine(ScrollBottom());
    }
    private void Update()
    {
        UpdateTimer();
        bool blocked = Round1PauseMenu.IsOpen || Round1PauseMenu.IsRestarting || ExitSmokeScreen.IsPlaying || (ending && ending.HasStarted);
        input.interactable = !blocked && PhotonNetwork.InRoom;
        if (blocked)
        {
            if (typing) CloseInput();
            return;
        }
        if (typing && Input.GetKeyDown(KeyCode.Escape))
        {
            SessionChat.EscapeConsumedFrame = Time.frameCount;
            CloseInput();
            return;
        }
        if (!typing && PhotonNetwork.InRoom &&
            (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
        {
            BeginInput("");
            input.Select();
            input.ActivateInputField();
        }
    }
    private void LateUpdate() => lastComposition = Input.compositionString;

    private void UpdateTimer()
    {
        double seconds = 0;
        if (PhotonNetwork.InRoom)
        {
            var props = PhotonNetwork.CurrentRoom.CustomProperties;
            string run = props[Round1Reload.RunKey] as string ?? "initial";
            if ((props["run_clock_token"] as string) == run && props["run_start"] is double start)
            {
                double end = ending && ending.HasStarted ? ending.ClearTimeOnNetwork : PhotonNetwork.Time;
                seconds = end - start;
                if (seconds < -2147483) seconds += 4294967.296;
            }
        }
        var time = TimeSpan.FromSeconds(Math.Max(0, seconds));
        timer.text = string.Format("Time  {0:00}:{1:00}.{2:00}", (int)time.TotalMinutes, time.Seconds, time.Milliseconds / 10);
    }

    private void BeginInput(string unused)
    {
        if (typing) return;
        typing = true;
        openedFrame = Time.frameCount;
        savedLock = Cursor.lockState;
        savedVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SessionChat.IsTyping = true;
    }
    private void Submit(string text)
    {
        if (!typing) return;
        if (openedFrame == Time.frameCount) { StartCoroutine(Refocus()); return; }
        // 한글 조합 확정 Enter가 전송으로 처리되지 않도록 한 번 더 입력받는다.
        if (!string.IsNullOrEmpty(Input.compositionString) || !string.IsNullOrEmpty(lastComposition))
        { StartCoroutine(Refocus()); return; }
        if (!string.IsNullOrWhiteSpace(text) && !chat.Send(text))
        { Debug.LogWarning("채팅을 보내지 못했습니다."); StartCoroutine(Refocus()); return; }
        CloseInput();
    }
    private IEnumerator Refocus()
    {
        yield return null;
        if (typing && input.interactable) { input.Select(); input.ActivateInputField(); }
    }
    private void CloseInput()
    {
        if (!typing) return;
        typing = false;
        SessionChat.IsTyping = false;
        SessionChat.BlockUntil = Time.unscaledTime + 0.15f;
        input.SetTextWithoutNotify("");
        input.DeactivateInputField();
        Cursor.lockState = savedLock;
        Cursor.visible = savedVisible;
    }
    private void Deselect(string unused)
    {
        // 마우스로 다른 UI를 선택했을 때도 조작을 복구한다.
        if (typing && !Input.GetKeyDown(KeyCode.Return) && !Input.GetKeyDown(KeyCode.KeypadEnter)) CloseInput();
    }
    private void RefreshHistory()
    {
        bool atBottom = scroll.verticalNormalizedPosition <= 0.05f;
        history.text = chat.History;
        if ((atBottom || history.text.Length == 0) && !scrollPending) StartCoroutine(ScrollBottom());
    }
    private IEnumerator ScrollBottom()
    {
        scrollPending = true;
        yield return null;
        Canvas.ForceUpdateCanvases();
        scroll.verticalNormalizedPosition = 0;
        scrollPending = false;
    }
    private void OnDisable() { if (typing) CloseInput(); }
    private void OnDestroy()
    {
        if (chat) chat.Changed -= RefreshHistory;
    }

    private RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }
    private void Stretch(RectTransform rect, float left = 0, float bottom = 0, float right = 0, float top = 0)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom); rect.offsetMax = new Vector2(-right, -top);
    }
    private TMP_Text Text(string name, Transform parent, float size)
    {
        var rect = Rect(name, parent);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = koreanFont; text.fontSize = size; text.color = Color.white;
        text.richText = false; text.raycastTarget = false;
        text.alignment = TextAlignmentOptions.TopLeft;
        return text;
    }
    private void BuildUI()
    {
        root = Rect("GameHUDCanvas", transform);
        var canvas = root.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -10;
        var scaler = root.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
        root.gameObject.AddComponent<GraphicRaycaster>();

        timer = Text("TimerText", root, 36);
        var timerRect = timer.rectTransform;
        timerRect.anchorMin = timerRect.anchorMax = timerRect.pivot = new Vector2(0, 1);
        timerRect.anchoredPosition = new Vector2(28, -24); timerRect.sizeDelta = new Vector2(420, 60);

        var panel = Rect("ChatPanel", root);
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(1, 0);
        panel.anchoredPosition = new Vector2(-24, 24); panel.sizeDelta = new Vector2(560, 300);
        panel.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.5f);
        var scrollArea = Rect("ChatScroll", panel); Stretch(scrollArea, 12, 58, 12, 12);
        scroll = scrollArea.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
        scrollArea.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.01f);
        var viewport = Rect("Viewport", scrollArea); Stretch(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();
        history = Text("History", viewport, 22);
        var content = history.rectTransform;
        content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
        content.pivot = new Vector2(0.5f, 1); content.sizeDelta = Vector2.zero;
        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = viewport; scroll.content = content; scroll.verticalNormalizedPosition = 0;

        var field = Rect("ChatInput", panel);
        field.anchorMin = field.anchorMax = new Vector2(0.5f, 0);
        field.pivot = new Vector2(0.5f, 0); field.anchoredPosition = new Vector2(0, 10);
        field.sizeDelta = new Vector2(536, 38);
        var image = field.gameObject.AddComponent<Image>(); image.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);
        input = field.gameObject.AddComponent<TMP_InputField>(); input.targetGraphic = image;
        var textArea = Rect("Text Area", field); Stretch(textArea, 8, 3, 8, 3);
        textArea.gameObject.AddComponent<RectMask2D>();
        var value = (TextMeshProUGUI)Text("Text", textArea, 22); Stretch(value.rectTransform);
        var hint = Text("Placeholder", textArea, 20); Stretch(hint.rectTransform);
        hint.text = "Enter: 채팅 입력 / 전송   Esc: 취소"; hint.color = Color.gray;
        input.textViewport = textArea; input.textComponent = value; input.placeholder = hint;
        input.lineType = TMP_InputField.LineType.SingleLine; input.characterLimit = 300;
        input.richText = false;
        input.onSelect.AddListener(BeginInput); input.onSubmit.AddListener(Submit); input.onDeselect.AddListener(Deselect);
        if (!EventSystem.current)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }
}
