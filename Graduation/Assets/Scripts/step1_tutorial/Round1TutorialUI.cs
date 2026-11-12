using System.Collections;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Round1TutorialUI : MonoBehaviour
{
    [System.Serializable]
    public class KeyVisual
    {
        public Image image;
        public Sprite normalImage;
        public Sprite pressedImage;

        public void ShowNormal()
        {
            if (image != null)
                image.sprite = normalImage;
        }

        public void ShowPressed()
        {
            if (image != null)
                image.sprite = pressedImage;
        }
    }

    [SerializeField] private TMP_Text guideText;

    [SerializeField] private GameObject wasdGroup;
    [SerializeField] private GameObject spaceObject;
    [SerializeField] private GameObject shiftObject;

    [SerializeField] private KeyVisual wKey;
    [SerializeField] private KeyVisual aKey;
    [SerializeField] private KeyVisual sKey;
    [SerializeField] private KeyVisual dKey;
    [SerializeField] private KeyVisual spaceKey;
    [SerializeField] private KeyVisual shiftKey;

    [SerializeField] private float runHoldDuration = 0.25f;

    private readonly bool[] movementDone = new bool[4];

    private int step;
    private bool changingStep;
    private float runHoldTime;
    private Color originalTextColor;
    private Round1Player localPlayer;
    private NetworkCarryBox box;

    private bool pathGuideDone;
    private bool pickupGuideDone;
    private bool throwGuideDone;
    private bool throwRequested;

    [SerializeField] private float boxGuideDistance = 2.5f;
    private void Start()
    {
        if (guideText == null ||
            wasdGroup == null ||
            spaceObject == null ||
            shiftObject == null)
        {
            Debug.LogError(
                "Round1TutorialUI의 UI 오브젝트 연결을 확인하세요.",
                this
            );
            enabled = false;
            return;
        }

        originalTextColor = guideText.color;

        wKey.ShowNormal();
        aKey.ShowNormal();
        sKey.ShowNormal();
        dKey.ShowNormal();
        spaceKey.ShowNormal();
        shiftKey.ShowNormal();

        ShowStep();
    }

    private void Update()
    {
        if (changingStep) return;

        switch (step)
        {
            case 0:
                CheckMovementKey(KeyCode.W, 0, wKey);
                CheckMovementKey(KeyCode.A, 1, aKey);
                CheckMovementKey(KeyCode.S, 2, sKey);
                CheckMovementKey(KeyCode.D, 3, dKey);

                if (movementDone[0] &&
                    movementDone[1] &&
                    movementDone[2] &&
                    movementDone[3])
                {
                    StartCoroutine(NextStep());
                }
                break;

            case 1:
                if (Input.GetKeyDown(KeyCode.Space))
                {
                    spaceKey.ShowPressed();
                    StartCoroutine(NextStep());
                }
                break;

            case 2:
                CheckRunning();
                break;

            case 3:
                if (Input.GetMouseButton(1) &&
                    Mathf.Abs(Input.GetAxis("Mouse X")) > 0.05f)
                {
                    StartCoroutine(NextStep());
                }
                break;
            default:
                UpdateInteractionGuide();
                break;
        }
    }

    private void CheckMovementKey(
        KeyCode key,
        int index,
        KeyVisual visual
    )
    {
        if (movementDone[index] || !Input.GetKeyDown(key))
            return;

        movementDone[index] = true;
        visual.ShowPressed();
    }

    private void CheckRunning()
    {
        bool shiftHeld =
            Input.GetKey(KeyCode.LeftShift) ||
            Input.GetKey(KeyCode.RightShift);

        bool wHeld = Input.GetKey(KeyCode.W);
        bool aHeld = Input.GetKey(KeyCode.A);
        bool sHeld = Input.GetKey(KeyCode.S);
        bool dHeld = Input.GetKey(KeyCode.D);

        bool directionHeld = wHeld || aHeld || sHeld || dHeld;

        if (!shiftHeld || !directionHeld)
        {
            runHoldTime = 0f;
            return;
        }

        runHoldTime += Time.deltaTime;

        if (runHoldTime < runHoldDuration)
            return;

        shiftKey.ShowPressed();

        if (wHeld) wKey.ShowPressed();
        if (aHeld) aKey.ShowPressed();
        if (sHeld) sKey.ShowPressed();
        if (dHeld) dKey.ShowPressed();

        StartCoroutine(NextStep());
    }

    private IEnumerator NextStep()
    {
        changingStep = true;
        guideText.color = Color.white;

        yield return new WaitForSeconds(0.6f);

        guideText.color = originalTextColor;
        step++;
        ShowStep();
        changingStep = false;
    }

    private void ShowStep()
    {
        wasdGroup.SetActive(step == 0 || step == 2);
        spaceObject.SetActive(step == 1);
        shiftObject.SetActive(step == 2);

        switch (step)
        {
            case 0:
                guideText.text = "W A S D 키를 눌러 이동해 보세요";
                break;

            case 1:
                guideText.text = "Space 키를 눌러 점프해 보세요";
                break;

            case 2:
                // 첫 WASD 단계에서 바뀐 이미지를 초기화
                wKey.ShowNormal();
                aKey.ShowNormal();
                sKey.ShowNormal();
                dKey.ShowNormal();
                shiftKey.ShowNormal();

                runHoldTime = 0f;
                guideText.text =
                    "Shift와 W A S D 중 하나를 동시에 눌러 달려 보세요";
                break;

            case 3:
                guideText.text =
                    "마우스 오른쪽 버튼을 누른 채 움직여 시점을 돌려 보세요";
                break;

            default:
                guideText.text = PhotonNetwork.IsMasterClient
                    ? "버튼 위에 올라가 길을 열어 주세요"
                    : "길이 열리면 건너편 상자로 이동하세요";
                break;
        }
    }
    private void UpdateInteractionGuide()
    {
        if (!Photon.Pun.PhotonNetwork.InRoom)
            return;

        if (localPlayer == null)
        {
            Round1Player[] players =
                FindObjectsOfType<Round1Player>();

            foreach (Round1Player player in players)
            {
                if (player.photonView.IsMine)
                {
                    localPlayer = player;
                    break;
                }
            }
        }

        if (box == null)
            box = FindObjectOfType<NetworkCarryBox>();

        if (localPlayer == null || box == null)
            return;

        int myActorNumber =
            Photon.Pun.PhotonNetwork.LocalPlayer.ActorNumber;

        bool holdingBox =
            box.HolderActorNumber == myActorNumber;

        bool boxAvailable =
            box.HolderActorNumber == 0;

        bool nearBox =
            Vector3.Distance(
                localPlayer.transform.position,
                box.transform.position
            ) <= boxGuideDistance;

        bool pathRaised = false;

        if (Photon.Pun.PhotonNetwork.CurrentRoom.CustomProperties
            .TryGetValue("round1PathRaised", out object value))
        {
            pathRaised = (bool)value;
        }

        // 길이 한 번 열렸으면 초기 버튼/대기 안내는 다시 표시하지 않음
        if (pathRaised)
            pathGuideDone = true;

        // 상자를 한 번 들었으면 E 안내는 다시 표시하지 않음
        if (holdingBox)
        {
            pickupGuideDone = true;

            if (Input.GetMouseButtonUp(0))
                throwRequested = true;
        }
        else if (throwRequested && pickupGuideDone)
        {
            // 던지기 요청 후 상자를 더 이상 들고 있지 않으면 완료
            throwGuideDone = true;
        }

        if (!pathGuideDone)
        {
            guideText.text =
                Photon.Pun.PhotonNetwork.IsMasterClient
                    ? "버튼 위에 올라가 길을 열어 주세요"
                    : "플레이어 1이 길을 열 때까지 기다리세요";
            return;
        }

        if (throwGuideDone)
        {
            guideText.text = "";
            return;
        }

        if (holdingBox)
        {
            guideText.text =
                "마우스 왼쪽 버튼으로 상자를 던지세요\n" +
                "오래 누를수록 멀리 날아갑니다";
            return;
        }

        if (!pickupGuideDone)
        {
            if (boxAvailable && nearBox)
            {
                guideText.text = "E 키를 눌러 상자를 드세요";
            }
            else
            {
                guideText.text =
                    Photon.Pun.PhotonNetwork.IsMasterClient
                        ? "상자를 건네받아 버튼 위에 놓아 주세요"
                        : "길을 건너 상자가 있는 곳으로 이동하세요";
            }

            return;
        }

        // 이미 E를 배운 뒤 상자를 놓친 경우에도 이전 안내를 반복하지 않음
        guideText.text = "";
    }
}