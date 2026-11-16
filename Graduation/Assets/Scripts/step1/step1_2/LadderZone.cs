using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class LadderZone : MonoBehaviour
{
    public Transform bottomPoint;
    public Transform topPoint;
    public Transform exitPoint;

    public bool IsReady =>
        bottomPoint != null &&
        topPoint != null &&
        exitPoint != null;

    private void Awake()
    {
        GetComponent<BoxCollider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        RegisterPlayer(other);
    }

    private void OnTriggerStay(Collider other)
    {
        RegisterPlayer(other);
    }

    private void RegisterPlayer(Collider other)
    {
        PlayerLadderClimb player =
            other.GetComponentInParent<PlayerLadderClimb>();

        if (player != null && player.photonView.IsMine)
            player.SetNearbyLadder(this);
    }

    private void OnTriggerExit(Collider other)
    {
        PlayerLadderClimb player =
            other.GetComponentInParent<PlayerLadderClimb>();

        if (player != null && player.photonView.IsMine)
            player.ClearNearbyLadder(this);
    }
}