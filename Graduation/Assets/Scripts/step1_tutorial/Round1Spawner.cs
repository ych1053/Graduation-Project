using Photon.Pun;
using UnityEngine;

public class Round1Spawner : MonoBehaviour
{
    [SerializeField] private Transform knightSpawn;
    [SerializeField] private Transform rangerSpawn;
    [SerializeField] private Camera mainCamera;

    private void Start()
    {
        if (!PhotonNetwork.InRoom)
        {
            Debug.LogWarning(
                "Photon 방에 들어오지 않았습니다. Main Camera를 유지합니다."
            );
            return;
        }

        string key = "c" + PhotonNetwork.LocalPlayer.ActorNumber;

        if (!PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey(key))
        {
            Debug.LogError("캐릭터 선택값이 없습니다: " + key);
            return;
        }

        int choice = (int)PhotonNetwork.CurrentRoom.CustomProperties[key];

        string prefabName;
        Transform spawnPoint;

        // CharacterSelectManager의 실제 선택값과 맞아야 함
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
            Debug.LogError(prefabName + "의 Spawn Point가 연결되지 않았습니다.");
            return;
        }

        GameObject player = PhotonNetwork.Instantiate(
            prefabName,
            spawnPoint.position,
            spawnPoint.rotation
        );

        Camera playerCamera = player.GetComponentInChildren<Camera>(true);

        if (playerCamera == null || !playerCamera.gameObject.activeInHierarchy)
        {
            Debug.LogError(
                "플레이어 카메라가 없거나 꺼져 있습니다. Main Camera를 유지합니다."
            );
            return;
        }

        if (mainCamera != null)
            mainCamera.gameObject.SetActive(false);
    }
}