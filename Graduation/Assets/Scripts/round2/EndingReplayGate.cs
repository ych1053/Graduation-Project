using System.Collections;
using Photon.Pun;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class EndingReplayGate : MonoBehaviour
{
    [SerializeField] private string firstScene = "Round_1";
    private IEnumerator Start()
    {
        Time.timeScale = 1f;
        PhotonNetwork.AutomaticallySyncScene = true;
        if (!PhotonNetwork.InRoom) yield break;
        string token = PhotonNetwork.CurrentRoom.CustomProperties["ending_replay"] as string;
        if (string.IsNullOrEmpty(token)) { Debug.LogError("엔딩의 다시하기 버튼으로 진입하세요."); yield break; }
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { "ending_ready", token } });
        while (PhotonNetwork.InRoom)
        {
            bool ready = PhotonNetwork.CurrentRoom.PlayerCount == 2;
            foreach (var player in PhotonNetwork.PlayerList)
                ready &= (player.CustomProperties["ending_ready"] as string) == token;
            if (ready && PhotonNetwork.IsMasterClient) break;
            yield return null;
        }
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient) yield break;
        if (!Application.CanStreamedLevelBeLoaded(firstScene))
        { Debug.LogError("Build Settings에 " + firstScene + " 씬을 추가하세요."); yield break; }

        // 캐릭터 선택(c + ActorNumber), PUN 씬 동기화 속성만 보존.
        var reset = new Hashtable();
        foreach (var entry in PhotonNetwork.CurrentRoom.CustomProperties)
        {
            string key = entry.Key as string;
            if (key == null || key == "curScn" || key == "ending_replay") continue;
            bool choice = false;
            foreach (var player in PhotonNetwork.PlayerList)
                if (key == "c" + player.ActorNumber) choice = true;
            if (!choice) reset[key] = null;
        }
        reset[Round1Reload.RunKey] = token;
        PhotonNetwork.CurrentRoom.SetCustomProperties(reset);
        PhotonNetwork.DestroyAll();
        // 서버의 새 실행 번호를 받은 후 다음 씬을 연다.
        while (PhotonNetwork.InRoom &&
            (PhotonNetwork.CurrentRoom.CustomProperties[Round1Reload.RunKey] as string) != token)
            yield return null;
        if (PhotonNetwork.InRoom) PhotonNetwork.LoadLevel(firstScene);
    }
}
