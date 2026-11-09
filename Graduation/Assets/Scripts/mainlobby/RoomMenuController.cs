using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RoomMenuController : MonoBehaviourPunCallbacks
{
    private bool roomRequestPending;//새로추가했음

    public TMP_InputField roomNameInput;
    public TMP_InputField nicknameInput;
    public TMP_Text statusText;
    public Transform roomListContent;
    public Button roomRowPrefab;

    private readonly Dictionary<string, RoomInfo> rooms =
        new Dictionary<string, RoomInfo>();

    private readonly List<Button> displayedRows = new List<Button>();

    private void Start()
    {
        roomNameInput.text = "Room_" + Random.Range(100, 1000);
    }

    public void ConfirmNickname()
    {
        string nickname = nicknameInput.text.Trim();

        if (string.IsNullOrEmpty(nickname))
        {
            statusText.text = "닉네임을 입력해 주세요.";
            return;
        }

        PhotonNetwork.NickName = nickname;
        //statusText.text = "닉네임 설정: " + nickname;
    }

    public void CreateRoom()
    {
        if (!CheckNickname()) return;

        if (!PhotonNetwork.InLobby)
        {
            statusText.text = "Photon 로비 접속을 기다려 주세요.";
            return;
        }

        string roomName = roomNameInput.text.Trim();

        if (string.IsNullOrEmpty(roomName))
        {
            statusText.text = "방 이름을 입력해 주세요.";
            return;
        }

        RoomOptions options = new RoomOptions
        {
            MaxPlayers = 2,
            IsOpen = true,
            IsVisible = true
        };

        statusText.text = "방을 만드는 중...";
        PhotonNetwork.CreateRoom(roomName, options, TypedLobby.Default);
    }

    private bool CheckNickname()
    {
        string nickname = nicknameInput.text.Trim();

        if (string.IsNullOrEmpty(nickname))
        {
            statusText.text = "먼저 닉네임을 입력해 주세요.";
            return false;
        }

        PhotonNetwork.NickName = nickname;
        return true;
    }

    public void JoinSelectedRoom(string roomName)
    {
        /*if (!CheckNickname()) return;

        statusText.text = "방에 입장하는 중...";
        PhotonNetwork.JoinRoom(roomName);*/
        if (roomRequestPending)
            return;

        if (!PhotonNetwork.InLobby || PhotonNetwork.InRoom)
        {
            statusText.text = "로비 연결 또는 방 입장을 기다려 주세요.";
            return;
        }

        if (!CheckNickname()) return;

        roomRequestPending = true;
        statusText.text = "방에 입장하는 중...";
        PhotonNetwork.JoinRoom(roomName);
    }

    public override void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        foreach (RoomInfo room in roomList)
        {
            if (room.RemovedFromList)
                rooms.Remove(room.Name);
            else
                rooms[room.Name] = room;
        }

        RefreshRoomList();
    }

    private void RefreshRoomList()
    {
        foreach (Button row in displayedRows)
        {
            if (row != null)
                Destroy(row.gameObject);
        }

        displayedRows.Clear();

        foreach (RoomInfo room in rooms.Values)
        {
            Button row = Instantiate(roomRowPrefab, roomListContent);
            row.gameObject.SetActive(true);

            TMP_Text label = row.GetComponentInChildren<TMP_Text>();
            label.text = room.Name + "  (" +
                         room.PlayerCount + "/" + room.MaxPlayers + ")";

            row.interactable =
                room.IsOpen && room.PlayerCount < room.MaxPlayers;

            string selectedName = room.Name;
            row.onClick.AddListener(
                () => JoinSelectedRoom(selectedName)
            );

            displayedRows.Add(row);
        }
    }

    public override void OnJoinedRoom()
    {
        statusText.text =
            "입장 완료: " + PhotonNetwork.CurrentRoom.Name +
            " (" + PhotonNetwork.CurrentRoom.PlayerCount + "/2)";

        if (PhotonNetwork.IsMasterClient &&
            PhotonNetwork.CurrentRoom.PlayerCount == 1)
        {
            PhotonNetwork.LoadLevel("CharacterSelect");
        }
        roomRequestPending = false;
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        statusText.text = "방 생성 실패: " + message;
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        statusText.text = "방 입장 실패: " + message;
        roomRequestPending = false;
    }
}