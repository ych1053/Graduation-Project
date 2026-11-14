using Photon.Pun;
using UnityEngine;

[RequireComponent(typeof(NetworkCarryBox))]
[RequireComponent(typeof(PhotonView))]
public class ConsumableBox : MonoBehaviourPun, IPunObservable
{
    public bool HasBeenThrown { get; private set; }
    public bool IsConsumed { get; private set; }

    public bool CanPickUp => !HasBeenThrown && !IsConsumed;

    private Rigidbody body;
    private Renderer[] renderers;
    private Collider[] colliders;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        renderers = GetComponentsInChildren<Renderer>(true);
        colliders = GetComponentsInChildren<Collider>(true);
    }

    public void MarkThrownOnMaster()
    {
        if (!PhotonNetwork.IsMasterClient || IsConsumed)
            return;

        photonView.RPC(nameof(MarkThrown), RpcTarget.All);
    }

    [PunRPC]
    private void MarkThrown()
    {
        HasBeenThrown = true;
    }

    public void ConsumeOnMaster()
    {
        if (!PhotonNetwork.IsMasterClient || IsConsumed)
            return;

        photonView.RPC(nameof(Consume), RpcTarget.All);
    }

    [PunRPC]
    private void Consume()
    {
        HasBeenThrown = true;
        HideBox();
    }

    private void HideBox()
    {
        if (IsConsumed) return;

        IsConsumed = true;

        if (body != null)
        {
            if (!body.isKinematic)
            {
                body.velocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            body.isKinematic = true;
        }

        foreach (Collider boxCollider in colliders)
            boxCollider.enabled = false;

        foreach (Renderer boxRenderer in renderers)
            boxRenderer.enabled = false;

        // 오브젝트 자체는 켜 둬서 소모 상태를 계속 동기화
    }

    public void OnPhotonSerializeView(
        PhotonStream stream,
        PhotonMessageInfo info
    )
    {
        if (stream.IsWriting)
        {
            stream.SendNext(HasBeenThrown);
            stream.SendNext(IsConsumed);
        }
        else
        {
            bool thrown = (bool)stream.ReceiveNext();
            bool consumed = (bool)stream.ReceiveNext();

            // 한 번 사용한 상태가 이전 정보 때문에 되돌아가지 않게 함
            if (thrown)
                HasBeenThrown = true;

            if (consumed)
                HideBox();
        }
    }
}