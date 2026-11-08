using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class PhotonConnectionTest : MonoBehaviourPunCallbacks
{
    private bool lobbyJoinRequested;

    private void Start()
    {
        // 이미 로비에 있으면 다시 접속하지 않음
        if (PhotonNetwork.InLobby)
        {
            Debug.Log("이미 Photon 로비에 접속해 있습니다.");
            return;
        }

        // 이미 방에 들어간 상태라면 로비 접속을 시도하지 않음
        if (PhotonNetwork.InRoom)
        {
            Debug.Log("이미 Photon 방에 들어가 있습니다.");
            return;
        }

        // Photon에 연결되어 있으면 로비 입장만 요청
        if (PhotonNetwork.IsConnectedAndReady)
        {
            JoinDefaultLobby();
            return;
        }

        // 연결되어 있지 않을 때만 Photon 접속
        if (!PhotonNetwork.IsConnected)
        {
            Debug.Log("Photon Cloud 접속을 요청합니다.");

            bool connectStarted = PhotonNetwork.ConnectUsingSettings();

            if (!connectStarted)
            {
                Debug.LogError("Photon 접속 요청을 시작하지 못했습니다.");
            }
        }
    }

    public override void OnConnectedToMaster()
    {
        Debug.Log("Photon Cloud 연결 성공");
        JoinDefaultLobby();
    }

    private void JoinDefaultLobby()
    {
        if (PhotonNetwork.InLobby || lobbyJoinRequested)
            return;

        lobbyJoinRequested = true;

        // JoinLobby 요청이 접수됐는지 확인
        bool requestAccepted =
            PhotonNetwork.JoinLobby(TypedLobby.Default);

        if (!requestAccepted)
        {
            lobbyJoinRequested = false;
            Debug.LogError(
                "로비 입장 요청이 접수되지 않았습니다. Photon 연결 상태를 확인하세요."
            );
        }
    }

    public override void OnJoinedLobby()
    {
        lobbyJoinRequested = false;
        Debug.Log("Photon 기본 로비 입장 성공");
    }

    public override void OnLeftLobby()
    {
        Debug.Log("Photon 로비에서 나왔습니다.");
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        lobbyJoinRequested = false;
        Debug.LogWarning("Photon 연결 종료: " + cause);
    }
}