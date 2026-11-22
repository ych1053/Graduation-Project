using Photon.Pun;
using ExitGames.Client.Photon;

public static class RunRecordClock
{
    // Round1Spawner: 모든 플레이어의 로딩 대기 뒤, SpawnPlayer 호출 직전에 실행.
    public static void BeginRun()
    {
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient) return;
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable {
            { "run_start", PhotonNetwork.Time },
            { "run_clock_token", PhotonNetwork.CurrentRoom.CustomProperties[Round1Reload.RunKey] as string ?? "initial" },
            { "rank_json", null }, { "rank_message", null }, { "rank_current_id", null }
        });
    }
}
