using System.Collections;
//using ExitGames.Client.Photon;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Round1Reload : MonoBehaviour
{
    public const string RunKey = "r1_run";
    public const string RestartingKey = "r1_restarting";

    private const string ReadyKey = "r1_reloadReady";
    private const string ResetDoneKey = "r1_resetDone";

    private IEnumerator Start()
    {
        Time.timeScale = 1f;
        PhotonNetwork.AutomaticallySyncScene = true;

        if (!PhotonNetwork.InRoom)
        {
            SceneManager.LoadScene("Round_1");
            yield break;
        }

        string token = null;

        while (PhotonNetwork.InRoom && string.IsNullOrEmpty(token))
        {
            token =
                PhotonNetwork.CurrentRoom.CustomProperties[RunKey]
                as string;

            yield return null;
        }

        if (!PhotonNetwork.InRoom)
            yield break;

        // 이 플레이어가 재시작 씬에 도착했음을 알림
        PhotonNetwork.LocalPlayer.SetCustomProperties(
            new Hashtable { { ReadyKey, token } }
        );

        // 두 플레이어가 모두 기존 씬을 떠날 때까지 기다림
        while (PhotonNetwork.InRoom)
        {
            if (PhotonNetwork.IsMasterClient &&
                AllPlayersReady(ReadyKey, token))
                break;

            yield return null;
        }

        if (!PhotonNetwork.InRoom)
            yield break;

        // 이전 플레이어 생성 정보와 캐시된 네트워크 오브젝트 정리
        PhotonNetwork.DestroyAll();

        Hashtable resetProperties = new Hashtable
        {
            { "round1PathRaised", false },
            { RestartingKey, false },
            { ResetDoneKey, token }
        };

        for (int i = 1; i <= 5; i++)
        {
            resetProperties["g2_step_" + i + "_state"] = 0;
            resetProperties["g2_step_" + i + "_time"] = 0d;
        }

        PhotonNetwork.CurrentRoom.SetCustomProperties(resetProperties);

        // 초기화가 서버에 반영된 다음 돌아감
        while (PhotonNetwork.InRoom &&
               !Equals(
                   PhotonNetwork.CurrentRoom.CustomProperties[ResetDoneKey],
                   token
               ))
        {
            yield return null;
        }

        if (PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient)
            PhotonNetwork.LoadLevel("Round_1");
    }

    public static bool AllPlayersReady(string key, string token)
    {
        if (!PhotonNetwork.InRoom)
            return false;

        foreach (var player in PhotonNetwork.PlayerList)
        {
            if (!player.CustomProperties.TryGetValue(
                    key, out object value
                ) || !Equals(value, token))
            {
                return false;
            }
        }

        return true;
    }
}