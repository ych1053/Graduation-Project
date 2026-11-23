using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

// KnightPlayer / RangerPlayer의 PhotonView가 있는 루트에 추가한다.
[DisallowMultipleComponent]
[RequireComponent(typeof(PhotonView), typeof(Round1Player))]
[DefaultExecutionOrder(150)]
public sealed class PlayerFootstepAudio : MonoBehaviourPun
{
    [Header("라운드별 짧은 발소리 클립 (각각 1개 이상)")]
    [FormerlySerializedAs("footstepClips")]
    [SerializeField] private AudioClip[] round1Clips;
    [SerializeField] private AudioClip[] round2Clips;

    private AudioClip[] footstepClips =>
        SceneManager.GetActiveScene().name == "Round_2" ? round2Clips : round1Clips;

    [Header("발걸음과 음량")]
    [SerializeField, Min(0.1f)] private float distancePerStep = 1.5f;
    [SerializeField, Range(0f, 1f)] private float volume = 0.5f;
    [SerializeField] private Vector2 pitchRange = new Vector2(0.95f, 1.05f);
    [SerializeField, Min(0.05f)] private float minimumInterval = 0.15f;

    [Header("상대 플레이어 발소리 거리")]
    [SerializeField, Min(0.1f)] private float minDistance = 2f;
    [SerializeField, Min(0.1f)] private float maxDistance = 18f;

    private Round1Player player;
    private AudioSource source;
    private Vector3 previousPosition;
    private float travelled;
    private float lastMovementAt;
    private float nextStepAt;
    private int lastClipIndex = -1;

    private void Awake()
    {
        player = GetComponent<Round1Player>();
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.volume = volume;
        source.dopplerLevel = 0f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = minDistance;
        source.maxDistance = Mathf.Max(minDistance, maxDistance);
    }

    private void OnEnable()
    {
        previousPosition = transform.position;
        travelled = 0f;
        lastMovementAt = Time.time;
        nextStepAt = Time.time + minimumInterval;
    }

    private void Start()
    {
        if (footstepClips == null || footstepClips.Length == 0)
            Debug.LogWarning("현재 씬 " + SceneManager.GetActiveScene().name +
                "에 해당하는 Round1 Clips 또는 Round2 Clips를 연결하세요.", this);
    }

    private void LateUpdate()
    {
        Vector3 delta = transform.position - previousPosition;
        previousPosition = transform.position;

        // 원격 캐릭터는 거리로 다시 판정하지 않고 소유자가 보낸 이벤트만 재생한다.
        if (!photonView.IsMine) return;

        if (!player.isActiveAndEnabled || !player.CanPlayFootsteps ||
            Time.deltaTime <= 0f || delta.magnitude > 2f)
        {
            travelled = 0f;
            return;
        }

        delta.y = 0f;
        float distance = delta.magnitude;
        if (distance > 0.001f)
        {
            lastMovementAt = Time.time;
            travelled += distance;
        }
        else
        {
            // 물리 업데이트 사이의 짧은 정지는 유지한다.
            if (Time.time - lastMovementAt > 0.12f)
                travelled = 0f;
            return;
        }

        if (travelled < distancePerStep || Time.time < nextStepAt) return;

        travelled = 0f;
        nextStepAt = Time.time + minimumInterval;
        int index = ChooseClip();
        if (index < 0) return;

        float pitch = Random.Range(
            Mathf.Clamp(Mathf.Min(pitchRange.x, pitchRange.y), 0.5f, 2f),
            Mathf.Clamp(Mathf.Max(pitchRange.x, pitchRange.y), 0.5f, 2f));

        PlayStep(index, pitch);
        photonView.RPC(nameof(ReceiveFootstep), RpcTarget.Others, index, pitch);
    }

    private int ChooseClip()
    {
        if (footstepClips == null || footstepClips.Length == 0) return -1;

        // 비어 있는 칸과, 가능한 경우 직전 클립을 제외한다.
        int candidates = 0;
        for (int i = 0; i < footstepClips.Length; i++)
            if (footstepClips[i] != null && i != lastClipIndex) candidates++;

        if (candidates == 0)
            return lastClipIndex >= 0 && lastClipIndex < footstepClips.Length &&
                footstepClips[lastClipIndex] != null ? lastClipIndex : -1;

        int selected = Random.Range(0, candidates);
        for (int i = 0; i < footstepClips.Length; i++)
        {
            if (footstepClips[i] == null || i == lastClipIndex) continue;
            if (selected-- == 0)
            {
                lastClipIndex = i;
                return i;
            }
        }
        return -1;
    }

    [PunRPC]
    private void ReceiveFootstep(int clipIndex, float pitch, PhotonMessageInfo info)
    {
        if (info.Sender == null || info.Sender.ActorNumber != photonView.OwnerActorNr)
            return;
        PlayStep(clipIndex, pitch);
    }

    private void PlayStep(int clipIndex, float pitch)
    {
        if (!isActiveAndEnabled || !player.isActiveAndEnabled ||
            footstepClips == null || clipIndex < 0 || clipIndex >= footstepClips.Length ||
            footstepClips[clipIndex] == null) return;

        // 내 발소리는 카메라 거리에 관계없이, 상대 발소리는 거리감 있게 들린다.
        source.spatialBlend = photonView.IsMine ? 0f : 1f;
        source.pitch = Mathf.Clamp(pitch, 0.5f, 2f);
        source.volume = volume;
        source.PlayOneShot(footstepClips[clipIndex]);
    }

    private void OnDisable()
    {
        travelled = 0f;
        if (source != null) source.Stop();
    }

    private void OnDestroy()
    {
        if (source != null) Destroy(source);
    }
}
