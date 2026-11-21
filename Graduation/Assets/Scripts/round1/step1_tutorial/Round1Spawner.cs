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
                "Photon �濡 ������ �ʾҽ��ϴ�. Main Camera�� �����մϴ�."
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

        // ��� �÷��̾ Round_1 �ε��� ��ĥ ������ ���
        while (PhotonNetwork.InRoom &&
               !Round1Reload.AllPlayersReady("r1_spawnReady", token))
        {
            yield return null;
        }

        if (PhotonNetwork.InRoom)
            {
                RunRecordClock.BeginRun();
                SpawnPlayer();
            }
    }

    private void SpawnPlayer()
    {
        if (spawned || !PhotonNetwork.InRoom)
            return;

        string key = "c" + PhotonNetwork.LocalPlayer.ActorNumber;

        if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(
                key, out object value))
        {
            Debug.LogError("ĳ���� ���ð��� �����ϴ�: " + key);
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
            Debug.LogError("�� �� ���� ĳ���� ���ð�: " + choice);
            return;
        }

        if (spawnPoint == null)
        {
            Debug.LogError(
                prefabName + "�� Spawn Point�� �����ϼ���."
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
                "�÷��̾� ī�޶� ���ų� ���� �ֽ��ϴ�. " +
                "Main Camera�� �����մϴ�."
            );
            return;
        }

        if (mainCamera != null)
            mainCamera.gameObject.SetActive(false);
    }
}