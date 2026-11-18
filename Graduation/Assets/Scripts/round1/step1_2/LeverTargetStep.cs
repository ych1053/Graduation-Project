using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

[RequireComponent(typeof(PhotonView))]
public class LeverTargetStep : MonoBehaviourPunCallbacks
{
    [SerializeField] private int stepId = 1;
    [SerializeField] private LeverTargetStep previousStep;

    private bool IsUnlocked =>
        previousStep == null || previousStep.IsComplete;

    [SerializeField] private Transform leverPoint;
    [SerializeField] private Transform targetSpawnPoint;
    [SerializeField] private Transform targetPeakPoint;
    [SerializeField] private Rigidbody targetBody;
    [SerializeField] private Transform platform;

    [SerializeField] private float interactionDistance = 2.5f;
    [SerializeField] private float riseDuration = 0.6f;
    [SerializeField] private float fallSpeed = 1.8f;

    // Platform 부모 기준으로 벽 안쪽으로 숨길 방향과 거리
    [SerializeField]
    private Vector3 hiddenOffset =
        new Vector3(0f, 0f, -2f);

    [SerializeField] private float platformSpeed = 2f;

    // 0: 레버 대기, 1: 표적 활성, 2: 완료
    private int state;
    private double activatedAt;

    private Vector3 extendedPosition;
    private Vector3 hiddenPosition;

    private Round1Player localPlayer;
    private bool initialized;

    private string StateKey => "g2_step_" + stepId + "_state";
    private string TimeKey => "g2_step_" + stepId + "_time";

    public bool IsComplete => state == 2;

    private void Start()
    {
        if (leverPoint == null ||
            targetSpawnPoint == null ||
            targetPeakPoint == null ||
            targetBody == null ||
            platform == null)
        {
            Debug.LogError(
                "LeverTargetStep의 연결 항목을 확인하세요.",
                this
            );
            enabled = false;
            return;
        }

        extendedPosition = platform.localPosition;
        hiddenPosition = extendedPosition + hiddenOffset;

        targetBody.isKinematic = true;
        targetBody.useGravity = false;

        initialized = true;

        ReadRoomState();

        platform.localPosition = IsComplete
            ? extendedPosition
            : hiddenPosition;
    }

    private void Update()
    {
        if (!initialized) return;

        Vector3 destination = IsComplete
            ? extendedPosition
            : hiddenPosition;

        platform.localPosition = Vector3.MoveTowards(
            platform.localPosition,
            destination,
            platformSpeed * Time.deltaTime
        );

        if (!PhotonNetwork.InRoom ||
            state != 0 ||
            !IsUnlocked ||
            Round1PauseMenu.InputBlocked)
            return;

        if (localPlayer == null)
        {
            foreach (Round1Player player in
                     FindObjectsOfType<Round1Player>())
            {
                if (player.photonView.IsMine)
                {
                    localPlayer = player;
                    break;
                }
            }
        }

        if (localPlayer == null) return;

        float distance = Vector3.Distance(
            localPlayer.transform.position,
            leverPoint.position
        );

        if (distance <= interactionDistance &&
            Input.GetKeyDown(KeyCode.E))
        {
            photonView.RPC(
                nameof(RequestActivate),
                RpcTarget.MasterClient
            );
        }
    }

    private void FixedUpdate()
    {
        if (!initialized || state != 1) return;

        targetBody.MovePosition(GetTargetPosition());
    }

    private Vector3 GetTargetPosition()
    {
        float elapsed = Mathf.Max(
            0f,
            (float)(PhotonNetwork.Time - activatedAt)
        );

        float duration = Mathf.Max(0.05f, riseDuration);

        if (elapsed < duration)
        {
            return Vector3.Lerp(
                targetSpawnPoint.position,
                targetPeakPoint.position,
                elapsed / duration
            );
        }

        return targetPeakPoint.position +
               Vector3.down * fallSpeed * (elapsed - duration);
    }

    [PunRPC]
    private void RequestActivate(PhotonMessageInfo info)
    {
        if (!initialized ||
            !PhotonNetwork.IsMasterClient ||
            state != 0 ||
            !IsUnlocked)
            return;

        Round1Player requestingPlayer = null;

        foreach (Round1Player player in
                 FindObjectsOfType<Round1Player>())
        {
            if (player.photonView.OwnerActorNr ==
                info.Sender.ActorNumber)
            {
                requestingPlayer = player;
                break;
            }
        }

        if (requestingPlayer == null) return;

        if (Vector3.Distance(
                requestingPlayer.transform.position,
                leverPoint.position
            ) > interactionDistance + 0.8f)
            return;

        PublishState(1, PhotonNetwork.Time);
    }

    public void TryHit(ConsumableBox box)
    {
        if (!initialized ||
            !PhotonNetwork.IsMasterClient ||
            state != 1 ||
            box == null ||
            !box.HasBeenThrown ||
            box.IsConsumed)
            return;

        NetworkCarryBox carryBox =
            box.GetComponent<NetworkCarryBox>();

        if (carryBox == null || carryBox.HolderActorNumber != 0)
            return;

        // 먼저 완료 처리해서 여러 충돌이 발생해도 한 번만 성공
        PublishState(2, activatedAt);

        // 소모형 상자는 이 호출로 소유 해제 후 사라짐
        carryBox.RespawnOnMaster();
    }

    public void TryFailAtDeathZone()
    {
        if (!initialized ||
            !PhotonNetwork.IsMasterClient ||
            state != 1)
            return;

        // 아래에서 올라오는 도중 데스존을 통과할 때는 실패하지 않음
        if (PhotonNetwork.Time - activatedAt <
            Mathf.Max(0.05f, riseDuration))
            return;

        PublishState(0, 0d);
    }

    private void PublishState(int newState, double startTime)
    {
        // 방장은 즉시 반영해 중복 작동을 막음
        ApplyState(newState, startTime);

        PhotonNetwork.CurrentRoom.SetCustomProperties(
            new Hashtable
            {
                { StateKey, newState },
                { TimeKey, startTime }
            }
        );
    }

    private void ApplyState(int newState, double startTime)
    {
        state = newState;
        activatedAt = startTime;

        bool showTarget = state == 1;

        if (showTarget)
        {
            Vector3 position = GetTargetPosition();
            targetBody.position = position;
            targetBody.transform.position = position;
        }

        targetBody.gameObject.SetActive(showTarget);
    }

    private void ReadRoomState()
    {
        int savedState = 0;
        double savedTime = 0d;

        if (PhotonNetwork.InRoom)
        {
            Hashtable properties =
                PhotonNetwork.CurrentRoom.CustomProperties;

            if (properties.TryGetValue(StateKey, out object s))
                savedState = (int)s;

            if (properties.TryGetValue(TimeKey, out object t))
                savedTime = (double)t;
        }

        ApplyState(savedState, savedTime);
    }

    public override void OnRoomPropertiesUpdate(
        Hashtable changedProperties
    )
    {
        if (!initialized) return;

        if (changedProperties.ContainsKey(StateKey) ||
            changedProperties.ContainsKey(TimeKey))
        {
            ReadRoomState();
        }
    }
}