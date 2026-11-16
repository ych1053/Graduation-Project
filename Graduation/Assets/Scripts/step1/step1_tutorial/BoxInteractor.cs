using Photon.Pun;
using UnityEngine;

[RequireComponent(typeof(PhotonView))]
public class BoxInteractor : MonoBehaviourPun
{
    [SerializeField] private Camera playerCamera;
    [SerializeField] private float pickupRange = 2.3f;
    [SerializeField] private float maxChargeTime = 2f;
    [SerializeField] private float minThrowForce = 5f;
    [SerializeField] private float maxThrowForce = 14f;

    private NetworkCarryBox chargingBox;
    private bool isCharging;
    private float chargeStartTime;

    private void Update()
    {
        if (!photonView.IsMine || playerCamera == null)
            return;
        if (Round1PauseMenu.InputBlocked)
        {
            isCharging = false;
            chargingBox = null;
            return;
        }//재시작
        int myActorNumber = PhotonNetwork.LocalPlayer.ActorNumber;

        NetworkCarryBox heldBox = FindMyHeldBox(myActorNumber);

        // 상자를 놓치거나 리스폰으로 사라지면 충전 취소
        if (heldBox == null || heldBox != chargingBox)
        {
            isCharging = false;
            chargingBox = null;
        }

        if (heldBox == null)
        {
            if (Input.GetKeyDown(KeyCode.E))
            {
                NetworkCarryBox nearestBox = FindNearestAvailableBox();

                if (nearestBox != null)
                    nearestBox.AskToPickUp();
            }

            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            isCharging = true;
            chargingBox = heldBox;
            chargeStartTime = Time.time;
        }

        if (isCharging && Input.GetMouseButtonUp(0))
        {
            float charge = Mathf.Clamp01(
                (Time.time - chargeStartTime) /
                Mathf.Max(0.01f, maxChargeTime)
            );

            float force = Mathf.Lerp(
                minThrowForce,
                maxThrowForce,
                charge
            );

            Vector3 direction =
                (playerCamera.transform.forward +
                 Vector3.up * 0.2f).normalized;

            heldBox.AskToThrow(direction, force);

            isCharging = false;
            chargingBox = null;
        }
    }

    private NetworkCarryBox FindMyHeldBox(int actorNumber)
    {
        NetworkCarryBox[] boxes =
            FindObjectsOfType<NetworkCarryBox>();

        foreach (NetworkCarryBox box in boxes)
        {
            if (box.HolderActorNumber == actorNumber)
                return box;
        }

        return null;
    }

    private NetworkCarryBox FindNearestAvailableBox()
    {
        NetworkCarryBox nearest = null;
        float nearestDistance = pickupRange;

        NetworkCarryBox[] boxes =
            FindObjectsOfType<NetworkCarryBox>();

        foreach (NetworkCarryBox box in boxes)
        {
            // 다른 사람이 들고 있는 상자는 제외
            if (box.HolderActorNumber != 0)
                continue;
            ConsumableBox consumable = box.GetComponent<ConsumableBox>();//이미던진

            if (consumable != null && !consumable.CanPickUp)//상자가앞에있어도
                continue;//사용하지않은상자선택가능
            float distance = Vector3.Distance(
                transform.position,
                box.transform.position
            );

            if (distance <= nearestDistance)
            {
                nearestDistance = distance;
                nearest = box;
            }
        }

        return nearest;
    }
}