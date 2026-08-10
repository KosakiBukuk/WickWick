using UnityEngine;

/// <summary>
/// 던져서 바닥/벽에 충돌 시 소음을 발산하여 적을 유인하는 투척물 스크립트
/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class ThrowableItem : MonoBehaviour
{
    [Header("Noise Settings")]
    [SerializeField] private float noiseRadius = 12.0f; // 소음이 도달하는 반경 (미터)
    [SerializeField] private LayerMask enemyLayer;      // 적 AI가 속한 Layer
    [SerializeField] private float minImpactVelocity = 1.5f; // 소음이 발생하는 최소 충돌 속도

    private bool hasLanded = false; // 중복 소음 발생 방지 플래그

    private void OnCollisionEnter(Collision collision)
    {
        // 이미 한번 바닥에 떨어져 소음을 냈다면 중복 발생 차단
        if (hasLanded) return;

        // 슬그머니 놓인 게 아니라 일정 속도 이상으로 쾅 부딪혔을 때만 소음 발생
        if (collision.relativeVelocity.magnitude >= minImpactVelocity)
        {
            hasLanded = true;
            EmitNoise(transform.position);

            // 💡 추후 충돌 사운드(SFX) 및 먼지 이펙트(FX)를 여기서 재생하면 돼!
            Debug.Log($"💥 [{gameObject.name}] 소음 발생! 위치: {transform.position}");
        }
    }

    /// <summary>
    /// 지정된 위치에서 overlapSphere로 주변 적을 찾아 소음을 전달함
    /// </summary>
    private void EmitNoise(Vector3 impactPosition)
    {
        Collider[] enemies = Physics.OverlapSphere(impactPosition, noiseRadius, enemyLayer);
        foreach (Collider enemyCol in enemies)
        {
            // 자식 콜라이더 피격 시에도 부모의 EnemyAI 컴포넌트 탐색
            EnemyAI enemy = enemyCol.GetComponentInParent<EnemyAI>();
            if (enemy != null)
            {
                enemy.OnHearNoise(impactPosition);
            }
        }
    }

    // 씬 뷰에서 소음 범위를 초록색 구체로 시각화 (디버깅용)
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, noiseRadius);
    }
}