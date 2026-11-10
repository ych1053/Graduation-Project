using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(PhotonView))]
public class NetworkCarryBox : MonoBehaviourPunCallbacks, IPunObservable
{
    //여기부터추가
    // 상자를 들고 있는 플레이어가 물에 빠졌을 때 호출
    public void AskToResetIfHeld()
    {
        photonView.RPC(nameof(RequestResetIfHeld), RpcTarget.MasterClient);
    }

    [PunRPC]
    private void RequestResetIfHeld(PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (holderActorNumber != info.Sender.ActorNumber) return;

        RespawnOnMaster();
    }

    // 상자 자체가 데스존에 닿았을 때 방장이 호출
    public void RespawnOnMaster()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        photonView.RPC(
            nameof(ResetBox),
            RpcTarget.All,
            spawnPosition,
            spawnRotation
        );
    }

    [PunRPC]
    private void ResetBox(Vector3 position, Quaternion rotation)
    {
        holderActorNumber = 0;

        // 원격 클라이언트의 상자는 계속 물리 계산을 하지 않음
        body.isKinematic = !PhotonNetwork.IsMasterClient;

        body.position = position;
        body.rotation = rotation;
        transform.SetPositionAndRotation(position, rotation);

        body.velocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;

        networkPosition = position;
        networkRotation = rotation;
    }//여기까지
    [SerializeField] private Vector3 carryOffset = new Vector3(0f, 1.2f, 1.6f);
    [SerializeField] private float pickupRange = 2.3f;
    [SerializeField] private float pushImpulse = 0.35f;
    //여기도 추가 플레이어2를 방장에 보냄
    public void AskToPush(Vector3 direction)
    {
        photonView.RPC(
            nameof(RequestPush),
            RpcTarget.MasterClient,
            direction
        );
    }

    [PunRPC]
    private void RequestPush(Vector3 direction, PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient ||
            holderActorNumber != 0 ||
            body.isKinematic)
            return;

        Round1Player player = FindPlayer(info.Sender.ActorNumber);
        if (player == null) return;

        if (Vector3.Distance(player.transform.position, body.position) > 2.8f)
            return;

        direction.y = 0f;
        if (direction.sqrMagnitude < 0.01f) return;

        body.AddForce(
            direction.normalized * pushImpulse,
            ForceMode.Impulse
        );
    }
    //여기까지
    private Rigidbody body;
    private int holderActorNumber;
    private Vector3 networkPosition;
    private Quaternion networkRotation;

    private readonly List<Collider> ignoredBoxColliders = new List<Collider>();
    private readonly List<Collider> ignoredPlayerColliders = new List<Collider>();
    private Coroutine restoreCollisionRoutine;

    public int HolderActorNumber => holderActorNumber;

    private void Start()
    {
        spawnPosition = transform.position;
        spawnRotation = transform.rotation;//추가됨

        body = GetComponent<Rigidbody>();
        networkPosition = transform.position;
        networkRotation = transform.rotation;

        if (!PhotonNetwork.IsMasterClient)
            body.isKinematic = true;
    }

    private void Update()
    {
        if (PhotonNetwork.IsMasterClient) return;

        if (holderActorNumber != 0)
        {
            Round1Player holder = FindPlayer(holderActorNumber);

            if (holder != null)
                transform.position = holder.transform.TransformPoint(carryOffset);
        }
        else
        {
            transform.position = Vector3.Lerp(
                transform.position, networkPosition, Time.deltaTime * 12f
            );

            transform.rotation = Quaternion.Slerp(
                transform.rotation, networkRotation, Time.deltaTime * 12f
            );
        }
    }

    private void FixedUpdate()
    {
        if (!PhotonNetwork.IsMasterClient || holderActorNumber == 0)
            return;

        Round1Player holder = FindPlayer(holderActorNumber);

        if (holder != null)
            body.MovePosition(holder.transform.TransformPoint(carryOffset));
    }

    public void AskToPickUp()
    {
        photonView.RPC(nameof(RequestPickUp), RpcTarget.MasterClient);
    }

    public void AskToThrow(Vector3 direction, float force)
    {
        photonView.RPC(
            nameof(RequestThrow),
            RpcTarget.MasterClient,
            direction,
            force
        );
    }

    [PunRPC]
    private void RequestPickUp(PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient || holderActorNumber != 0)
            return;

        Round1Player player = FindPlayer(info.Sender.ActorNumber);
        if (player == null) return;

        if (Vector3.Distance(player.transform.position, transform.position)
            > pickupRange + 0.8f)
            return;

        body.velocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.isKinematic = true;

        photonView.RPC(
            nameof(SetHolder),
            RpcTarget.All,
            info.Sender.ActorNumber
        );
    }

    [PunRPC]
    private void RequestThrow(
        Vector3 direction,
        float force,
        PhotonMessageInfo info
    )
    {
        if (!PhotonNetwork.IsMasterClient ||
            holderActorNumber != info.Sender.ActorNumber)
            return;

        Round1Player holder = FindPlayer(holderActorNumber);
        if (holder == null) return;

        body.position = holder.transform.TransformPoint(carryOffset);
        body.isKinematic = false;
        body.velocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;

        photonView.RPC(nameof(SetHolder), RpcTarget.All, 0);
        body.AddForce(direction.normalized * force, ForceMode.Impulse);
    }

    [PunRPC]
    private void SetHolder(int actorNumber)
    {
        ApplyHolder(actorNumber);
    }

    private void ApplyHolder(int actorNumber)
    {
        if (holderActorNumber == actorNumber) return;

        holderActorNumber = actorNumber;

        if (actorNumber == 0)
        {
            // 던진 직후 상자가 플레이어와 겹쳐 있는 동안 충돌을 유지하지 않음
            restoreCollisionRoutine = StartCoroutine(
                RestoreCollisionWhenSeparated()
            );
        }
        else
        {
            if (restoreCollisionRoutine != null)
                StopCoroutine(restoreCollisionRoutine);

            RestoreIgnoredCollisions();

            Round1Player holder = FindPlayer(actorNumber);
            if (holder != null)
                IgnoreCollisionWith(holder);
        }
    }

    private void IgnoreCollisionWith(Round1Player player)
    {
        Collider[] boxColliders = GetComponentsInChildren<Collider>();
        Collider[] playerColliders =
            player.GetComponentsInChildren<Collider>();

        foreach (Collider boxCollider in boxColliders)
        {
            foreach (Collider playerCollider in playerColliders)
            {
                Physics.IgnoreCollision(
                    boxCollider, playerCollider, true
                );

                ignoredBoxColliders.Add(boxCollider);
                ignoredPlayerColliders.Add(playerCollider);
            }
        }
    }

    private IEnumerator RestoreCollisionWhenSeparated()
    {
        yield return new WaitForSeconds(0.3f);

        // 상자가 플레이어 몸에서 벗어날 때까지 기다림
        while (holderActorNumber == 0 && IsStillOverlapping())
            yield return null;

        if (holderActorNumber == 0)
            RestoreIgnoredCollisions();

        restoreCollisionRoutine = null;
    }

    private bool IsStillOverlapping()
    {
        for (int i = 0; i < ignoredBoxColliders.Count; i++)
        {
            Collider boxCollider = ignoredBoxColliders[i];
            Collider playerCollider = ignoredPlayerColliders[i];

            if (boxCollider != null &&
                playerCollider != null &&
                boxCollider.bounds.Intersects(playerCollider.bounds))
                return true;
        }

        return false;
    }

    private void RestoreIgnoredCollisions()
    {
        for (int i = 0; i < ignoredBoxColliders.Count; i++)
        {
            Collider boxCollider = ignoredBoxColliders[i];
            Collider playerCollider = ignoredPlayerColliders[i];

            if (boxCollider != null && playerCollider != null)
            {
                Physics.IgnoreCollision(
                    boxCollider, playerCollider, false
                );
            }
        }

        ignoredBoxColliders.Clear();
        ignoredPlayerColliders.Clear();
    }

    private Round1Player FindPlayer(int actorNumber)
    {
        Round1Player[] players = FindObjectsOfType<Round1Player>();

        foreach (Round1Player player in players)
        {
            if (player.photonView.OwnerActorNr == actorNumber)
                return player;
        }

        return null;
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (!PhotonNetwork.IsMasterClient ||
            holderActorNumber != otherPlayer.ActorNumber)
            return;

        body.isKinematic = false;
        photonView.RPC(nameof(SetHolder), RpcTarget.All, 0);
    }

    public void OnPhotonSerializeView(
        PhotonStream stream,
        PhotonMessageInfo info
    )
    {
        if (stream.IsWriting)
        {
            stream.SendNext(transform.position);
            stream.SendNext(transform.rotation);
            stream.SendNext(holderActorNumber);
        }
        else
        {
            networkPosition = (Vector3)stream.ReceiveNext();
            networkRotation = (Quaternion)stream.ReceiveNext();

            int receivedHolder = (int)stream.ReceiveNext();
            ApplyHolder(receivedHolder);
        }
    }
    private Vector3 spawnPosition;
    private Quaternion spawnRotation;//추가됨
}