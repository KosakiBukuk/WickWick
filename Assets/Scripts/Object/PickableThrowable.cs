using UnityEngine;

/// <summary>
/// 맵 선반에 배치된 E키 습득 가능 벽돌 오브젝트
/// </summary>
public class PickableThrowable : MonoBehaviour, IInteractable
{
    [SerializeField] private GameObject throwablePrefab; // 던졌을 때 실제 발사될 물리 프리팹

    public void Interact(GameObject player)
    {
        PlayerCombat combat = player.GetComponent<PlayerCombat>();
        if (combat != null)
        {
            // PlayerCombat에서 습득 성공 시 맵에 있던 벽돌 삭제
            if (combat.PickupThrowable(throwablePrefab))
            {
                Destroy(gameObject);
            }
        }
    }
}