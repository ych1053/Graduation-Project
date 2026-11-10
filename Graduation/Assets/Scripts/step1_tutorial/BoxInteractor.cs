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

    private NetworkCarryBox box;
    private float chargeStartTime;

    private void Update()
    {
        if (!photonView.IsMine || playerCamera == null)
            return;

        if (box == null)
            box = FindObjectOfType<NetworkCarryBox>();

        if (box == null) return;

        int myActorNumber = PhotonNetwork.LocalPlayer.ActorNumber;

        if (Input.GetKeyDown(KeyCode.E) &&
            box.HolderActorNumber == 0 &&
            Vector3.Distance(transform.position, box.transform.position)
                <= pickupRange)
        {
            box.AskToPickUp();
        }

        if (box.HolderActorNumber != myActorNumber)
            return;

        if (Input.GetMouseButtonDown(0))
            chargeStartTime = Time.time;

        if (Input.GetMouseButtonUp(0))
        {
            float charge = Mathf.Clamp01(
                (Time.time - chargeStartTime) / maxChargeTime
            );

            float force = Mathf.Lerp(
                minThrowForce,
                maxThrowForce,
                charge
            );

            Vector3 direction =
                (playerCamera.transform.forward + Vector3.up * 0.2f).normalized;

            box.AskToThrow(direction, force);
        }
    }
}