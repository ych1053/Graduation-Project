using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class PhotonConnectionTest : MonoBehaviourPunCallbacks
{
    private const string TestRoomName = "DogAndBear_TestRoom";
    private bool roomRequestSent;

    private void Start()
    {
        if (PhotonNetwork.InRoom)
        {
            ShowRoomInfo();
        }
        else if (PhotonNetwork.IsConnectedAndReady)
        {
            JoinTestRoom();
        }
        else
        {
            PhotonNetwork.ConnectUsingSettings();
        }
    }

    public override void OnConnectedToMaster()
    {
        Debug.Log("Photon Cloud 연결 성공");
        JoinTestRoom();
    }

    private void JoinTestRoom()
    {
        if (roomRequestSent || PhotonNetwork.InRoom)
            return;

        roomRequestSent = true;

        RoomOptions options = new RoomOptions
        {
            MaxPlayers = 2
        };

        PhotonNetwork.JoinOrCreateRoom(
            TestRoomName,
            options,
            TypedLobby.Default
        );
    }

    public override void OnJoinedRoom()
    {
        ShowRoomInfo();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Debug.Log(newPlayer.NickName + "님이 테스트 방에 들어왔어요.");
        ShowRoomInfo();
    }

    private void ShowRoomInfo()
    {
        Debug.Log(
            "방 입장 성공: " +
            PhotonNetwork.CurrentRoom.Name +
            " / 인원 " +
            PhotonNetwork.CurrentRoom.PlayerCount +
            "/2"
        );
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.LogError("방 입장 실패: " + message);
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        Debug.LogError("방 생성 실패: " + message);
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.LogError("Photon 연결 종료: " + cause);
    }
}