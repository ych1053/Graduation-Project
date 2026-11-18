using Photon.Pun;
using UnityEngine;

[RequireComponent(typeof(PhotonView))]
public class TeamRespawnManager : MonoBehaviourPun
{
    public static TeamRespawnManager Instance { get; private set; }

    [Header("복귀 직후 중복 감지 방지")]
    [SerializeField] private float respawnCooldown = 0.75f;

    private float nextRequestTime;

    // 방장이 발행한 복귀 번호
    private int masterRespawnNumber;

    // 이 클라이언트가 처리한 마지막 복귀 번호
    private int lastAppliedRespawnNumber;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError(
                "TeamRespawnManager는 씬에 하나만 있어야 합니다."
            );

            enabled = false;
            return;
        }

        Instance = this;
    }

    public void RequestTeamRespawn(Round1Player fallenPlayer)
    {
        if (!isActiveAndEnabled ||
            !PhotonNetwork.InRoom ||
            fallenPlayer == null ||
            !fallenPlayer.photonView.IsMine)
        {
            return;
        }

        if (Time.unscaledTime < nextRequestTime)
            return;

        // 여러 Collider나 OnTriggerStay의 반복 요청을 제한한다.
        nextRequestTime = Time.unscaledTime + 0.25f;

        photonView.RPC(
            nameof(RequestRespawnOnMaster),
            RpcTarget.MasterClient,
            fallenPlayer.photonView.ViewID,
            lastAppliedRespawnNumber
        );
    }

    [PunRPC]
    private void RequestRespawnOnMaster(
        int fallenPlayerViewId,
        int knownRespawnNumber,
        PhotonMessageInfo info)
    {
        if (!isActiveAndEnabled ||
            !PhotonNetwork.InRoom ||
            !PhotonNetwork.IsMasterClient ||
            info.Sender == null)
        {
            return;
        }

        // 이미 처리 중이거나 처리한 복귀에 대한 중복 요청은 무시한다.
        if (knownRespawnNumber != masterRespawnNumber)
            return;

        PhotonView playerView =
            PhotonView.Find(fallenPlayerViewId);

        if (playerView == null ||
            playerView.OwnerActorNr != info.Sender.ActorNumber)
        {
            return;
        }

        Round1Player fallenPlayer =
            playerView.GetComponentInParent<Round1Player>();

        if (fallenPlayer == null)
            return;

        // 먼저 번호를 올려 동시에 들어온 다른 요청을 막는다.
        masterRespawnNumber++;

        // 함께 복귀할 두 플레이어의 소지 상자를 정리한다.
        NetworkCarryBox[] boxes =
            FindObjectsOfType<NetworkCarryBox>();

        foreach (NetworkCarryBox box in boxes)
        {
            if (box.HolderActorNumber != 0)
            {
                // 일반 상자는 복귀,
                // 소모 상자는 기존 ConsumableBox 규칙으로 처리.
                box.RespawnOnMaster();
            }
        }

        photonView.RPC(
            nameof(ApplyTeamRespawn),
            RpcTarget.AllViaServer,
            masterRespawnNumber
        );
    }

    [PunRPC]
    private void ApplyTeamRespawn(
        int respawnNumber,
        PhotonMessageInfo info)
    {
        if (!isActiveAndEnabled ||
            !PhotonNetwork.InRoom ||
            info.Sender == null ||
            !info.Sender.IsMasterClient)
        {
            return;
        }

        if (respawnNumber <= lastAppliedRespawnNumber)
            return;

        lastAppliedRespawnNumber = respawnNumber;

        // 이후 방장이 바뀌어도 번호를 이어서 사용할 수 있게 한다.
        masterRespawnNumber = Mathf.Max(
            masterRespawnNumber,
            respawnNumber
        );

        nextRequestTime =
            Time.unscaledTime + Mathf.Max(0.1f, respawnCooldown);

        Round1Player[] players =
            FindObjectsOfType<Round1Player>();

        foreach (Round1Player player in players)
        {
            // 각 클라이언트가 자기 캐릭터를 복귀시킨다.
            if (player.photonView.IsMine)
            {
                player.RespawnAtStart();
            }
        }

        Debug.Log(
            "[TeamRespawn] 두 플레이어를 시작 위치로 복귀시켰습니다."
        );
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}