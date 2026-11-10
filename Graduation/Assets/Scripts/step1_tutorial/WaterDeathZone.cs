using Photon.Pun;
using UnityEngine;

public class WaterDeathZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        Round1Player player = other.GetComponentInParent<Round1Player>();

        if (player != null)
        {
            // 각 컴퓨터는 자기 캐릭터만 복귀시킴
            if (!player.photonView.IsMine) return;

            NetworkCarryBox box = FindObjectOfType<NetworkCarryBox>();

            // 상자를 들고 빠졌다면 상자도 원래 위치로
            if (box != null &&
                box.HolderActorNumber ==
                PhotonNetwork.LocalPlayer.ActorNumber)
            {
                box.AskToResetIfHeld();
            }

            player.RespawnAtStart();
            return;
        }

        NetworkCarryBox fallenBox =
            other.GetComponentInParent<NetworkCarryBox>();

        if (fallenBox != null && PhotonNetwork.IsMasterClient)
            fallenBox.RespawnOnMaster();
    }
}