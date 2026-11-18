using Photon.Pun;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class Round2SharedMovement : MonoBehaviourPunCallbacks
{
    public static Round2SharedMovement Instance { get; private set; }

    [Header("Round_2 스폰 관리자")]
    [SerializeField] private Round2Spawner spawner;

    [Header("각 플레이어의 도착 영역")]
    [SerializeField] private Round2ArrivalZone player1ArrivalZone;
    [SerializeField] private Round2ArrivalZone player2ArrivalZone;

    [Header("공유 이동 설정")]
    [SerializeField] private float sharedWalkSpeed = 4f;

    [Header("입력 동기화")]
    [SerializeField] private float sendInterval = 0.1f;
    [SerializeField] private float inputTimeout = 0.5f;

    public bool IsSharedMode { get; private set; } = true;
    public float SharedWalkSpeed => sharedWalkSpeed;

    private const string CompletedRunKey =
        "r2_sharedMoveCompletedRun";

    private string runToken;

    private float player1Axis;
    private float player2Axis;

    private float player1ReceivedAt = -100f;
    private float player2ReceivedAt = -100f;

    private float nextSendTime;
    private float lastSentAxis = float.NaN;

    private float nextArrivalCheckTime;
    private bool completionRequested;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError(
                "Round2SharedMovement는 씬에 하나만 있어야 합니다."
            );

            enabled = false;
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        if (spawner == null)
        {
            Debug.LogError(
                "Round2SharedMovement의 Spawner를 연결하세요."
            );
        }

        if (player1ArrivalZone == null ||
            player2ArrivalZone == null)
        {
            Debug.LogError(
                "두 플레이어의 Arrival Zone을 연결하세요."
            );
        }

        if (!PhotonNetwork.InRoom)
            return;

        // 현재 게임 실행을 구분하는 기존 값을 사용한다.
        runToken =
            PhotonNetwork.CurrentRoom.CustomProperties[
                Round1Reload.RunKey
            ] as string ?? "initial";

        ApplyCompletionState();
    }

    private void FixedUpdate()
    {
        if (!PhotonNetwork.InRoom ||
            !PhotonNetwork.IsMasterClient ||
            !IsSharedMode ||
            completionRequested ||
            string.IsNullOrEmpty(runToken))
        {
            return;
        }

        if (spawner == null ||
            player1ArrivalZone == null ||
            player2ArrivalZone == null)
        {
            return;
        }

        if (PhotonNetwork.CurrentRoom.PlayerCount != 2 ||
            spawner.Player1ActorNumber <= 0 ||
            spawner.Player2ActorNumber <= 0)
        {
            return;
        }

        if (Time.unscaledTime < nextArrivalCheckTime)
            return;

        nextArrivalCheckTime = Time.unscaledTime + 0.1f;

        // 방장 화면에서 각자 지정된 도착 영역을 검사한다.
        bool player1Arrived =
            player1ArrivalZone.ContainsPlayer(
                spawner.Player1ActorNumber
            );

        bool player2Arrived =
            player2ArrivalZone.ContainsPlayer(
                spawner.Player2ActorNumber
            );

        if (!player1Arrived || !player2Arrived)
            return;

        // 완료 상태를 방에 저장하여 양쪽에 전달한다.
        completionRequested =
            PhotonNetwork.CurrentRoom.SetCustomProperties(
                new Hashtable
                {
                    { CompletedRunKey, runToken }
                }
            );
    }

    public void UpdateLocalInput(
        PhotonView playerView,
        bool canInput)
    {
        if (!IsSharedMode ||
            !PhotonNetwork.InRoom ||
            spawner == null ||
            playerView == null ||
            !playerView.IsMine ||
            spawner.LocalPlayerSlot == 0)
        {
            return;
        }

        float axis = 0f;

        if (canInput && Application.isFocused)
        {
            if (spawner.LocalPlayerSlot == 1)
            {
                if (Input.GetKey(KeyCode.W))
                    axis += 1f;

                if (Input.GetKey(KeyCode.S))
                    axis -= 1f;
            }
            else
            {
                if (Input.GetKey(KeyCode.D))
                    axis += 1f;

                if (Input.GetKey(KeyCode.A))
                    axis -= 1f;
            }
        }

        ReceiveInput(playerView.OwnerActorNr, axis);

        if (axis != lastSentAxis ||
            Time.unscaledTime >= nextSendTime)
        {
            playerView.RPC(
                nameof(Round1Player.ReceiveSharedMoveInput),
                RpcTarget.Others,
                axis
            );

            lastSentAxis = axis;

            nextSendTime =
                Time.unscaledTime +
                Mathf.Max(0.02f, sendInterval);
        }
    }

    public void ReceiveInput(int actorNumber, float axis)
    {
        if (!IsSharedMode || spawner == null)
            return;

        if (float.IsNaN(axis) || float.IsInfinity(axis))
            return;

        axis = Mathf.Clamp(axis, -1f, 1f);

        if (actorNumber == spawner.Player1ActorNumber)
        {
            player1Axis = axis;
            player1ReceivedAt = Time.unscaledTime;
        }
        else if (actorNumber == spawner.Player2ActorNumber)
        {
            player2Axis = axis;
            player2ReceivedAt = Time.unscaledTime;
        }
    }

    public Vector2 GetSharedInput()
    {
        if (!IsSharedMode ||
            !PhotonNetwork.InRoom ||
            PhotonNetwork.CurrentRoom.PlayerCount != 2)
        {
            return Vector2.zero;
        }

        float now = Time.unscaledTime;

        float horizontal =
            now - player2ReceivedAt <= inputTimeout
                ? player2Axis
                : 0f;

        float vertical =
            now - player1ReceivedAt <= inputTimeout
                ? player1Axis
                : 0f;

        return Vector2.ClampMagnitude(
            new Vector2(horizontal, vertical),
            1f
        );
    }

    public override void OnRoomPropertiesUpdate(
        Hashtable propertiesThatChanged)
    {
        if (propertiesThatChanged.ContainsKey(CompletedRunKey))
        {
            ApplyCompletionState();
        }
    }

    private void ApplyCompletionState()
    {
        if (!PhotonNetwork.InRoom ||
            string.IsNullOrEmpty(runToken) ||
            !IsSharedMode)
        {
            return;
        }

        if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(
                CompletedRunKey,
                out object value))
        {
            return;
        }

        if (!(value is string completedToken) ||
            completedToken != runToken)
        {
            return;
        }

        IsSharedMode = false;

        player1Axis = 0f;
        player2Axis = 0f;
        player1ReceivedAt = -100f;
        player2ReceivedAt = -100f;

        Debug.Log(
            "[Round2] 두 플레이어 도착 완료! " +
            "공유 조작을 해제하고 각자 WASD 조작으로 전환합니다."
        );
    }

    public override void OnMasterClientSwitched(
        Photon.Realtime.Player newMasterClient)
    {
        completionRequested = false;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}