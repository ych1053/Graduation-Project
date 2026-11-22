using System;
using System.Collections;
using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hashtable = ExitGames.Client.Photon.Hashtable;

[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(PhotonView))]
public class Round1PauseMenu : MonoBehaviourPunCallbacks
{
    [SerializeField] private GameObject pausePanel;
    [Header("Round_2에서만 켜세요")]
    [SerializeField] private bool useEndingReplay;
    [SerializeField] private Round2EndingChest ending;

    public static bool IsOpen { get; private set; }
    public static bool IsRestarting { get; private set; }
    private static float resumeInputAt;
    public static bool InputBlocked => IsOpen || IsRestarting || Time.unscaledTime < resumeInputAt || SessionChat.InputBlocked || ExitSmokeScreen.IsPlaying;

    private bool restartStarted;
    private bool requestPending;
    private float requestDeadline;
    private CursorLockMode savedCursorLock;
    private bool savedCursorVisible;
    private string ReloadScene => useEndingReplay ? "EndingReplay" : "Round1Reload";

    private void Awake()
    {
        IsOpen = false;
        IsRestarting = false;
        resumeInputAt = 0f;
        Time.timeScale = 1f;
        PhotonNetwork.AutomaticallySyncScene = true;
        if (!pausePanel)
        { Debug.LogError("Pause Panel을 연결하세요.", this); enabled = false; return; }
        pausePanel.SetActive(false);
    }

    private void Update()
    {
        if (requestPending && !IsRestarting && Time.realtimeSinceStartup >= requestDeadline)
        { requestPending = false; Debug.LogWarning("재시작 응답이 없습니다. 다시 시도하세요."); }
        if (IsRestarting || ExitSmokeScreen.IsPlaying) return;
        if (ending && ending.HasStarted)
        {
            if (IsOpen) ResumeGame();
            return;
        }
        if (Input.GetKeyDown(KeyCode.Escape) && SessionChat.EscapeConsumedFrame != Time.frameCount)
        {
            if (IsOpen) ResumeGame();
            else OpenMenu();
        }
    }

    private void OpenMenu()
    {
        savedCursorLock = Cursor.lockState;
        savedCursorVisible = Cursor.visible;
        IsOpen = true;
        pausePanel.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void ResumeGame()
    {
        if (IsRestarting) return;
        pausePanel.SetActive(false);
        IsOpen = false;
        Cursor.lockState = savedCursorLock;
        Cursor.visible = savedCursorVisible;
        resumeInputAt = Time.unscaledTime + 0.15f;
    }

    public void RestartGame()
    {
        if (IsRestarting || requestPending || (ending && ending.HasStarted)) return;
        if (!PhotonNetwork.InRoom)
        { SceneManager.LoadScene("Round_1"); return; }
        if (!Application.CanStreamedLevelBeLoaded(ReloadScene))
        { Debug.LogError(ReloadScene + " 씬을 Build Settings에 추가하세요."); return; }
        if (useEndingReplay && PhotonNetwork.CurrentRoom.PlayerCount != 2)
        { Debug.LogWarning("재시작에는 두 플레이어가 필요합니다."); return; }
        requestPending = true;
        requestDeadline = Time.realtimeSinceStartup + 12f;
        photonView.RPC(nameof(RequestRestart), RpcTarget.MasterClient);
    }

    [PunRPC]
    private void RequestRestart(PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient || restartStarted || info.Sender == null ||
            (ending && ending.HasStarted)) return;
        if (!Application.CanStreamedLevelBeLoaded(ReloadScene)) return;
        if (useEndingReplay && PhotonNetwork.CurrentRoom.PlayerCount != 2) return;
        restartStarted = true;
        StartCoroutine(BeginRestart());
    }

    private IEnumerator BeginRestart()
    {
        string token = Guid.NewGuid().ToString("N");
        string tokenKey = useEndingReplay ? "ending_replay" : Round1Reload.RunKey;
        var properties = new Hashtable {
            { tokenKey, token },
            { Round1Reload.RestartingKey, true }
        };
        PhotonNetwork.CurrentRoom.SetCustomProperties(properties);
        float deadline = Time.realtimeSinceStartup + 10f;
        while (PhotonNetwork.InRoom && Time.realtimeSinceStartup < deadline &&
            !Equals(PhotonNetwork.CurrentRoom.CustomProperties[tokenKey], token))
            yield return null;
        if (!PhotonNetwork.InRoom) yield break;
        if (!Equals(PhotonNetwork.CurrentRoom.CustomProperties[tokenKey], token))
        {
            restartStarted = false;
            requestPending = false;
            IsRestarting = false;
            PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { Round1Reload.RestartingKey, false } });
            Debug.LogError("재시작 상태를 서버에 기록하지 못했습니다.");
            yield break;
        }
        if (PhotonNetwork.IsMasterClient) PhotonNetwork.LoadLevel(ReloadScene);
    }

    public override void OnRoomPropertiesUpdate(Hashtable changedProperties)
    {
        if (changedProperties.TryGetValue(Round1Reload.RestartingKey, out object value))
            IsRestarting = value is bool restarting && restarting;
    }

    public void QuitGame()
    {
        if (PhotonNetwork.IsConnected) PhotonNetwork.Disconnect();
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OnDestroy()
    {
        if (IsOpen)
        {
            Cursor.lockState = savedCursorLock;
            Cursor.visible = savedCursorVisible;
        }
        IsOpen = false;
        IsRestarting = false;
        resumeInputAt = 0f;
    }
}
