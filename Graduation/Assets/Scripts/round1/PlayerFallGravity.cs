using Photon.Pun;
using UnityEngine;

// KnightPlayer / RangerPlayer의 Rigidbody와 PhotonView가 있는 루트에 추가한다.
// 기본 중력은 Rigidbody가 적용하므로 하강 중 부족한 배수만 추가한다.
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody), typeof(PhotonView), typeof(Round1Player))]
public sealed class PlayerFallGravity : MonoBehaviourPun
{
    [SerializeField, Range(1f, 5f)] private float fallGravityMultiplier = 2.5f;

    private Rigidbody body;
    private Round1Player player;
    private PlayerLadderClimb ladder;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        player = GetComponent<Round1Player>();
        ladder = GetComponent<PlayerLadderClimb>();
    }

    private void FixedUpdate()
    {
        if (!photonView.IsMine || !player.isActiveAndEnabled ||
            body.isKinematic || !body.useGravity ||
            (ladder != null && ladder.IsClimbing))
            return;

        // 상승 중에는 기존 점프를 유지한다. 지면 정지 상태에도 추가하지 않는다.
        if (body.velocity.y >= -0.05f) return;

        float extraMultiplier = Mathf.Max(0f, fallGravityMultiplier - 1f);
        body.AddForce(Physics.gravity * extraMultiplier, ForceMode.Acceleration);
    }
}
