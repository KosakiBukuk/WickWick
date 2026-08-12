using UnityEngine;

/// <summary>
/// 🎯 [적 사망 파편 폭발 연출 모듈]
/// 생성되자마자 자식 큐브들에게 사방으로 튀어나가는 폭발력을 가하고, 
/// 일정 시간 뒤 자동으로 삭제하여 메모리를 정리합니다.
/// </summary>
public class EnemyShatter : MonoBehaviour
{
    [Header("Explosion Physics Settings")]
    [Tooltip("파편이 튀어나가는 폭발 힘")]
    [SerializeField] private float explosionForce = 400f;

    [Tooltip("폭발 반경")]
    [SerializeField] private float explosionRadius = 3f;

    [Tooltip("위쪽으로 솟구치는 가중치")]
    [SerializeField] private float upwardsModifier = 1.5f;

    [Header("Cleanup Settings")]
    [Tooltip("파편들이 사라질 시간(초)")]
    [SerializeField] private float destroyDelay = 3.0f;

    private void Start()
    {
        // 🎯 1. 모든 자식 큐브의 Rigidbody에 물리 폭발력(AddExplosionForce) 가하기!
        Rigidbody[] rbs = GetComponentsInChildren<Rigidbody>();

        foreach (Rigidbody rb in rbs)
        {
            // 약간의 랜덤성 추가로 파편이 진짜처럼 불규칙하게 튐!
            Vector3 randomOffset = Random.insideUnitSphere * 0.2f;
            rb.AddExplosionForce(explosionForce, transform.position + randomOffset, explosionRadius, upwardsModifier);
        }

        // 🎯 2. 지정된 시간이 지나면 파편 오브젝트 자동 삭제 (메모리 최적화!)
        Destroy(gameObject, destroyDelay);
    }
}