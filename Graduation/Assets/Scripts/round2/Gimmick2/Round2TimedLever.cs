using Photon.Pun;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

[RequireComponent(typeof(PhotonView))]
public class Round2TimedLever : MonoBehaviourPunCallbacks
{
    [Header("레버 구분")]
    [SerializeField] private int platformId = 1;

    [Tooltip("이 레버를 사용할 플레이어 번호: 1 또는 2")]
    [Range(1, 2)]
    [SerializeField] private int allowedPlayerSlot = 2;

    [Header("연결")]
    [SerializeField] private Round2Spawner spawner;
    [SerializeField] private Rigidbody platformBody;

    [Header("상호작용")]
    [SerializeField] private float interactDistance = 2.5f;

    [Header("발판 이동")]
    [SerializeField] private float hiddenDepth = 4f;
    [SerializeField] private float riseDuration = 0.5f;
    [SerializeField] private float holdDuration = 5f;
    [SerializeField] private float lowerDuration = 0.5f;

    private Vector3 raisedPosition;
    private Vector3 loweredPosition;

    private string runToken;
    private string runKey;
    private string startKey;

    private bool initialized;
    private bool hasActivation;
    private bool requestPending;
    private double startedAt;

    private float TotalDuration =>
        riseDuration + holdDuration + lowerDuration;

    private bool IsBusy =>
        hasActivation &&
        PhotonNetwork.Time - startedAt < TotalDuration;

    private void Start()
    {
        if (spawner == null || platformBody == null)
        {
            Debug.LogError(
                name + ": Spawner와 Platform Body를 연결하세요."
            );

            return;
        }

        if (platformId <= 0)
        {
            Debug.LogError(
                name + ": Platform Id는 1 이상의 값으로 지정하세요."
            );

            return;
        }

        riseDuration = Mathf.Max(0.05f, riseDuration);
        lowerDuration = Mathf.Max(0.05f, lowerDuration);
        holdDuration = Mathf.Max(0f, holdDuration);
        hiddenDepth = Mathf.Max(0f, hiddenDepth);
        interactDistance = Mathf.Max(0.1f, interactDistance);

        // 에디터에서 배치한 위치를 올라온 위치로 기억한다.
        raisedPosition = platformBody.position;

        loweredPosition =
            raisedPosition + Vector3.down * hiddenDepth;

        platformBody.useGravity = false;
        platformBody.isKinematic = true;

        runKey = "r2_timed_" + platformId + "_run";
        startKey = "r2_timed_" + platformId + "_start";

        if (!PhotonNetwork.InRoom)
        {
            Debug.LogWarning(
                name + ": Photon 방을 통해 Round_2로 입장하세요."
            );

            platformBody.position = loweredPosition;
            return;
        }

        runToken =
            PhotonNetwork.CurrentRoom.CustomProperties[
                Round1Reload.RunKey
            ] as string ?? "initial";

        initialized = true;

        ReadNetworkState();

        platformBody.position = CalculatePosition();
    }

    private bool CanInteract(int actorNumber)
    {
        if (!initialized ||
            !isActiveAndEnabled ||
            !PhotonNetwork.InRoom ||
            PhotonNetwork.CurrentRoom.PlayerCount != 2)
        {
            return false;
        }

        // 첫 기믹의 공유 조작이 끝난 다음 사용할 수 있다.
        Round2SharedMovement shared =
            Round2SharedMovement.Instance;

        if (shared == null || shared.IsSharedMode)
            return false;

        int allowedActor =
            allowedPlayerSlot == 1
                ? spawner.Player1ActorNumber
                : spawner.Player2ActorNumber;

        return allowedActor > 0 && actorNumber == allowedActor;
    }

    private void Update()
    {
        if (!PhotonNetwork.InRoom ||
            Round1PauseMenu.InputBlocked ||
            !Input.GetKeyDown(KeyCode.F))
        {
            return;
        }

        int actorNumber =
            PhotonNetwork.LocalPlayer.ActorNumber;

        if (!CanInteract(actorNumber))
            return;

        Round1Player player = FindPlayer(actorNumber);

        if (player == null)
            return;

        // 여러 레버가 가까이 있어도 가장 가까운 하나만 선택한다.
        Round2TimedLever nearest =
            FindNearestLever(player.transform.position, actorNumber);

        if (nearest != this || IsBusy || requestPending)
            return;

        photonView.RPC(
            nameof(RequestActivate),
            RpcTarget.MasterClient
        );
    }

