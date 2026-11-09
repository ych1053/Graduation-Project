using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class SelectionCountdown : MonoBehaviourPunCallbacks
{
    private const string CountdownKey = "countdownAt";
    private const string PrologueStartKey = "prologueStartAt";

    public TMP_Text countdownText;

    private bool propertyChangePending;
    private bool loadingPrologue;

    private void Start()
    {
        countdownText.gameObject.SetActive(false);

        if (PhotonNetwork.IsMasterClient)
            CheckStartCondition();
    }

    private void Update()
    {
        if (!PhotonNetwork.InRoom || loadingPrologue)
            return;

        double countdownEnd = GetCountdownEndTime();

        if (countdownEnd <= 0)
        {
            countdownText.gameObject.SetActive(false);
            return;
        }

        double remaining = countdownEnd - PhotonNetwork.Time;
        countdownText.gameObject.SetActive(true);

        if (remaining > 0)
        {
            countdownText.text =
                Mathf.CeilToInt((float)remaining).ToString();
            return;
        }

        countdownText.text = "시작!";

        if (!PhotonNetwork.IsMasterClient)
            return;

        if (!CanStart())
        {
            CancelCountdown();
            return;
        }

        loadingPrologue = true;

        // 양쪽 플레이어가 같은 시각에 프롤로그를 재생하도록 저장
        PhotonNetwork.CurrentRoom.SetCustomProperties(
            new Hashtable
            {
                { PrologueStartKey, PhotonNetwork.Time + 3.0 }
            }
        );

        PhotonNetwork.CurrentRoom.IsOpen = false;
        PhotonNetwork.CurrentRoom.IsVisible = false;
        PhotonNetwork.LoadLevel("Prologue");
    }

    private void CheckStartCondition()
    {
        if (!PhotonNetwork.IsMasterClient ||
            !PhotonNetwork.InRoom ||
            loadingPrologue ||
            propertyChangePending)
            return;

        double countdownEnd = GetCountdownEndTime();

        if (CanStart())
        {
            if (countdownEnd <= 0)
            {
                propertyChangePending = true;

                PhotonNetwork.CurrentRoom.SetCustomProperties(
                    new Hashtable
                    {
                        { CountdownKey, PhotonNetwork.Time + 3.0 }
                    }
                );
            }
        }
        else if (countdownEnd > 0)
        {
            CancelCountdown();
        }
    }

    private void CancelCountdown()
    {
        if (!PhotonNetwork.IsMasterClient || propertyChangePending)
            return;

        propertyChangePending = true;

        PhotonNetwork.CurrentRoom.SetCustomProperties(
            new Hashtable
            {
                { CountdownKey, 0.0 }
            }
        );
    }

    private bool CanStart()
    {
        if (!PhotonNetwork.InRoom ||
            PhotonNetwork.PlayerList.Length != 2)
            return false;

        Player first = PhotonNetwork.PlayerList[0];
        Player second = PhotonNetwork.PlayerList[1];

        int firstChoice = GetChoice(first);
        int secondChoice = GetChoice(second);

        return firstChoice != -1 &&
               secondChoice != -1 &&
               firstChoice != secondChoice &&
               IsReady(first) &&
               IsReady(second);
    }

    private int GetChoice(Player player)
    {
        object value = PhotonNetwork.CurrentRoom.CustomProperties[
            "c" + player.ActorNumber
        ];

        return value is int ? (int)value : -1;
    }

    private bool IsReady(Player player)
    {
        object value = PhotonNetwork.CurrentRoom.CustomProperties[
            "r" + player.ActorNumber
        ];

        return value is bool && (bool)value;
    }

    private double GetCountdownEndTime()
    {
        object value = PhotonNetwork.CurrentRoom.CustomProperties[
            CountdownKey
        ];

        return value is double ? (double)value : 0.0;
    }

    public override void OnRoomPropertiesUpdate(Hashtable changedProps)
    {
        if (changedProps.ContainsKey(CountdownKey))
            propertyChangePending = false;

        if (PhotonNetwork.IsMasterClient)
            CheckStartCondition();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (PhotonNetwork.IsMasterClient)
            CheckStartCondition();
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        propertyChangePending = false;

        if (PhotonNetwork.IsMasterClient)
            CheckStartCondition();
    }
}