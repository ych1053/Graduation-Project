using UnityEngine;

public class Round2StepSensor : MonoBehaviour
{
    private Round2HiddenPath path;
    private BoxCollider floor;
    private string key;

    public void Bind(Round2HiddenPath manager, BoxCollider box, string propertyKey)
    {
        path = manager;
        floor = box;
        key = propertyKey;
    }

    private void OnCollisionEnter(Collision collision) => CheckTop(collision);
    private void OnCollisionStay(Collision collision) => CheckTop(collision);

    private void CheckTop(Collision collision)
    {
        Round1Player player = collision.collider.GetComponentInParent<Round1Player>();
        if (!player || !player.photonView.IsMine || !floor)
            return;

        // 옆면에 부딪히거나 아래에서 머리를 찧은 경우는 제외한다.
        Vector3 up = floor.transform.up;
        Vector3 top = floor.transform.TransformPoint(
            floor.center + Vector3.up * (floor.size.y * 0.5f));
        if (Vector3.Dot(collision.collider.bounds.center - top, up) <= 0f)
            return;

        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint c = collision.GetContact(i);
            if (Mathf.Abs(Vector3.Dot(c.normal, up)) >= 0.6f &&
                Mathf.Abs(Vector3.Dot(c.point - top, up)) < 0.1f)
            {
                path.ReportStep(player, key);
                return;
            }
        }
    }
}
