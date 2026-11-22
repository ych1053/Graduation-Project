using Photon.Pun;
using UnityEngine;

// 움직이는 도끼날 또는 창끝의 자식 Hitbox에 붙인다.
// Trigger Collider와 kinematic Rigidbody가 애니메이션을 따라 움직인다.
[RequireComponent(typeof(Collider), typeof(Rigidbody))]
public class AnimatedHazardHitbox : MonoBehaviour
{
    private Collider hitbox;
    private Rigidbody body;
    private bool missingManagerLogged;

    private void Awake()
    {
        hitbox = GetComponent<Collider>();
        body = GetComponent<Rigidbody>();
        hitbox.isTrigger = true;
        body.isKinematic = true;
        body.useGravity = false;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
    }

    private void OnTriggerEnter(Collider other) => Hit(other);
    private void OnTriggerStay(Collider other) => Hit(other);

    private void Hit(Collider other)
    {
        if (!PhotonNetwork.InRoom || Round1PauseMenu.IsRestarting) return;

        Round1Player player = other.GetComponentInParent<Round1Player>();
        if (player == null || !player.photonView.IsMine) return;

        TeamRespawnManager manager = TeamRespawnManager.Instance;
        if (manager == null)
        {
            if (!missingManagerLogged)
            {
                Debug.LogError("Round_2에 TeamRespawnManager가 필요합니다.", this);
                missingManagerLogged = true;
            }
            return;
        }
        manager.RequestTeamRespawn(player);
    }
}
