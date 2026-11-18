using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class Round2ArrivalZone : MonoBehaviour
{
    private BoxCollider zoneCollider;

    private void Awake()
    {
        zoneCollider = GetComponent<BoxCollider>();
        zoneCollider.isTrigger = true;
    }

    public bool ContainsPlayer(int actorNumber)
    {
        if (actorNumber <= 0 ||
            !gameObject.activeInHierarchy ||
            !zoneCollider.enabled)
        {
            return false;
        }

        Vector3 center =
            transform.TransformPoint(zoneCollider.center);

        Vector3 scale = transform.lossyScale;

        Vector3 halfSize = Vector3.Scale(
            zoneCollider.size * 0.5f,
            new Vector3(
                Mathf.Abs(scale.x),
                Mathf.Abs(scale.y),
                Mathf.Abs(scale.z)
            )
        );

        Collider[] overlaps = Physics.OverlapBox(
            center,
            halfSize,
            transform.rotation,
            ~0,
            QueryTriggerInteraction.Ignore
        );

        foreach (Collider hit in overlaps)
        {
            Round1Player player =
                hit.GetComponentInParent<Round1Player>();

            if (player == null)
                continue;

            if (player.photonView.OwnerActorNr == actorNumber)
                return true;
        }

        return false;
    }

    private void OnDrawGizmosSelected()
    {
        BoxCollider box = GetComponent<BoxCollider>();

        if (box == null)
            return;

        Matrix4x4 previousMatrix = Gizmos.matrix;

        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = Color.green;

        Gizmos.DrawWireCube(box.center, box.size);

        Gizmos.matrix = previousMatrix;
    }
}