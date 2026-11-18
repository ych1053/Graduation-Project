using System.Collections;
using Photon.Pun;
using UnityEngine;

public class Round2Spawner : MonoBehaviour
{
    [Header("각 플레이어의 시작 위치")]
    [SerializeField] private Transform player1Spawn;
    [SerializeField] private Transform player2Spawn;

    [Header("캐릭터 생성 전 사용할 카메라")]
    [SerializeField] private Camera mainCamera;

    private const string ReadyKey = "r2_spawnReady";

    private bool spawned;

    // 다음 공유 조작 단계에서도 사용할 플레이어 구분
    public int Player1ActorNumber { get; private set; }
    public int Player2ActorNumber { get; private set; }
    public int LocalPlayerSlot { get; private set; }

    private IEnumerator Start()
    {
        if (!PhotonNetwork.InRoom)
        {
            Debug.LogWarning(
                "[Round2] Photon 방에 들어오지 않았습니다. " +
                "기존 게임 흐름을 통해 Round_2로 이동하세요."
            );

            yield break;
        }

        if (player1Spawn == null || player2Spawn == null)
        {
            Debug.LogError(
                "[Round2] Player 1 Spawn과 Player 2 Spawn을 연결하세요."
            );

            yield break;
        }

        // 기존 Round_1 재시작 구분값을 이어서 사용한다.
        // Round_2의 로딩 완료 표시는 별도 키로 구분한다.
        string token =
            PhotonNetwork.CurrentRoom.CustomProperties[
                Round1Reload.RunKey
            ] as string ?? "initial";

        PhotonNetwork.LocalPlayer.SetCustomProperties(
            new ExitGames.Client.Photon.Hashtable
            {
                { ReadyKey, token }
            }
        );

        Debug.Log("[Round2] 두 플레이어의 씬 로딩을 기다립니다.");

        while (PhotonNetwork.InRoom &&
               !BothPlayersReady(token))
        {
            yield return null;
        }

        if (!PhotonNetwork.InRoom)
            yield break;

        AssignPlayerSlots();

        GameObject player = SpawnPlayer();

        if (player == null)
            yield break;

        // 플레이어의 초기화가 진행될 시간을 한 프레임 준다.
        yield return null;

        SwitchToPlayerCamera(player);
    }

    private bool BothPlayersReady(string token)
    {
        if (!PhotonNetwork.InRoom)
            return false;

        var players = PhotonNetwork.PlayerList;

        // 이 게임은 두 플레이어가 모두 있어야 시작한다.
        if (players.Length != 2)
            return false;

        foreach (var player in players)
        {
            if (!player.CustomProperties.TryGetValue(
                    ReadyKey, out object value))
            {
                return false;
            }

            if (!(value is string readyToken) ||
                readyToken != token)
            {
                return false;
            }
        }

        return true;
    }

    private void AssignPlayerSlots()
    {
        var players = PhotonNetwork.PlayerList;

        int firstActor = players[0].ActorNumber;
        int secondActor = players[1].ActorNumber;

        // ActorNumber가 작은 사람이 먼저 입장한 플레이어다.
        Player1ActorNumber = Mathf.Min(firstActor, secondActor);
        Player2ActorNumber = Mathf.Max(firstActor, secondActor);

        LocalPlayerSlot =
            PhotonNetwork.LocalPlayer.ActorNumber ==
            Player1ActorNumber
                ? 1
                : 2;
    }

    private GameObject SpawnPlayer()
    {
        if (spawned || !PhotonNetwork.InRoom)
            return null;

        // 기존 캐릭터 선택 정보를 그대로 사용한다.
        string key =
            "c" + PhotonNetwork.LocalPlayer.ActorNumber;

        if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(
                key, out object value))
        {
            Debug.LogError(
                "[Round2] 캐릭터 선택값이 없습니다: " + key
            );

            return null;
        }

        if (!(value is int choice))
        {
            Debug.LogError(
                "[Round2] 캐릭터 선택값의 자료형이 올바르지 않습니다."
            );

            return null;
        }

        string prefabName;

        if (choice == 0)
        {
            prefabName = "KnightPlayer";
        }
        else if (choice == 1)
        {
            prefabName = "RangerPlayer";
        }
        else
        {
            Debug.LogError(
                "[Round2] 알 수 없는 캐릭터 선택값: " + choice
            );

            return null;
        }

        // 캐릭터 종류가 아니라 플레이어 번호로 위치를 정한다.
        Transform spawnPoint =
            LocalPlayerSlot == 1
                ? player1Spawn
                : player2Spawn;

        GameObject player = PhotonNetwork.Instantiate(
            prefabName,
            spawnPoint.position,
            spawnPoint.rotation
        );

        if (player == null)
            return null;

        spawned = true;

        Debug.Log(
            "[Round2] 플레이어 " + LocalPlayerSlot +
            " 생성 완료 / 캐릭터: " + prefabName
        );

        return player;
    }

    private void SwitchToPlayerCamera(GameObject player)
    {
        Camera[] cameras =
            player.GetComponentsInChildren<Camera>(true);

        foreach (Camera camera in cameras)
        {
            if (!camera.enabled ||
                !camera.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (mainCamera != null)
                mainCamera.gameObject.SetActive(false);

            return;
        }

        Debug.LogError(
            "[Round2] 활성화된 플레이어 카메라가 없습니다. " +
            "Main Camera를 유지합니다."
        );
    }
}