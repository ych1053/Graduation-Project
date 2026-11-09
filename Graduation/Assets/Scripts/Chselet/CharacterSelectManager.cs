using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Hashtable = ExitGames.Client.Photon.Hashtable;

[RequireComponent(typeof(PhotonView))]
public class CharacterSelectManager : MonoBehaviourPunCallbacks
{
    [Serializable]
    public class PlayerSlotUI
    {
        public RectTransform root;
        public TMP_Text playerName;
        public TMP_Text characterName;
        public TMP_Text readyText;
        public Button leftButton;
        public Button rightButton;
    }

    private const int NoChoice = -1;
    private const int Knight = 0;
    private const int Ranger = 1;

    public TMP_Text roomTitle;
    public PlayerSlotUI slot1;
    public PlayerSlotUI slot2;
    public GameObject knightPortrait;
    public GameObject rangerPortrait;

    public Color normalTextColor = Color.yellow;
    public Color readyTextColor = Color.green;

    private readonly Dictionary<int, int> masterChoices =
        new Dictionary<int, int>();

    private float slot1Y;
    private float slot2Y;

    private static string ChoiceKey(int actor) { return "c" + actor; }
    private static string ReadyKey(int actor) { return "r" + actor; }

    private IEnumerator Start()
    {
        float waitTime = 10f;

        while (!PhotonNetwork.InRoom && waitTime > 0f)
        {
            waitTime -= Time.unscaledDeltaTime;
            yield return null;
        }

        if (!PhotonNetwork.InRoom)
        {
            Debug.LogError(
                "10초 안에 Photon 방에 입장하지 못했습니다. " +
                "Bootstrap에서 방을 만들거나 입장했는지 확인하세요."
            );
            yield break;
        }

        slot1Y = slot1.root.anchoredPosition.y;
        slot2Y = slot2.root.anchoredPosition.y;

        slot1.leftButton.onClick.AddListener(() => RequestDirection(0, -1));
        slot1.rightButton.onClick.AddListener(() => RequestDirection(0, 1));
        slot2.leftButton.onClick.AddListener(() => RequestDirection(1, -1));
        slot2.rightButton.onClick.AddListener(() => RequestDirection(1, 1));

        if (PhotonNetwork.IsMasterClient)
            LoadMasterChoices();

        RefreshScreen();
    }

    private void Update()
    {
        if (PhotonNetwork.InRoom && Input.GetKeyDown(KeyCode.F))
            photonView.RPC(nameof(RequestReady), RpcTarget.MasterClient);
    }

    private void RequestDirection(int slotIndex, int direction)
    {
        Player[] players = GetPlayersInOrder();

        if (slotIndex >= players.Length || !players[slotIndex].IsLocal)
            return;

        photonView.RPC(nameof(RequestMove), RpcTarget.MasterClient, direction);
    }

    [PunRPC]
    private void RequestMove(int direction, PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient || !PhotonNetwork.InRoom)
            return;

        int actor = info.Sender.ActorNumber;
        int current = masterChoices.ContainsKey(actor)
            ? masterChoices[actor] : NoChoice;

        // 반대쪽에서 누르면 먼저 가운데로 돌아옴
        int next;
        if (current == Ranger && direction == -1)
            next = NoChoice;
        else if (current == Knight && direction == 1)
            next = NoChoice;
        else if (current == NoChoice)
            next = direction == -1 ? Knight : Ranger;
        else
            return;

        // 다른 플레이어가 이미 선택한 캐릭터인지 검사
        if (next != NoChoice)
        {
            foreach (Player player in PhotonNetwork.PlayerList)
            {
                if (player.ActorNumber == actor)
                    continue;

                if (masterChoices.TryGetValue(
                        player.ActorNumber, out int otherChoice) &&
                    otherChoice == next)
                {
                    photonView.RPC(
                        nameof(ShakeRejected),
                        RpcTarget.All,
                        actor
                    );
                    return;
                }
            }
        }

        masterChoices[actor] = next;

