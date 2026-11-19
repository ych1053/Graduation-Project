using Photon.Pun;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Round1Player : MonoBehaviourPun, IPunObservable
{
    [SerializeField] private Camera playerCamera;
    [SerializeField] private float walkSpeed = 4f;
    [SerializeField] private float runSpeed = 7f;
    [SerializeField] private float jumpForce = 6f;
    [SerializeField] private float mouseSensitivity = 3f;

    private PlayerLadderClimb ladderClimb;
    private Rigidbody body;

    private Vector2 moveInput;
    private bool isRunning;
    private bool jumpRequested;
    private bool isGrounded;

    private Vector3 networkPosition;
    private Quaternion networkRotation;

    private Vector3 spawnPosition;
    private Quaternion spawnRotation;

    private float nextBoxPushTime;

    private bool IsClimbing =>
        ladderClimb != null && ladderClimb.IsClimbing;

    private bool UsesSharedMovement
    {
        get
        {
            return Round2SharedMovement.Instance != null &&
                   Round2SharedMovement.Instance.IsSharedMode;
        }
    }

    private void Awake()
    {
        spawnPosition = transform.position;
        spawnRotation = transform.rotation;

        body = GetComponent<Rigidbody>();
        ladderClimb = GetComponent<PlayerLadderClimb>();

        if (playerCamera != null)
        {
            playerCamera.gameObject.SetActive(photonView.IsMine);
        }

        if (!photonView.IsMine)
        {
            body.isKinematic = true;
        }

        networkPosition = transform.position;
        networkRotation = transform.rotation;
    }

    private void Update()
    {
        // 상대 캐릭터는 받은 위치와 회전으로 표시한다.
        if (!photonView.IsMine)
        {
            transform.position = Vector3.Lerp(
                transform.position,
                networkPosition,
                Time.deltaTime * 12f
            );

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                networkRotation,
                Time.deltaTime * 12f
            );

            return;
        }

        bool inputBlocked =
            Round1PauseMenu.InputBlocked || IsClimbing;

        // 메뉴나 사다리 때문에 조작할 수 없을 때도
        // 상대에게 입력 0을 전달한다.
        if (UsesSharedMovement)
        {
            Round2SharedMovement.Instance.UpdateLocalInput(
                photonView,
                !inputBlocked
            );
        }

        if (inputBlocked)
        {
            moveInput = Vector2.zero;
            jumpRequested = false;
            isRunning = false;
            return;
        }

        if (UsesSharedMovement)
        {
            // 두 플레이어의 담당 입력을 합쳐 사용한다.
            moveInput =
                Round2SharedMovement.Instance.GetSharedInput();

            // 공유 구간에서는 두 캐릭터의 속도를 맞춘다.
            isRunning = false;
        }
        else
        {
            // Round_1과 일반 조작 구간의 기존 입력.
            moveInput = new Vector2(
                Input.GetAxisRaw("Horizontal"),
                Input.GetAxisRaw("Vertical")
            ).normalized;

            isRunning =
                Input.GetKey(KeyCode.LeftShift) ||
                Input.GetKey(KeyCode.RightShift);
        }

        // 점프는 기존처럼 각자 조작한다.
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            jumpRequested = true;
        }

        // 우클릭 시점 조작도 각자 유지한다.
        if (Input.GetMouseButton(1))
        {
            float turn =
                Input.GetAxis("Mouse X") * mouseSensitivity;

            transform.Rotate(0f, turn, 0f);
        }
    }

    private void FixedUpdate()
    {
        if (!photonView.IsMine)
            return;

        if (Round1PauseMenu.InputBlocked || IsClimbing)
            return;

        Vector3 direction = GetMovementDirection();

        float speed;

        if (UsesSharedMovement)
        {
            speed =
                Round2SharedMovement.Instance.SharedWalkSpeed;
        }
        else
        {
            speed = isRunning ? runSpeed : walkSpeed;
        }

        body.MovePosition(
            body.position +
            direction * speed * Time.fixedDeltaTime
        );

        if (jumpRequested)
        {
            body.AddForce(
                Vector3.up * jumpForce,
                ForceMode.Impulse
            );

            isGrounded = false;
            jumpRequested = false;
        }
    }

    private Vector3 GetMovementDirection()
    {
        return transform.right * moveInput.x +
               transform.forward * moveInput.y;
    }

    [PunRPC]
    public void ReceiveSharedMoveInput(
        float axis,
        int attemptNumber,
        PhotonMessageInfo info)
    {
        if (info.Sender == null ||
            info.Sender.ActorNumber != photonView.OwnerActorNr)
        {
            return;
        }

        if (Round2SharedMovement.Instance == null)
            return;

        Round2SharedMovement.Instance.ReceiveInput(
            info.Sender.ActorNumber,
            axis,
            attemptNumber
        );
    }

    private void OnCollisionStay(Collision collision)
    {
        if (!photonView.IsMine)
            return;

        // 방장이 아닌 플레이어의 상자 밀기.
        if (!Round1PauseMenu.InputBlocked &&
            !IsClimbing &&
            !PhotonNetwork.IsMasterClient &&
            Time.time >= nextBoxPushTime &&
            moveInput.sqrMagnitude > 0.01f)
        {
            NetworkCarryBox box =
                collision.collider
                    .GetComponentInParent<NetworkCarryBox>();

            if (box != null && box.HolderActorNumber == 0)
            {
                box.AskToPush(GetMovementDirection());

                nextBoxPushTime = Time.time + 0.15f;
            }
        }

        foreach (ContactPoint contact in collision.contacts)
        {
            if (contact.normal.y > 0.5f)
            {
                isGrounded = true;
                return;
            }
        }
    }

    private void OnCollisionExit(Collision collision)
    {
        if (photonView.IsMine)
            isGrounded = false;
    }

    public void RespawnAtStart()
    {
        if (!photonView.IsMine)
            return;

        // 사다리 상태를 먼저 해제하고 기존 Rigidbody 설정을 복원한다.
        if (ladderClimb != null)
        {
            ladderClimb.CancelClimb();
        }

        // 공유 조작 중이었다면 내 담당 입력을 일단 0으로 전달한다.
        if (UsesSharedMovement)
        {
            Round2SharedMovement.Instance.UpdateLocalInput(
                photonView,
                false
            );
        }

        if (!body.isKinematic)
        {
            body.velocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        body.position = spawnPosition;
        body.rotation = spawnRotation;

        transform.SetPositionAndRotation(
            spawnPosition,
            spawnRotation
        );

        moveInput = Vector2.zero;
        jumpRequested = false;
        isRunning = false;
        isGrounded = false;

        nextBoxPushTime = Time.time + 0.2f;

        photonView.RPC(
            nameof(SnapToSpawn),
            RpcTarget.Others,
            spawnPosition,
            spawnRotation
        );
    }



    [PunRPC]
    private void SnapToSpawn(
        Vector3 position,
        Quaternion rotation)
    {
        if (photonView.IsMine)
            return;

        networkPosition = position;
        networkRotation = rotation;

        transform.SetPositionAndRotation(position, rotation);
    }

    public void OnPhotonSerializeView(
        PhotonStream stream,
        PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(transform.position);
            stream.SendNext(transform.rotation);
        }
        else
        {
            networkPosition =
                (Vector3)stream.ReceiveNext();

            networkRotation =
                (Quaternion)stream.ReceiveNext();
        }
    }
}