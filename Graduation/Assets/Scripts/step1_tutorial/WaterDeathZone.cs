using Photon.Pun;
using UnityEngine;

public class WaterDeathZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        Round1Player player =
            other.GetComponentInParent<Round1Player>();

        if (player != null)
        {
            if (!player.photonView.IsMine) return;

            int myActorNumber =
                PhotonNetwork.LocalPlayer.ActorNumber;

            NetworkCarryBox[] boxes =
                FindObjectsOfType<NetworkCarryBox>();

            foreach (NetworkCarryBox box in boxes)
            {
                if (box.HolderActorNumber == myActorNumber)
                {
                    box.AskToResetIfHeld();
                    break;
                }
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