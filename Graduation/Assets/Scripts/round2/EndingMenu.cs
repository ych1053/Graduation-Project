using System;
using System.Collections;
using Photon.Pun;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(PhotonView))]
public class EndingMenu : MonoBehaviourPun
{
    [SerializeField] private GlobalRanking ranking;
    [SerializeField] private GameObject buttonsPanel;
    [SerializeField] private Button replayButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private string replayScene = "EndingReplay";
    private bool shown;
    private bool loading;

    private void Awake()
    {
        PhotonNetwork.AutomaticallySyncScene = true;
        if (buttonsPanel) buttonsPanel.SetActive(false);
    }
    private void Start()
    {
        if (!ranking || !buttonsPanel || !replayButton || !quitButton)
        { Debug.LogError("EndingMenu의 네 항목을 연결하세요.", this); enabled = false; return; }
        replayButton.onClick.AddListener(Replay);
        quitButton.onClick.AddListener(Quit);
    }
    private void Update()
    {
        if (!shown && ranking.PresentationComplete)
        {
            shown = true;
            buttonsPanel.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
    private void Replay()
    {
        if (!shown || loading || !PhotonNetwork.InRoom) return;
        photonView.RPC(nameof(RequestReplay), RpcTarget.MasterClient);
    }
    [PunRPC]
    private void RequestReplay(PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient || loading || !ranking.PresentationComplete || info.Sender == null) return;
        if (PhotonNetwork.CurrentRoom.PlayerCount != 2)
        { Debug.LogWarning("다시하기에는 두 플레이어가 필요합니다."); return; }
        if (!Application.CanStreamedLevelBeLoaded(replayScene))
        { Debug.LogError("Build Settings에 EndingReplay 씬을 추가하세요."); return; }
        loading = true;
        replayButton.interactable = false;
        StartCoroutine(LoadReplayAfterToken());
    }
    private IEnumerator LoadReplayAfterToken()
    {
        string token = Guid.NewGuid().ToString("N");
        PhotonNetwork.CurrentRoom.SetCustomProperties(new ExitGames.Client.Photon.Hashtable {
            { "ending_replay", token }
        });
        float deadline = Time.realtimeSinceStartup + 10f;
        while (PhotonNetwork.InRoom && Time.realtimeSinceStartup < deadline &&
            (PhotonNetwork.CurrentRoom.CustomProperties["ending_replay"] as string) != token)
            yield return null;
        if (!PhotonNetwork.InRoom ||
            (PhotonNetwork.CurrentRoom.CustomProperties["ending_replay"] as string) != token)
        { Debug.LogError("다시하기 표식을 서버에 기록하지 못했습니다."); loading = false; replayButton.interactable = true; yield break; }
        Time.timeScale = 1f;
        PhotonNetwork.LoadLevel(replayScene);
    }
    private void Quit()
    {
        PhotonNetwork.Disconnect();
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
    private void OnDestroy()
    {
        if (replayButton) replayButton.onClick.RemoveListener(Replay);
        if (quitButton) quitButton.onClick.RemoveListener(Quit);
    }
}
