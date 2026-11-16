using System.Collections;
using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Round1Spawner : MonoBehaviour
{
    [SerializeField] private Transform knightSpawn;
    [SerializeField] private Transform rangerSpawn;
    [SerializeField] private Camera mainCamera;

    private bool spawned;

    private IEnumerator Start()
    {
        if (!PhotonNetwork.InRoom)
        {
            Debug.LogWarning(
                "Photon 방에 들어오지 않았습니다. Main Camera를 유지합니다."
            );
            yield break;
        }

        string token =
            PhotonNetwork.CurrentRoom.CustomProperties[
                Round1Reload.RunKey
            ] as string ?? "initial";

        PhotonNetwork.LocalPlayer.SetCustomProperties(
            new ExitGames.Client.Photon.Hashtable
            {
                { "r1_spawnReady", token }
            }
        );

        // 모든 플레이어가 Round_1 로딩을 마칠 때까지 대기
        while (PhotonNetwork.InRoom &&
               !Round1Reload.AllPlayersReady("r1_spawnReady", token))
        {
            yield return null;
        }

        if (PhotonNetwork.InRoom)
            SpawnPlayer();
    }

    private void SpawnPlayer()
    {
        if (spawned || !PhotonNetwork.InRoom)
            return;

        string key = "c" + PhotonNetwork.LocalPlayer.ActorNumber;

        if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(
                key, out object value))
        {
            Debug.LogError("캐릭터 선택값이 없습니다: " + key);
            return;
        }

        int choice = (int)value;

        string prefabName;
        Transform spawnPoint;

        if (choice == 0)
        {
            prefabName = "KnightPlayer";
            spawnPoint = knightSpawn;
        }
        else if (choice == 1)
        {
            prefabName = "RangerPlayer";
            spawnPoint = rangerSpawn;
        }
        else
        {
            Debug.LogError("알 수 없는 캐릭터 선택값: " + choice);
            return;
        }

        if (spawnPoint == null)
        {
            Debug.LogError(
                prefabName + "의 Spawn Point를 연결하세요."
            );
            return;
        }

        GameObject player = PhotonNetwork.Instantiate(
            prefabName,
            spawnPoint.position,
            spawnPoint.rotation
        );

        if (player == null)
            return;

        spawned = true;

        Camera playerCamera =
            player.GetComponentInChildren<Camera>(true);

        if (playerCamera == null ||
            !playerCamera.enabled ||
            !playerCamera.gameObject.activeInHierarchy)
        {
            Debug.LogError(
                "플레이어 카메라가 없거나 꺼져 있습니다. " +
                "Main Camera를 유지합니다."
            );
            return;
        }

        if (mainCamera != null)
            mainCamera.gameObject.SetActive(false);
    }
}