using UnityEngine;

/// <summary>
/// 🎯 던져서 바닥/벽에 충돌 시 소음을 발산하여 적을 유인하고, 
/// 충돌 속도에 맞춰 3D 충돌 사운드(SFX)를 재생하는 투척물 스크립트입니다.
/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class ThrowableItem : MonoBehaviour
{
    [Header("Noise Settings")]
    [Tooltip("소음이 도달하는 반경 (미터)")]
    [SerializeField] private float noiseRadius = 12.0f;

    [Tooltip("적 AI가 속한 LayerMask")]
    [SerializeField] private LayerMask enemyLayer;

    [Tooltip("소음 및 사운드가 발생하는 최소 충돌 속도")]
    [SerializeField] private float minImpactVelocity = 1.5f;

    [Header("🔊 Audio Settings")]
    [Tooltip("충돌 사운드가 나올 AudioSource (비워두면 자동 찾기)")]
    [SerializeField] private AudioSource audioSource;

    [Tooltip("소화기/물체가 바닥에 쾅! 부딪힐 때 날 사운드 파일")]
    [SerializeField] private AudioClip impactSFX;

    private bool hasLanded = false; // 중복 소음 및 중복 사운드 방지 플래그

    private void Awake()
    {
        // AudioSource 자동 캐싱
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        // 이미 한번 바닥에 떨어져 소음을 냈다면 중복 발생 차단
        if (hasLanded) return;

        // 일정 속도 이상으로 쾅 부딪혔을 때만 소음 및 사운드 발생!
        if (collision.relativeVelocity.magnitude >= minImpactVelocity)
        {
            hasLanded = true;

            // 🎯 1. 주변 적 AI 유인 소음 발생!
            EmitNoise(transform.position);

            // 🎯 2. 충돌 속도에 맞춰 찰진 3D SFX 사운드 출력!
            if (impactSFX != null)
            {
                // 충돌 속도가 빠를수록 소리가 커지는 생동감 연출 (최대 볼륨 1.0)
                float impactVolume = Mathf.Clamp01(collision.relativeVelocity.magnitude / 10f);

                if (audioSource != null)
                {
                    audioSource.PlayOneShot(impactSFX, impactVolume);
                }
                else
                {
                    // AudioSource가 없을 경우 3D 위치에서 직접 사운드 출력!
                    AudioSource.PlayClipAtPoint(impactSFX, transform.position, impactVolume);
                }
            }

            Debug.Log($"💥 [{gameObject.name}] 소음 및 사운드 발생! 위치: {transform.position}");
        }
    }

    /// <summary>
    /// 지정된 위치에서 OverlapSphere로 주변 적을 찾아 소음을 전달함
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