        PhotonNetwork.CurrentRoom.SetCustomProperties(
            new Hashtable
            {
                { ChoiceKey(actor), next },
                { ReadyKey(actor), false }
            }
        );
    }

    [PunRPC]
    private void RequestReady(PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient || !PhotonNetwork.InRoom)
            return;

        int actor = info.Sender.ActorNumber;

        if (!masterChoices.TryGetValue(actor, out int choice) ||
            choice == NoChoice)
            return;

        PhotonNetwork.CurrentRoom.SetCustomProperties(
            new Hashtable { { ReadyKey(actor), true } }
        );
    }

    [PunRPC]
    private void ShakeRejected(int actor)
    {
        if (PhotonNetwork.LocalPlayer.ActorNumber != actor)
            return;

        Player[] players = GetPlayersInOrder();

        for (int i = 0; i < players.Length; i++)
        {
            if (players[i].ActorNumber == actor)
            {
                StartCoroutine(Shake(
                    i == 0 ? slot1.root : slot2.root
                ));
                break;
            }
        }
    }

    private IEnumerator Shake(RectTransform card)
    {
        Vector2 original = card.anchoredPosition;
        float elapsed = 0f;

        while (elapsed < 0.25f)
        {
            elapsed += Time.deltaTime;
            card.anchoredPosition = original +
                Vector2.right * Mathf.Sin(elapsed * 55f) * 12f;
            yield return null;
        }

        card.anchoredPosition = original;
    }

    private void LoadMasterChoices()
    {
        masterChoices.Clear();

        foreach (Player player in PhotonNetwork.PlayerList)
            masterChoices[player.ActorNumber] = GetChoice(player);
    }

    private Player[] GetPlayersInOrder()
    {
        Player[] players = PhotonNetwork.PlayerList;
        Array.Sort(players, (a, b) =>
            a.ActorNumber.CompareTo(b.ActorNumber));
        return players;
    }

    private int GetChoice(Player player)
    {
        if (player == null || !PhotonNetwork.InRoom)
            return NoChoice;

        object value = PhotonNetwork.CurrentRoom.CustomProperties[
            ChoiceKey(player.ActorNumber)
        ];

        return value is int ? (int)value : NoChoice;
    }

    private bool IsReady(Player player)
    {
        if (player == null || !PhotonNetwork.InRoom)
            return false;

        object value = PhotonNetwork.CurrentRoom.CustomProperties[
            ReadyKey(player.ActorNumber)
        ];

        return value is bool && (bool)value;
    }

    private void RefreshScreen()
    {
        if (!PhotonNetwork.InRoom)
            return;

        roomTitle.text = PhotonNetwork.CurrentRoom.Name;

        Player[] players = GetPlayersInOrder();
        Player first = players.Length > 0 ? players[0] : null;
        Player second = players.Length > 1 ? players[1] : null;

        UpdateSlot(slot1, first, slot1Y);
        UpdateSlot(slot2, second, slot2Y);

        knightPortrait.SetActive(
            GetChoice(first) == Knight ||
            GetChoice(second) == Knight
        );

        rangerPortrait.SetActive(
            GetChoice(first) == Ranger ||
            GetChoice(second) == Ranger
        );
    }

    private void UpdateSlot(PlayerSlotUI slot, Player player, float y)
    {
        slot.root.gameObject.SetActive(player != null);
        if (player == null) return;

        int choice = GetChoice(player);
        bool ready = IsReady(player);

        slot.playerName.text = string.IsNullOrEmpty(player.NickName)
            ? "Player " + player.ActorNumber
            : player.NickName;

        slot.characterName.text = choice == Knight
            ? "기사 선택 완료"
            : choice == Ranger
                ? "궁수 선택 완료"
                : "캐릭터 선택";

        slot.readyText.text = ready
            ? "준비완료"
            : choice == NoChoice
                ? ""
                : "F를 눌러 준비";

        Color textColor = ready
            ? readyTextColor : normalTextColor;

        slot.playerName.color = textColor;
        slot.characterName.color = textColor;
        slot.readyText.color = textColor;

        float x = choice == Knight ? -650f
            : choice == Ranger ? 650f : 0f;

        slot.root.anchoredPosition = new Vector2(x, y);

        slot.leftButton.gameObject.SetActive(choice != Knight);
        slot.rightButton.gameObject.SetActive(choice != Ranger);
        slot.leftButton.interactable = player.IsLocal;
        slot.rightButton.interactable = player.IsLocal;
    }

    public override void OnRoomPropertiesUpdate(Hashtable changedProps)
    {
        RefreshScreen();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        if (PhotonNetwork.IsMasterClient)
            masterChoices[newPlayer.ActorNumber] = NoChoice;

        RefreshScreen();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (PhotonNetwork.IsMasterClient)
            masterChoices.Remove(otherPlayer.ActorNumber);

        RefreshScreen();
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        if (PhotonNetwork.IsMasterClient)
            LoadMasterChoices();

        RefreshScreen();
    }
}