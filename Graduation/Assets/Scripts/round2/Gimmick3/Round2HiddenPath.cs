using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class Round2HiddenPath : MonoBehaviourPunCallbacks
{
    [SerializeField] private Round2Spawner spawner;
    [SerializeField] private Transform player1Tiles;
    [SerializeField] private BoxCollider player1GoalFloor;
    [SerializeField] private GameObject player2Bridge;

    private const string BridgeKey = "r2_g3_bridge";
    private Renderer[][] tileRenderers;
    private string[] tileKeys;
    private readonly HashSet<string> pending = new HashSet<string>();
    private string run;
    private string attemptStamp;
    private int appliedAttempt = -1;

    private void Awake()
    {
        if (!spawner || !player1Tiles || !player1GoalFloor || !player2Bridge ||
            player1Tiles.childCount == 0)
        {
            Debug.LogError("Gimmick03: 관리자 연결과 P1 발판을 확인하세요.", this);
            enabled = false;
            return;
        }

        int count = player1Tiles.childCount;
        tileRenderers = new Renderer[count][];
        tileKeys = new string[count];
        for (int i = 0; i < count; i++)
        {
            Transform tile = player1Tiles.GetChild(i);
            BoxCollider floor = tile.GetComponent<BoxCollider>();
            if (!floor || floor.isTrigger || !floor.enabled)
            {
                Debug.LogError(tile.name + ": 발판 루트에 일반 Box Collider가 필요합니다.", tile);
                enabled = false;
                return;
            }

            tileKeys[i] = "r2_g3_tile_" + i;
            tileRenderers[i] = tile.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer visual in tileRenderers[i])
                visual.enabled = false;

            AddSensor(floor, tileKeys[i]);
        }

        AddSensor(player1GoalFloor, BridgeKey);
        player2Bridge.SetActive(false);
    }

    private void AddSensor(BoxCollider floor, string key)
    {
        Round2StepSensor sensor = floor.gameObject.AddComponent<Round2StepSensor>();
        sensor.Bind(this, floor, key);
    }

    private void Update()
    {
        SyncAttempt();
    }

    private bool SyncAttempt()
    {
        Round2SharedMovement shared = Round2SharedMovement.Instance;
        if (!PhotonNetwork.InRoom || !shared)
            return false;

        if (run == null)
        {
            PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(
                Round1Reload.RunKey, out object token);
            run = token as string ?? "initial";
        }

        if (appliedAttempt != shared.CurrentAttempt)
        {
            appliedAttempt = shared.CurrentAttempt;
            attemptStamp = run + "|" + appliedAttempt;
            pending.Clear();
            RefreshDisplay();
        }
        return true;
    }

    public void ReportStep(Round1Player player, string key)
    {
        if (!isActiveAndEnabled || !player.photonView.IsMine ||
            spawner.LocalPlayerSlot != 1 || !SyncAttempt() ||
            Round2SharedMovement.Instance.IsSharedMode ||
            IsMarked(key) || !pending.Add(key))
            return;

        // P1의 컴퓨터가 실제 접촉을 감지하고 두 화면에 결과를 공유한다.
        if (!PhotonNetwork.CurrentRoom.SetCustomProperties(
                new Hashtable { { key, attemptStamp } }))
            pending.Remove(key);
    }

    private bool IsMarked(string key)
    {
        return PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(key, out object value)
            && value is string stamp && stamp == attemptStamp;
    }

    private void RefreshDisplay()
    {
        for (int i = 0; i < tileKeys.Length; i++)
        {
            bool visible = IsMarked(tileKeys[i]);
            foreach (Renderer visual in tileRenderers[i])
                visual.enabled = visible;
        }
        player2Bridge.SetActive(IsMarked(BridgeKey));
    }

    public override void OnRoomPropertiesUpdate(Hashtable changed)
    {
        if (!SyncAttempt())
            return;
        if (changed.ContainsKey(BridgeKey))
        {
            RefreshDisplay();
            return;
        }
        foreach (string key in tileKeys)
        {
            if (changed.ContainsKey(key))
            {
                RefreshDisplay();
                return;
            }
        }
    }
}
