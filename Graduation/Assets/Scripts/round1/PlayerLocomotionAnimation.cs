using UnityEngine;
using Photon.Pun;

// KnightPlayer/RangerPlayer 루트에 붙인다. 원격 플레이어에서도 실행한다.
[DefaultExecutionOrder(100)]
public class PlayerLocomotionAnimation : MonoBehaviourPun
{
    [SerializeField] private Animator modelAnimator;
    [SerializeField, Min(0f)] private float smoothing = 0.12f;
    [SerializeField, Min(0f)] private float idleThreshold = 0.15f;
    [SerializeField, Min(8f)] private float teleportSpeed = 20f;
    private readonly int speedHash = Animator.StringToHash("Speed");
    private readonly int jumpHash = Animator.StringToHash("Jump");
    private readonly int holdingHash = Animator.StringToHash("Holding");
    private bool hasHoldingParameter;
    private float nextHoldingCheck;
    private Vector3 previousPosition;
    private float distanceSinceSample;
    private float sampleTime;
    private float targetSpeed;
    private Round1Player player;
    private PhotonView view;

    private void OnEnable() => previousPosition = transform.position;
    private void Start()
    {
        player = GetComponent<Round1Player>();
        view = GetComponent<PhotonView>();
        if (!modelAnimator) modelAnimator = GetComponentInChildren<Animator>();
        if (!modelAnimator || !modelAnimator.runtimeAnimatorController)
        { Debug.LogError("모델의 Animator와 Controller를 연결하세요.", this); enabled = false; return; }
        bool hasSpeed = false;
        foreach (var parameter in modelAnimator.parameters)
        {
            if (parameter.nameHash == speedHash && parameter.type == AnimatorControllerParameterType.Float) hasSpeed = true;
            if (parameter.nameHash == holdingHash && parameter.type == AnimatorControllerParameterType.Bool) hasHoldingParameter = true;
        }
        if (!hasSpeed)
        { Debug.LogError("Animator Controller에 Float 파라미터 Speed를 추가하세요.", this); enabled = false; return; }
        modelAnimator.applyRootMotion = false;
        modelAnimator.SetFloat(speedHash, 0);
    }
    private void LateUpdate()
    {
        if (hasHoldingParameter && Time.time >= nextHoldingCheck)
        {
            nextHoldingCheck = Time.time + 0.05f;
            bool holding = false;
            int actor = photonView.OwnerActorNr;
            foreach (NetworkCarryBox box in FindObjectsOfType<NetworkCarryBox>())
            {
                if (box.HolderActorNumber != actor) continue;
                holding = true;
                break;
            }
            modelAnimator.SetBool(holdingHash, holding);
        }
        if (view != null && view.IsMine && player != null)
        {
            modelAnimator.SetFloat(speedHash, player.AnimationSpeed, smoothing, Time.deltaTime);
            previousPosition = transform.position;
            return;
        }
        Vector3 delta = transform.position - previousPosition;
        previousPosition = transform.position;
        delta.y = 0;
        if (Time.deltaTime <= 0) return;
        if (delta.magnitude > teleportSpeed * Time.deltaTime)
        { distanceSinceSample = 0; sampleTime = 0; targetSpeed = 0; modelAnimator.SetFloat(speedHash, 0); return; }
        distanceSinceSample += delta.magnitude;
        sampleTime += Time.deltaTime;
        if (sampleTime >= 0.10f)
        {
            targetSpeed = distanceSinceSample / sampleTime;
            if (targetSpeed < idleThreshold) targetSpeed = 0;
            distanceSinceSample = 0;
            sampleTime = 0;
        }
        modelAnimator.SetFloat(speedHash, targetSpeed, smoothing, Time.deltaTime);
    }

    public void PlayJump()
    {
        if (!photonView.IsMine || modelAnimator == null) return;
        modelAnimator.SetTrigger(jumpHash);
        photonView.RPC(nameof(PlayJumpRemote), RpcTarget.Others);
    }

    [PunRPC]
    private void PlayJumpRemote()
    {
        if (modelAnimator != null) modelAnimator.SetTrigger(jumpHash);
    }
}