    private static Round1Player FindPlayer(int actorNumber)
    {
        Round1Player[] players =
            FindObjectsOfType<Round1Player>();

        foreach (Round1Player player in players)
        {
            if (player.photonView.OwnerActorNr == actorNumber)
                return player;
        }

        return null;
    }

    private static Round2TimedLever FindNearestLever(
        Vector3 playerPosition,
        int actorNumber)
    {
        Round2TimedLever nearest = null;
        float nearestDistance = float.PositiveInfinity;

        Round2TimedLever[] levers =
            FindObjectsOfType<Round2TimedLever>();

        foreach (Round2TimedLever lever in levers)
        {
            if (!lever.CanInteract(actorNumber))
                continue;

            float distance = Vector3.Distance(
                playerPosition,
                lever.transform.position
            );

            if (distance > lever.interactDistance)
                continue;

            bool closer = distance < nearestDistance;

            bool tied =
                distance == nearestDistance &&
                nearest != null &&
                lever.photonView.ViewID < nearest.photonView.ViewID;

            if (closer || tied)
            {
                nearest = lever;
                nearestDistance = distance;
            }
        }

        return nearest;
    }

    [PunRPC]
    private void RequestActivate(PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient ||
            info.Sender == null ||
            !CanInteract(info.Sender.ActorNumber) ||
            IsBusy ||
            requestPending)
        {
            return;
        }

        Round1Player player =
            FindPlayer(info.Sender.ActorNumber);

        if (player == null)
            return;

        float distance = Vector3.Distance(
            player.transform.position,
            transform.position
        );

        // 네트워크 보간에 따른 작은 위치 차이를 허용한다.
        if (distance > interactDistance + 0.5f)
            return;

        // 서버 응답 전 중복 작동을 막는다.
        requestPending = true;

        bool sent =
            PhotonNetwork.CurrentRoom.SetCustomProperties(
                new Hashtable
                {
                    { runKey, runToken },
                    { startKey, PhotonNetwork.Time }
                }
            );

        if (!sent)
            requestPending = false;
    }

    public override void OnRoomPropertiesUpdate(
        Hashtable propertiesThatChanged)
    {
        if (!initialized)
            return;

        if (propertiesThatChanged.ContainsKey(runKey) ||
            propertiesThatChanged.ContainsKey(startKey))
        {
            ReadNetworkState();
            requestPending = false;
        }
    }

    private void ReadNetworkState()
    {
        hasActivation = false;

        if (!PhotonNetwork.InRoom)
            return;

        Hashtable properties =
            PhotonNetwork.CurrentRoom.CustomProperties;

        if (!properties.TryGetValue(runKey, out object runValue) ||
            !(runValue is string savedRun) ||
            savedRun != runToken)
        {
            return;
        }

        if (!properties.TryGetValue(startKey, out object timeValue) ||
            !(timeValue is double startTime))
        {
            return;
        }

        startedAt = startTime;
        hasActivation = true;
    }

    private void FixedUpdate()
    {
        if (!initialized || platformBody == null)
            return;

        platformBody.MovePosition(CalculatePosition());
    }

    private Vector3 CalculatePosition()
    {
        if (!hasActivation)
            return loweredPosition;

        double elapsed = PhotonNetwork.Time - startedAt;

        if (elapsed < 0)
            return loweredPosition;

        // 상승
        if (elapsed < riseDuration)
        {
            float t = Mathf.Clamp01(
                (float)(elapsed / riseDuration)
            );

            t = Mathf.SmoothStep(0f, 1f, t);

            return Vector3.Lerp(
                loweredPosition,
                raisedPosition,
                t
            );
        }

        // 완전히 올라온 상태 유지
        if (elapsed < riseDuration + holdDuration)
            return raisedPosition;

        // 하강
        if (elapsed < TotalDuration)
        {
            float t = Mathf.Clamp01(
                (float)(
                    (elapsed - riseDuration - holdDuration) /
                    lowerDuration
                )
            );

            t = Mathf.SmoothStep(0f, 1f, t);

            return Vector3.Lerp(
                raisedPosition,
                loweredPosition,
                t
            );
        }

        return loweredPosition;
    }

    public override void OnMasterClientSwitched(
        Photon.Realtime.Player newMasterClient)
    {
        requestPending = false;
    }
}