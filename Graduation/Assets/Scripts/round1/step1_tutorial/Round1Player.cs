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
    public bool CanPlayFootsteps =>
        isGrounded &&
        !jumpRequested &&
        !IsClimbing &&
        !Round1PauseMenu.InputBlocked;

    private Vector3 networkPosition;
    private Quaternion networkRotation;

    private Vector3 spawnPosition;
    private Quaternion spawnRotation;

    private float nextBoxPushTime;

    public float AnimationSpeed
    {
        get
        {
            if (Round1PauseMenu.InputBlocked || IsClimbing ||
                 moveInput.sqrMagnitude < 0.01f)
                return 0f;

            if (UsesSharedMovement)
                return Round2SharedMovement.Instance.SharedWalkSpeed;

            return isRunning ? runSpeed : walkSpeed;
        }
    }
    
    
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
        // ��� ĳ���ʹ� ���� ��ġ�� ȸ������ ǥ���Ѵ�.
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

        // �޴��� ��ٸ� ������ ������ �� ���� ����
        // ��뿡�� �Է� 0�� �����Ѵ�.
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
            // �� �÷��̾��� ��� �Է��� ���� ����Ѵ�.
            moveInput =
                Round2SharedMovement.Instance.GetSharedInput();

            // ���� ���������� �� ĳ������ �ӵ��� �����.
            isRunning = false;
        }
        else
        {
            // Round_1�� �Ϲ� ���� ������ ���� �Է�.
            moveInput = new Vector2(
                Input.GetAxisRaw("Horizontal"),
                Input.GetAxisRaw("Vertical")
            ).normalized;

            isRunning =
                Input.GetKey(KeyCode.LeftShift) ||
                Input.GetKey(KeyCode.RightShift);
        }

        // ������ ����ó�� ���� �����Ѵ�.
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            jumpRequested = true;
            PlayerLocomotionAnimation animation =
                GetComponent<PlayerLocomotionAnimation>();

            if (animation != null)
                animation.PlayJump();
        }

        // ��Ŭ�� ���� ���۵� ���� �����Ѵ�.
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
            PlayerLocomotionAnimation animation =
                GetComponent<PlayerLocomotionAnimation>();

           // if (animation != null)
                //animation.PlayJump();
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

        // ������ �ƴ� �÷��̾��� ���� �б�.
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

        // ��ٸ� ���¸� ���� �����ϰ� ���� Rigidbody ������ �����Ѵ�.
        if (ladderClimb != null)
        {
            ladderClimb.CancelClimb();
        }

        // ���� ���� ���̾��ٸ� �� ��� �Է��� �ϴ� 0���� �����Ѵ�.
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