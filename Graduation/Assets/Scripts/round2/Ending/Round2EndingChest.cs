using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

// Round2Ending에 씬 PhotonView와 함께 붙인다.
[RequireComponent(typeof(PhotonView))]
public class Round2EndingChest : MonoBehaviourPun
{
    [SerializeField] private Transform chestRoot;
    [SerializeField] private Collider chestCollider;
    [SerializeField] private Animator chestAnimator;
    [SerializeField] private Camera endingCamera;
    [Tooltip("Animator 0번 레이어에 있는 열림 상태의 전체 이름")]
    [SerializeField] private string openState = "Base Layer.Open";
    [SerializeField, Min(0.5f)] private float interactDistance = 3f;
    [Header("상자와 함께 날아갈 연출용 모델")]
    [SerializeField] private Transform launchGroup;
    [SerializeField] private GameObject knightVisual;
    [SerializeField] private GameObject rangerVisual;
    [SerializeField, Min(0.1f)] private float openingSeconds = 2.2f;
    [SerializeField, Min(0f)] private float openHold = 0.25f;
    [SerializeField, Min(0.1f)] private float launchSeconds = 1.1f;
    [SerializeField, Min(1f)] private float launchHeight = 22f;

    public bool HasStarted { get; private set; }
    public double ClearTimeOnNetwork { get; private set; }
    public bool SkyReady { get; private set; }

    private int openHash;
    private Vector3 groupOrigin, groupScale;
    private Quaternion cameraFront, cameraSky;
    private bool openFinished;
    private bool accepted;
    private Round1Player localPlayer;
    private Camera activeCamera;
    private Transform cameraOriginalParent;
    private Vector3 cameraOriginalLocalPosition;
    private Quaternion cameraOriginalLocalRotation;
    private float cameraOriginalFov;
    private float oldSpeed;
    private AnimatorCullingMode oldCulling;
    private bool endingCameraInitiallyActive;
    private readonly List<Renderer> hidden = new List<Renderer>();
    private readonly List<Behaviour> paused = new List<Behaviour>();
    private Rigidbody frozenBody;

    private void Awake()
    {
        // Inspector 연결 오류가 있어 Start가 중단되어도 연출용 복제 모델은 숨긴다.
        if (knightVisual) knightVisual.SetActive(false);
        if (rangerVisual) rangerVisual.SetActive(false);
        if (endingCamera)
        {
            endingCameraInitiallyActive = endingCamera.gameObject.activeSelf;
            endingCamera.enabled = false;
        }
    }

    private void Start()
    {
        openHash = Animator.StringToHash(openState);
        if (!chestRoot || !chestCollider || !chestAnimator || !endingCamera ||
            !launchGroup || !knightVisual || !rangerVisual ||
            !chestCollider.transform.IsChildOf(chestRoot) ||
            !chestRoot.IsChildOf(launchGroup) ||
            launchGroup == chestRoot || knightVisual == rangerVisual ||
            knightVisual.transform.parent != launchGroup || rangerVisual.transform.parent != launchGroup ||
            endingCamera.transform.IsChildOf(launchGroup) || transform.IsChildOf(launchGroup) ||
            !chestAnimator.runtimeAnimatorController || !chestAnimator.HasState(0, openHash))
        {
            Debug.LogError("Round2Ending: 상자/카메라/LaunchGroup/모델 연결과 열림 상태 이름을 확인하세요. " + openState, this);
            enabled = false;
            return;
        }
        oldSpeed = chestAnimator.speed;
        oldCulling = chestAnimator.cullingMode;
        groupOrigin = launchGroup.position;
        groupScale = launchGroup.localScale;
        cameraFront = endingCamera.transform.rotation;
        cameraSky = Quaternion.FromToRotation(endingCamera.transform.forward, Vector3.up) * cameraFront;
    }

    private void Update()
    {
        if (HasStarted)
        {
            if (!SkyReady) AdvanceEnding();
            return;
        }
        if (!PhotonNetwork.InRoom || Round1PauseMenu.InputBlocked ||
            !Input.GetKeyDown(KeyCode.F))
            return;

        if (!localPlayer)
        {
            foreach (Round1Player player in FindObjectsOfType<Round1Player>())
                if (player.photonView.IsMine) { localPlayer = player; break; }
        }
        if (!localPlayer || !InRange(localPlayer, interactDistance)) return;
        photonView.RPC(nameof(RequestEnding), RpcTarget.MasterClient,
            localPlayer.photonView.ViewID);
    }

    private bool InRange(Round1Player player, float range)
    {
        Vector3 position = player.transform.position;
        return (chestCollider.ClosestPoint(position) - position).sqrMagnitude <= range * range;
    }

