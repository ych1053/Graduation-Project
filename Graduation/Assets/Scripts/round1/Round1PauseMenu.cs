using System.Collections;
//using ExitGames.Client.Photon;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(PhotonView))]
public class Round1PauseMenu : MonoBehaviourPunCallbacks
{
    [SerializeField] private GameObject pausePanel;

    public static bool IsOpen { get; private set; }
    public static bool IsRestarting { get; private set; }

    private static float resumeInputAt;

    public static bool InputBlocked =>
        IsOpen ||
        IsRestarting ||
        Time.unscaledTime < resumeInputAt;

    private bool restartStarted;
    private CursorLockMode savedCursorLock;
    private bool savedCursorVisible;

    private void Awake()
    {
        IsOpen = false;
        IsRestarting = false;
        resumeInputAt = 0f;

        Time.timeScale = 1f;
        PhotonNetwork.AutomaticallySyncScene = true;

        if (pausePanel == null)
        {
            Debug.LogError("Pause Panel을 연결하세요.", this);
            enabled = false;
            return;
        }

        pausePanel.SetActive(false);
    }

    private void Update()
    {
        if (IsRestarting) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (IsOpen)
                ResumeGame();
            else
                OpenMenu();
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

        // 이어하기 버튼을 놓은 입력이 상자 던지기로 이어지지 않게 함
        resumeInputAt = Time.unscaledTime + 0.15f;
    }

    public void RestartGame()
    {
        if (IsRestarting) return;

        if (!PhotonNetwork.InRoom)
        {
            SceneManager.LoadScene("Round_1");
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded("Round1Reload"))
        {
            Debug.LogError(
                "Round1Reload 씬을 Build Settings에 추가하세요."
            );
            return;
        }

        IsRestarting = true;

        photonView.RPC(
            nameof(RequestRestart),
            RpcTarget.MasterClient
        );
    }

    [PunRPC]
    private void RequestRestart()
    {
        if (!PhotonNetwork.IsMasterClient || restartStarted)
            return;

        restartStarted = true;
        StartCoroutine(BeginRestart());
    }

    private IEnumerator BeginRestart()
    {
        string token = System.Guid.NewGuid().ToString();

        PhotonNetwork.CurrentRoom.SetCustomProperties(
            new Hashtable
            {
                { Round1Reload.RunKey, token },
                { Round1Reload.RestartingKey, true }
            }
        );

        // 새 재시작 번호가 서버에 반영된 것을 확인
        while (PhotonNetwork.InRoom &&
               !Equals(
                   PhotonNetwork.CurrentRoom.CustomProperties[
                       Round1Reload.RunKey
                   ],
                   token
               ))
        {
            yield return null;
        }

        if (PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient)
            PhotonNetwork.LoadLevel("Round1Reload");
    }

    public override void OnRoomPropertiesUpdate(
        Hashtable changedProperties
    )
    {
        if (changedProperties.TryGetValue(
                Round1Reload.RestartingKey,
                out object value
            ) && value is bool restarting && restarting)
        {
            IsRestarting = true;
        }
    }

    public void QuitGame()
    {
        if (PhotonNetwork.IsConnected)
            PhotonNetwork.Disconnect();

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
    }
}