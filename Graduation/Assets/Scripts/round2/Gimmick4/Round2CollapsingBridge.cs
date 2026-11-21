using System;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class Round2CollapsingBridge : MonoBehaviourPunCallbacks
{
    [Serializable]
    private class BridgePiece
    {
        public Transform bridgeVisual;
        public Transform walkSurface;

        // 각 조각의 원래 상태를 저장해 공동 리스폰 때 복구한다.
        [NonSerialized] public Vector3 originalPosition;
        [NonSerialized] public Quaternion originalRotation;
        [NonSerialized] public Collider[] floorColliders;
        [NonSerialized] public bool[] floorEnabled;
        [NonSerialized] public Renderer[] visualRenderers;
        [NonSerialized] public bool[] rendererEnabled;
        [NonSerialized] public bool floorVisible = true;
        [NonSerialized] public bool meshVisible = true;
    }

    [Header("연결")]
    [SerializeField] private BoxCollider startTrigger;
    [Tooltip("먼저 무너질 입구 쪽 조각부터 순서대로 연결하세요.")]
    [SerializeField] private BridgePiece[] bridgePieces = new BridgePiece[0];

    // 이전 단일 다리 설정도 배열을 연결하기 전까지 사용할 수 있다.
    [SerializeField, HideInInspector] private Transform bridgeVisual;
    [SerializeField, HideInInspector] private Transform walkSurface;

    [Header("시간")]
    [Tooltip("입구 통과 후 첫 번째 조각이 무너지기까지의 시간")]
    [SerializeField, Min(0.1f)] private float shakeDuration = 5f;
    [Tooltip("다음 조각이 무너지기 시작하는 시간 간격")]
    [SerializeField, Min(0.05f)] private float collapseInterval = 0.6f;
    [SerializeField, Min(0.1f)] private float fallDuration = 1.2f;

    [Header("흔들림과 낙하")]
    [SerializeField] private Vector3 shakeAxis = Vector3.right;
    [SerializeField, Min(0f)] private float shakeDistance = 0.12f;
    [SerializeField, Min(0f)] private float shakeFrequency = 3f;
    [SerializeField, Min(0f)] private float fallDistance = 8f;

    private const string StampKey = "r2_g4_bridge_stamp";
    private const string TimeKey = "r2_g4_bridge_time";
    private string attemptStamp;
    private bool initialized;
    private bool waitingForClear;
    private bool requestPending;
    private double requestedStart;
    private float nextCheckTime;
    private float retryTime;

    private void Start()
    {
        if (bridgePieces == null || bridgePieces.Length == 0)
        {
            bridgePieces = new[]
            {
                new BridgePiece { bridgeVisual = bridgeVisual, walkSurface = walkSurface }
            };
        }

        if (!startTrigger)
        {
            Debug.LogError("Start Trigger를 연결하세요.", this);
            enabled = false;
            return;
        }

        // 모든 연결을 먼저 검사한다. 조각 사이에 외형이나 충돌판을 공유할 수 없다.
        var roots = new List<Transform>();
        for (int i = 0; i < bridgePieces.Length; i++)
        {
            BridgePiece piece = bridgePieces[i];
            if (piece == null || !piece.bridgeVisual || !piece.walkSurface ||
                !RegisterRoot(piece.bridgeVisual, roots) ||
                !RegisterRoot(piece.walkSurface, roots))
            {
                Debug.LogError("Bridge Pieces의 Element " + i +
                    " 연결을 확인하세요. 각 Visual과 WalkSurface는 독립된 오브젝트여야 합니다.", this);
                enabled = false;
                return;
            }

            piece.originalPosition = piece.bridgeVisual.localPosition;
            piece.originalRotation = piece.bridgeVisual.localRotation;
            piece.floorColliders = piece.walkSurface.GetComponentsInChildren<Collider>(true);
            piece.floorEnabled = new bool[piece.floorColliders.Length];
            bool hasFloor = false;
            for (int j = 0; j < piece.floorColliders.Length; j++)
            {
                Collider floor = piece.floorColliders[j];
                piece.floorEnabled[j] = floor.enabled;
                hasFloor |= floor.enabled && !floor.isTrigger && floor.gameObject.activeInHierarchy;
            }
            if (!hasFloor)
            {
                Debug.LogError("Element " + i +
                    "의 Walk Surface에 활성화된 일반 Collider가 필요합니다.", this);
                enabled = false;
                return;
            }

            piece.visualRenderers = piece.bridgeVisual.GetComponentsInChildren<Renderer>(true);
            piece.rendererEnabled = new bool[piece.visualRenderers.Length];
            for (int j = 0; j < piece.visualRenderers.Length; j++)
                piece.rendererEnabled[j] = piece.visualRenderers[j].enabled;
        }

        foreach (BridgePiece piece in bridgePieces)
        {
            // 외형의 Collider를 끄고, 해당 조각의 WalkSurface로 걷기를 처리한다.
            foreach (Collider collider in piece.bridgeVisual.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;
        }

        initialized = true;
        RestoreBridge();
    }

    private bool RegisterRoot(Transform candidate, List<Transform> roots)
    {
        foreach (Transform root in roots)
        {
            if (candidate == root || candidate.IsChildOf(root) || root.IsChildOf(candidate))
                return false;
        }
        roots.Add(candidate);
        return true;
    }

    private void Update()
    {
        Round2SharedMovement shared = Round2SharedMovement.Instance;
        if (!initialized || !PhotonNetwork.InRoom || shared == null)
            return;

        var properties = PhotonNetwork.CurrentRoom.CustomProperties;
        properties.TryGetValue(Round1Reload.RunKey, out object runValue);
        string stamp = (runValue as string ?? "initial") + "|" + shared.CurrentAttempt;
        if (stamp != attemptStamp)
        {
            // 기존 공동 리스폰이 재도전 번호를 올리면 다리를 복구한다.
            waitingForClear = attemptStamp != null;
            attemptStamp = stamp;
            requestPending = false;
            nextCheckTime = 0f;
            RestoreBridge();
        }

        bool started = properties.TryGetValue(StampKey, out object savedStamp) &&
                       savedStamp is string saved && saved == attemptStamp;
        if (started && properties.TryGetValue(TimeKey, out object time) && time is double start)
        {
            requestPending = false;
            AnimateBridge((float)System.Math.Max(0d, PhotonNetwork.Time - start));
            return;
        }

        if (!PhotonNetwork.IsMasterClient || Time.unscaledTime < nextCheckTime)
            return;
        nextCheckTime = Time.unscaledTime + 0.05f;

        bool playerInside = ContainsPlayer();
        if (waitingForClear)
        {
            if (!playerInside)
                waitingForClear = false;
            return;
        }
        if (shared.IsSharedMode || PhotonNetwork.CurrentRoom.PlayerCount != 2)
            return;

        if (!requestPending)
        {
            if (!playerInside)
                return;
            requestPending = true;
            requestedStart = PhotonNetwork.Time;
            retryTime = 0f;
        }
        if (Time.unscaledTime < retryTime)
            return;

        // 재전송해도 최초 시작 시각을 유지한다.
        retryTime = Time.unscaledTime + 2f;
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable
        {
            { StampKey, attemptStamp },
            { TimeKey, requestedStart }
        });
    }

    private bool ContainsPlayer()
    {
        if (!startTrigger.enabled || !startTrigger.gameObject.activeInHierarchy)
            return false;
        Transform zone = startTrigger.transform;
        Vector3 scale = zone.lossyScale;
        scale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        Vector3 halfSize = Vector3.Scale(startTrigger.size * 0.5f, scale);
        Collider[] hits = Physics.OverlapBox(zone.TransformPoint(startTrigger.center),
            halfSize, zone.rotation, ~0, QueryTriggerInteraction.Ignore);
        foreach (Collider hit in hits)
        {
            Round1Player player = hit.GetComponentInParent<Round1Player>();
            if (player != null && player.photonView.Owner != null &&
                PhotonNetwork.CurrentRoom.Players.ContainsKey(player.photonView.OwnerActorNr))
                return true;
        }
        return false;
    }

    private void AnimateBridge(float elapsed)
    {
        for (int i = 0; i < bridgePieces.Length; i++)
        {
            BridgePiece piece = bridgePieces[i];
            float collapseTime = Mathf.Max(0.1f, shakeDuration) +
                                 i * Mathf.Max(0.05f, collapseInterval);

            if (elapsed < collapseTime)
            {
                float strength = Mathf.Lerp(0.3f, 1f, elapsed / collapseTime);
                float offset = Mathf.Sin(elapsed * shakeFrequency * Mathf.PI * 2f);
                piece.bridgeVisual.localPosition = piece.originalPosition +
                    shakeAxis.normalized * (offset * shakeDistance * strength);
                continue;
            }

            // 해당 조각이 무너지는 순간에만 그 조각의 충돌판을 끈다.
            SetFloor(piece, false);
            float progress = Mathf.Clamp01((elapsed - collapseTime) / Mathf.Max(0.1f, fallDuration));
            Vector3 localDrop = Vector3.down * (fallDistance * progress * progress);
            if (piece.bridgeVisual.parent != null)
                localDrop = piece.bridgeVisual.parent.InverseTransformVector(localDrop);
            piece.bridgeVisual.localPosition = piece.originalPosition + localDrop;
            if (progress >= 1f)
                SetMesh(piece, false);
        }
    }

    private void RestoreBridge()
    {
        foreach (BridgePiece piece in bridgePieces)
        {
            piece.bridgeVisual.localPosition = piece.originalPosition;
            piece.bridgeVisual.localRotation = piece.originalRotation;
            SetFloor(piece, true);
            SetMesh(piece, true);
        }
    }

    private void SetFloor(BridgePiece piece, bool visible)
    {
        if (piece.floorVisible == visible)
            return;
        piece.floorVisible = visible;
        for (int i = 0; i < piece.floorColliders.Length; i++)
            piece.floorColliders[i].enabled = visible && piece.floorEnabled[i];
    }

    private void SetMesh(BridgePiece piece, bool visible)
    {
        if (piece.meshVisible == visible)
            return;
        piece.meshVisible = visible;
        for (int i = 0; i < piece.visualRenderers.Length; i++)
            piece.visualRenderers[i].enabled = visible && piece.rendererEnabled[i];
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        requestPending = false;
    }
}