    [PunRPC]
    private void RequestEnding(int playerViewId, PhotonMessageInfo info)
    {
        if (!enabled || !PhotonNetwork.IsMasterClient || accepted || HasStarted ||
            PhotonNetwork.CurrentRoom.PlayerCount != 2) return;
        PhotonView view = PhotonView.Find(playerViewId);
        if (!view || info.Sender == null || view.OwnerActorNr != info.Sender.ActorNumber) return;
        Round1Player player = view.GetComponent<Round1Player>();
        if (!player || !InRange(player, interactDistance + 0.75f)) return;

        accepted = true;
        photonView.RPC(nameof(BeginEnding), RpcTarget.AllViaServer, PhotonNetwork.Time);
    }

    [PunRPC]
    private void BeginEnding(double clearTime, PhotonMessageInfo info)
    {
        if (!enabled || HasStarted || info.Sender != PhotonNetwork.MasterClient) return;
        foreach (Round1Player player in FindObjectsOfType<Round1Player>())
        {
            if (!player.photonView.IsMine) continue;
            foreach (Camera cam in player.GetComponentsInChildren<Camera>(true))
            {
                if (!cam.isActiveAndEnabled) continue;
                activeCamera = cam;
                break;
            }
            break;
        }
        if (!activeCamera)
        {
            Debug.LogError("Round2Ending: 현재 화면을 그리는 플레이어 카메라를 찾지 못했습니다.", this);
            return;
        }
        cameraOriginalParent = activeCamera.transform.parent;
        cameraOriginalLocalPosition = activeCamera.transform.localPosition;
        cameraOriginalLocalRotation = activeCamera.transform.localRotation;
        cameraOriginalFov = activeCamera.fieldOfView;
        activeCamera.transform.SetParent(null, true);
        activeCamera.transform.SetPositionAndRotation(endingCamera.transform.position, cameraFront);
        activeCamera.fieldOfView = endingCamera.fieldOfView;
        activeCamera.targetDisplay = 0;
        activeCamera.targetTexture = null;
        HasStarted = true;
        ClearTimeOnNetwork = clearTime;
        foreach (Round1Player player in FindObjectsOfType<Round1Player>())
        {
            foreach (Renderer visual in player.GetComponentsInChildren<Renderer>(true))
            {
                if (visual.forceRenderingOff) continue;
                visual.forceRenderingOff = true;
                hidden.Add(visual);
            }
            if (!player.photonView.IsMine) continue;
            var ladder = player.GetComponent<PlayerLadderClimb>();
            if (ladder) { ladder.CancelClimb(); Pause(ladder); }
            Pause(player);
            Rigidbody body = player.GetComponent<Rigidbody>();
            if (body && !body.isKinematic)
            {
                body.velocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.isKinematic = true;
                frozenBody = body;
            }
        }
        knightVisual.SetActive(true);
        rangerVisual.SetActive(true);
        chestAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        chestAnimator.speed = 1f;
        chestAnimator.Play(openHash, 0, 0f);
        chestAnimator.Update(0f);
        float stateLength = chestAnimator.GetCurrentAnimatorStateInfo(0).length;
        if (stateLength > 0.01f)
            chestAnimator.speed = stateLength / Mathf.Max(0.1f, openingSeconds);
        AdvanceEnding();
    }

    private void AdvanceEnding()
    {
        // 두 화면에서 같은 시각에 뚜껑이 다 열리고, 같은 시각에 튀어 오른다.
        float elapsed = (float)(PhotonNetwork.Time - ClearTimeOnNetwork);
        if (elapsed < -2147483f) elapsed += 4294967.296f;
        float openTime = Mathf.Max(0.1f, openingSeconds);
        if (!openFinished && elapsed >= openTime)
        {
            chestAnimator.speed = 0f;
            openFinished = true;
        }

        float flight = Mathf.Clamp01((elapsed - openTime - openHold) / Mathf.Max(0.1f, launchSeconds));
        if (flight <= 0f) return;

        // 처음에 빠르게 솟구치고 멀어지며 작아진다.
        float height = launchHeight * flight * (2f - flight);
        launchGroup.position = groupOrigin + Vector3.up * height;
        float vanish = Mathf.InverseLerp(0.65f, 1f, flight);
        launchGroup.localScale = groupScale * (1f - Mathf.SmoothStep(0f, 1f, vanish));

        Vector3 sight = launchGroup.position - activeCamera.transform.position;
        Vector3 right = cameraFront * Vector3.right;
        Vector3 cameraUp = Vector3.Cross(sight, right);
        Quaternion tracking = cameraUp.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(sight, cameraUp) : cameraSky;
        Quaternion aim = Quaternion.Slerp(cameraFront, tracking, Mathf.Clamp01(flight / 0.18f));
        activeCamera.transform.rotation = Quaternion.Slerp(aim, cameraSky, vanish);

        if (flight >= 1f)
        {
            knightVisual.SetActive(false);
            rangerVisual.SetActive(false);
            launchGroup.gameObject.SetActive(false);
            SkyReady = true;
        }
    }

    private void Pause(Behaviour component)
    {
        if (!component.enabled) return;
        component.enabled = false;
        paused.Add(component);
    }

    private void OnDestroy()
    {
        // 재시작의 씬 교체 중에는 기존 엔딩 오브젝트를 복원하지 않는다.
    }
}
