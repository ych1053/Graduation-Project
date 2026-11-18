using Photon.Pun;
using UnityEngine;

public class GimmickTarget : MonoBehaviour
{
    [SerializeField] private LeverTargetStep step;

    private void OnTriggerEnter(Collider other)
    {
        CheckContact(other);
    }

    private void OnTriggerStay(Collider other)
    {
        CheckContact(other);
    }

    private void CheckContact(Collider other)
    {
        if (!PhotonNetwork.IsMasterClient || step == null)
            return;

        WaterDeathZone deathZone =
            other.GetComponentInParent<WaterDeathZone>();

        if (deathZone != null)
        {
            step.TryFailAtDeathZone();
            return;
        }

        ConsumableBox box =
            other.GetComponentInParent<ConsumableBox>();

        if (box != null)
            step.TryHit(box);
    }
}