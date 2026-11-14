using Photon.Pun;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(PhotonView))]
public class PlayerLadderClimb : MonoBehaviourPun
{
    [SerializeField] private float climbSpeed = 2.5f;
    [SerializeField] private float attachDistance = 1f;

    public bool IsClimbing { get; private set; }

    private Rigidbody body;
    private LadderZone nearbyLadder;
    private LadderZone activeLadder;

    private float progress;
    private float ladderLength;
    private float reattachTime;

    private bool savedUseGravity;
    private bool savedIsKinematic;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
    }

    public void SetNearbyLadder(LadderZone ladder)
    {
        nearbyLadder = ladder;
    }

    public void ClearNearbyLadder(LadderZone ladder)
    {
        if (nearbyLadder == ladder)
            nearbyLadder = null;
    }

    private void Update()
    {
        if (!photonView.IsMine) return;

        if (IsClimbing && Input.GetKeyDown(KeyCode.Space))
            CancelClimb();
    }

    private void FixedUpdate()
    {
        if (!photonView.IsMine) return;

        if (!IsClimbing)
        {
            if (Time.time >= reattachTime &&
                nearbyLadder != null &&
                nearbyLadder.IsReady &&
                Input.GetKey(KeyCode.W))
            {
                TryBeginClimb();
            }

            return;
        }

        if (activeLadder == null || !activeLadder.IsReady)
        {
            CancelClimb();
            return;
        }

        float input = 0f;

        if (Input.GetKey(KeyCode.W)) input += 1f;
        if (Input.GetKey(KeyCode.S)) input -= 1f;

        progress = Mathf.Clamp01(
            progress + input * climbSpeed *
            Time.fixedDeltaTime / ladderLength
        );

        Vector3 position = Vector3.Lerp(
            activeLadder.bottomPoint.position,
            activeLadder.topPoint.position,
            progress
        );

        body.MovePosition(position);

        if (input > 0f && progress >= 1f)
        {
            Vector3 exitPosition =
                activeLadder.exitPoint.position;

            CancelClimb();

            body.position = exitPosition;
            transform.position = exitPosition;
        }
        else if (input < 0f && progress <= 0f)
        {
            Vector3 bottomPosition =
                activeLadder.bottomPoint.position;

            CancelClimb();

            body.position = bottomPosition;
            transform.position = bottomPosition;
        }
    }

    private void TryBeginClimb()
    {
        Vector3 bottom = nearbyLadder.bottomPoint.position;
        Vector3 top = nearbyLadder.topPoint.position;
        Vector3 line = top - bottom;

        ladderLength = line.magnitude;
        if (ladderLength < 0.1f) return;

        float nearestProgress = Mathf.Clamp01(
            Vector3.Dot(body.position - bottom, line) /
            line.sqrMagnitude
        );

        Vector3 nearestPosition =
            Vector3.Lerp(bottom, top, nearestProgress);

        if (Vector3.Distance(body.position, nearestPosition)
            > attachDistance)
            return;

        savedUseGravity = body.useGravity;
        savedIsKinematic = body.isKinematic;

        body.velocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;

        body.useGravity = false;
        body.isKinematic = true;

        activeLadder = nearbyLadder;
        progress = nearestProgress;
        IsClimbing = true;

        body.position = nearestPosition;
        transform.position = nearestPosition;
    }

    public void CancelClimb()
    {
        if (!IsClimbing) return;

        IsClimbing = false;
        activeLadder = null;

        body.isKinematic = savedIsKinematic;
        body.useGravity = savedUseGravity;

        if (!body.isKinematic)
        {
            body.velocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        // 떨어진 직후 바로 다시 붙는 것을 방지
        reattachTime = Time.time + 0.5f;
    }

    private void OnDisable()
    {
        if (body != null && IsClimbing)
            CancelClimb();
    }
}