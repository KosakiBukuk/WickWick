using UnityEngine;

/// <summary>
/// 🎯 통과하는 플레이어의 구역 번호를 갱신해 주는 감지기
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class ZoneBoundaryTrigger : MonoBehaviour
{
    [Header("Target Zone")]
    [Tooltip("이 트리거를 밟았을 때 플레이어에게 지정할 구역 번호")]
    [SerializeField] private int targetZone = 1;

    private void Awake()
    {
        // 콜라이더를 트리거 모드로 자동 세팅
        GetComponent<BoxCollider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // 🎯 부딪힌 플레이어의 컴포넌트를 가져와서 구역 번호 갱신!
            PlayerController player = other.GetComponent<PlayerController>();
            if (player != null)
            {
                player.SetCurrentZone(targetZone);
            }
        }
    }
}