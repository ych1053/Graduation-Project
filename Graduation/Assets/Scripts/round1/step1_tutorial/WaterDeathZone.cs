using Photon.Pun;
using UnityEngine;

public class WaterDeathZone : MonoBehaviour
{
    private bool missingManagerLogged;

    private void OnTriggerEnter(Collider other)
    {
        Round1Player player =
            other.GetComponentInParent<Round1Player>();

        if (player != null)
        {
            RequestPlayerRespawn(player);
            return;
        }

        // 상자만 떨어진 경우에는 기존 상자 처리만 실행한다.
        NetworkCarryBox fallenBox =
            other.GetComponentInParent<NetworkCarryBox>();

        if (fallenBox != null && PhotonNetwork.IsMasterClient)
        {
            fallenBox.RespawnOnMaster();
        }
    }

    private void OnTriggerStay(Collider other)
    {
        // 복귀 대기 중인 플레이어가 영역 안에 남아 있으면 재요청.
        // 실제 요청 간격과 중복 처리는 관리자가 제어한다.
        Round1Player player =
            other.GetComponentInParent<Round1Player>();

        if (player != null)
        {
            RequestPlayerRespawn(player);
        }
    }

    private void RequestPlayerRespawn(Round1Player player)
    {
        if (!player.photonView.IsMine)
            return;

        if (TeamRespawnManager.Instance == null)
        {
            if (!missingManagerLogged)
            {
                Debug.LogError(
                    "현재 씬에 TeamRespawnManager를 추가하세요."
                );

                missingManagerLogged = true;
            }

            return;
        }

        TeamRespawnManager.Instance.RequestTeamRespawn(player);
    }
}