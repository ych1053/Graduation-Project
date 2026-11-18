using System.Collections;
using Photon.Pun;
using UnityEngine;

[RequireComponent(typeof(PhotonView))]
public class RoundExitInteract : MonoBehaviourPun
{
    [SerializeField] private LeverTargetStep lastStep;
    [SerializeField] private string nextSceneName = "Round_2";
    [SerializeField] private float interactionDistance = 3f;

    [SerializeField] private Animator chestAnimator;
    [SerializeField] private string openStateName = "Open";
    [SerializeField] private float sceneMoveDelay = 1.5f;

    private Round1Player localPlayer;
    private bool isOpening;

    // None이면 바로 사용 가능
    private bool CanUse =>
        lastStep == null || lastStep.IsComplete;

    private void Awake()
    {
        PhotonNetwork.AutomaticallySyncScene = true;

        if (chestAnimator == null)
            chestAnimator = GetComponentInChildren<Animator>(true);
    }

    private void Update()
    {
        if (!PhotonNetwork.InRoom ||
            isOpening ||
            Round1PauseMenu.InputBlocked ||
            !CanUse)
            return;

        if (localPlayer == null)
        {
            localPlayer = FindPlayer(
                PhotonNetwork.LocalPlayer.ActorNumber
            );
        }

        if (localPlayer == null) return;

        float distance = Vector3.Distance(
            localPlayer.transform.position,
            transform.position
        );

        if (distance <= interactionDistance &&
            Input.GetKeyDown(KeyCode.E))
        {
            photonView.RPC(
                nameof(RequestOpen),
                RpcTarget.MasterClient
            );
        }
    }

    private Round1Player FindPlayer(int actorNumber)
    {
        foreach (Round1Player player in
                 FindObjectsOfType<Round1Player>())
        {
            if (player.photonView.OwnerActorNr == actorNumber)
                return player;
        }

        return null;
    }

    [PunRPC]
    private void RequestOpen(PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient ||
            isOpening ||
            Round1PauseMenu.IsRestarting ||
            !CanUse)
            return;

        Round1Player player = FindPlayer(info.Sender.ActorNumber);
        if (player == null) return;

        if (Vector3.Distance(
                player.transform.position,
                transform.position
            ) > interactionDistance + 0.8f)
            return;

        if (!Application.CanStreamedLevelBeLoaded(nextSceneName))
        {
            Debug.LogError(
                nextSceneName +
                " 씬을 Build Settings에 추가하세요.",
                this
            );
            return;
        }

        // 연속 요청 방지
        isOpening = true;

        // 양쪽 화면에서 같은 열림 동작 실행
        photonView.RPC(nameof(PlayOpen), RpcTarget.All);
    }

    [PunRPC]
    private void PlayOpen(PhotonMessageInfo info)
    {
        if (!info.Sender.IsMasterClient) return;

        isOpening = true;

        if (chestAnimator != null &&
            chestAnimator.runtimeAnimatorController != null)
        {
            int stateHash = Animator.StringToHash(openStateName);

            if (chestAnimator.HasState(0, stateHash))
            {
                chestAnimator.Play(stateHash, 0, 0f);
            }
            else
            {
                Debug.LogWarning(
                    "상자의 Open State Name을 확인하세요: " +
                    openStateName,
                    this
                );
            }
        }

        // 애니메이션 설정 여부와 관계없이 씬 이동은 진행
        if (PhotonNetwork.IsMasterClient)
            StartCoroutine(MoveToNextScene());
    }

    private IEnumerator MoveToNextScene()
    {
        yield return new WaitForSecondsRealtime(
            Mathf.Max(0f, sceneMoveDelay)
        );

        if (!PhotonNetwork.InRoom ||
            !PhotonNetwork.IsMasterClient ||
            Round1PauseMenu.IsRestarting)
            yield break;

        PhotonNetwork.DestroyAll();
        PhotonNetwork.LoadLevel(nextSceneName);
    }
}