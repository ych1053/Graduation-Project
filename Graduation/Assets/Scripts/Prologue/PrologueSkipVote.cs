using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(PhotonView))]
public class PrologueSkipVote : MonoBehaviourPunCallbacks
{
    [SerializeField] private TMP_Text skipPrompt;

    private readonly HashSet<int> readyPlayers = new HashSet<int>();
    private bool pressedF;

    // 스킵과 기존 프롤로그 종료가 동시에 일어나도 한 번만 씬 이동
    private static bool loadStarted;

    private void Awake()
    {
        loadStarted = false;
    }

    private void Start()
    {
        PhotonNetwork.AutomaticallySyncScene = true;
        UpdatePrompt(0);
    }

    private void Update()
    {
        if (!PhotonNetwork.InRoom || pressedF) return;

        if (Input.GetKeyDown(KeyCode.F))
        {
            pressedF = true;
            UpdatePrompt(readyPlayers.Count);

            photonView.RPC(
                nameof(RequestSkip),
                RpcTarget.MasterClient
            );
        }
    }

    [PunRPC]
    private void RequestSkip(PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient || loadStarted) return;

        int actorNumber = info.Sender.ActorNumber;

        if (PhotonNetwork.CurrentRoom.GetPlayer(actorNumber) == null)
            return;

        readyPlayers.Add(actorNumber);

        photonView.RPC(
            nameof(ShowReadyCount),
            RpcTarget.All,
            readyPlayers.Count
        );

        // 반드시 방에 두 명이 있고, 두 명 모두 F를 눌러야 이동
        if (PhotonNetwork.CurrentRoom.PlayerCount == 2 &&
            readyPlayers.Count == 2)
        {
            LoadRound1Once();
        }
    }

    [PunRPC]
    private void ShowReadyCount(int count)
    {
        UpdatePrompt(count);
    }

    private void UpdatePrompt(int count)
    {
        if (skipPrompt == null) return;

        if (pressedF)
        {
            skipPrompt.text =
                $"상대 플레이어를 기다리는 중... ({count}/2)";
        }
        else
        {
            skipPrompt.text =
                $"준비가 되면 F키를 눌러 진행하세요 ({count}/2)";
        }
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        readyPlayers.Remove(otherPlayer.ActorNumber);

        photonView.RPC(
            nameof(ShowReadyCount),
            RpcTarget.All,
            readyPlayers.Count
        );
    }

    public static void LoadRound1Once()
    {
        if (loadStarted ||
            !PhotonNetwork.InRoom ||
            !PhotonNetwork.IsMasterClient)
            return;

        loadStarted = true;
        PhotonNetwork.LoadLevel("Round_1");
    }
}