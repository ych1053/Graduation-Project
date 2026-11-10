using Photon.Pun;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]

public class Round1Player : MonoBehaviourPun, IPunObservable
{
    //추가함

    public void RespawnAtStart()
    {
        if (!photonView.IsMine) return;

        body.velocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.position = spawnPosition;
        body.rotation = spawnRotation;

        transform.SetPositionAndRotation(spawnPosition, spawnRotation);
        moveInput = Vector2.zero;
        jumpRequested = false;
        isGrounded = false;

        photonView.RPC(
            nameof(SnapToSpawn),
            RpcTarget.Others,
            spawnPosition,
            spawnRotation
        );
    }

    [PunRPC]
    private void SnapToSpawn(Vector3 position, Quaternion rotation)
    {
        if (photonView.IsMine) return;

        networkPosition = position;
        networkRotation = rotation;
        transform.SetPositionAndRotation(position, rotation);
    }//여기까지추가
    [SerializeField] private Camera playerCamera;
    [SerializeField] private float walkSpeed = 4f;
    [SerializeField] private float runSpeed = 7f;
    [SerializeField] private float jumpForce = 6f;
    [SerializeField] private float mouseSensitivity = 3f;

    private Rigidbody body;
    private Vector2 moveInput;
    private bool isRunning;
    private bool jumpRequested;
    private bool isGrounded;

    private Vector3 networkPosition;
    private Quaternion networkRotation;

    private void Awake()
    {
        spawnPosition = transform.position;
        spawnRotation = transform.rotation;

        body = GetComponent<Rigidbody>();

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
        if (!photonView.IsMine)
        {
            transform.position = Vector3.Lerp(
                transform.position, networkPosition, Time.deltaTime * 12f
            );
            transform.rotation = Quaternion.Slerp(
                transform.rotation, networkRotation, Time.deltaTime * 12f
            );
            return;
        }

        moveInput = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical")
        ).normalized;

        isRunning = Input.GetKey(KeyCode.LeftShift) ||
                    Input.GetKey(KeyCode.RightShift);

        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            jumpRequested = true;
        }

        // 우클릭한 동안 마우스를 좌우로 움직여 방향 전환
        if (Input.GetMouseButton(1))
        {
            float turn = Input.GetAxis("Mouse X") * mouseSensitivity;
            transform.Rotate(0f, turn, 0f);
        }
    }

    private void FixedUpdate()
    {
        if (!photonView.IsMine) return;

        Vector3 direction =
            transform.right * moveInput.x +
            transform.forward * moveInput.y;

        float speed = isRunning ? runSpeed : walkSpeed;
        body.MovePosition(body.position + direction * speed * Time.fixedDeltaTime);

        if (jumpRequested)
        {
            body.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            isGrounded = false;
            jumpRequested = false;
        }
    }
    private float nextBoxPushTime;//여기도 추가 플레이어2힘정보->플레이어1 

    private void OnCollisionStay(Collision collision)
    {
        if (!photonView.IsMine) return;
        // 방장이 아닌 플레이어가 상자를 밀면 방장에게 힘 적용을 요청
        if (!PhotonNetwork.IsMasterClient &&
            Time.time >= nextBoxPushTime &&
            moveInput.sqrMagnitude > 0.01f)
        {
            NetworkCarryBox box =
                collision.collider.GetComponentInParent<NetworkCarryBox>();

            if (box != null && box.HolderActorNumber == 0)
            {
                Vector3 pushDirection =
                    transform.right * moveInput.x +
                    transform.forward * moveInput.y;

                box.AskToPush(pushDirection);
                nextBoxPushTime = Time.time + 0.15f;
            }
        }//여기까지
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

    public void OnPhotonSerializeView(
        PhotonStream stream,
        PhotonMessageInfo info
    )
    {
        if (stream.IsWriting)
        {
            stream.SendNext(transform.position);
            stream.SendNext(transform.rotation);
        }
        else
        {
            networkPosition = (Vector3)stream.ReceiveNext();
            networkRotation = (Quaternion)stream.ReceiveNext();
        }
    }//여기부터 데스존
    private Vector3 spawnPosition;
    private Quaternion spawnRotation;

}