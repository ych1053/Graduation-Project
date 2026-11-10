using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

[RequireComponent(typeof(PhotonView))]
[RequireComponent(typeof(BoxCollider))]
public class WaterPathButton : MonoBehaviourPunCallbacks
{
    private const string PathRaisedKey = "round1PathRaised";

    [SerializeField] private Transform risingPath;
    [SerializeField] private float riseHeight = 2f;
    [SerializeField] private float moveSpeed = 2f;

    private BoxCollider buttonTrigger;

    // 각 플레이어가 자기 화면에서 밟고 있는지 확인
    private readonly HashSet<Collider> localPlayerColliders =
        new HashSet<Collider>();

    // 방장이 관리하는 버튼 위 플레이어 목록
    private readonly HashSet<int> pressingActors =
        new HashSet<int>();

    private Vector3 loweredPosition;
    private Vector3 raisedPosition;
    private bool boxOnButton;
    private bool shouldRise;

    private void Start()
    {
        buttonTrigger = GetComponent<BoxCollider>();

        if (risingPath == null)
        {
            Debug.LogError("Rising Path가 연결되지 않았습니다.", this);
            enabled = false;
            return;
        }

        loweredPosition = risingPath.localPosition;
        raisedPosition = loweredPosition + Vector3.up * riseHeight;

        if (PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient)
            SetRoomPathState(false);
    }

    private void Update()
    {
        if (risingPath == null) return;

        Vector3 target = shouldRise
            ? raisedPosition
            : loweredPosition;

        risingPath.localPosition = Vector3.MoveTowards(
            risingPath.localPosition,
            target,
            moveSpeed * Time.deltaTime
        );
    }

    private void FixedUpdate()
    {
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient)
            return;

        // 방장의 물리 화면에서 버튼 영역 안의 상자를 검사
        Bounds area = buttonTrigger.bounds;
        Collider[] touching = Physics.OverlapBox(
            area.center,
            area.extents,
            Quaternion.identity
        );

        bool foundBox = false;

        foreach (Collider hit in touching)
        {
            NetworkCarryBox box =
                hit.GetComponentInParent<NetworkCarryBox>();

            // 손에 들린 상자는 버튼을 누르지 않음
            if (box != null && box.HolderActorNumber == 0)
            {
                foundBox = true;
                break;
            }
        }

        if (boxOnButton != foundBox)
        {
            boxOnButton = foundBox;
            UpdatePathState();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        PhotonView playerView =
            other.GetComponentInParent<PhotonView>();

        if (playerView == null ||
            !playerView.IsMine ||
            !playerView.CompareTag("Player"))
            return;

        localPlayerColliders.Add(other);

        if (localPlayerColliders.Count == 1)
        {
            photonView.RPC(
                nameof(RequestButtonState),
                RpcTarget.MasterClient,
                true
            );
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!localPlayerColliders.Remove(other))
            return;

        if (localPlayerColliders.Count == 0)
        {
            photonView.RPC(
                nameof(RequestButtonState),
                RpcTarget.MasterClient,
                false
            );
        }
    }

    [PunRPC]
    private void RequestButtonState(
        bool pressed,
        PhotonMessageInfo info
    )
    {
        if (!PhotonNetwork.IsMasterClient) return;

        int actorNumber = info.Sender.ActorNumber;

        if (pressed)
            pressingActors.Add(actorNumber);
        else
            pressingActors.Remove(actorNumber);

        UpdatePathState();
    }

    private void UpdatePathState()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        bool raised = pressingActors.Count > 0 || boxOnButton;
        SetRoomPathState(raised);
    }

    private void SetRoomPathState(bool raised)
    {
        Hashtable properties = new Hashtable
        {
            { PathRaisedKey, raised }
        };

        PhotonNetwork.CurrentRoom.SetCustomProperties(properties);
    }

    public override void OnRoomPropertiesUpdate(
        Hashtable changedProperties
    )
    {
        if (changedProperties.TryGetValue(
            PathRaisedKey, out object value))
        {
            shouldRise = (bool)value;
        }
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        if (pressingActors.Remove(otherPlayer.ActorNumber))
            UpdatePathState();
    }
}