using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// [4단계] 2m 이내 접근 시 단검 베기 공격(선모션 0.25s + 후딜 0.35s) 및 데미지 전달 전담
/// </summary>
[RequireComponent(typeof(EnemyHealth))]
[RequireComponent(typeof(EnemyAI))]
public class EnemyCombat : MonoBehaviour
{
    [Header("🗡️ Melee Attack Settings")]
    [SerializeField] private float meleeAttackRange = 2.0f;    // 공격 사거리 (2m)
    [SerializeField] private float meleeDamage = 25f;          // 단검 데미지
    [SerializeField] private float meleeWindUpTime = 0.25f;    // 칼 치켜올리는 선모션 (0.25초)
    [SerializeField] private float meleeRecoveryTime = 0.35f;  // 자세 복귀 후딜레이 (0.35초)
    [SerializeField] private float meleeCooldown = 1.0f;       // 공격 재사용 쿨타임

    private EnemyAI enemyAI;
    private EnemyHealth enemyHealth;
    private NavMeshAgent agent;
    private Transform playerTransform;

    private float lastAttackTime = 0f;
    private bool isAttacking = false;

    private void Awake()
    {
        enemyAI = GetComponent<EnemyAI>();
        enemyHealth = GetComponent<EnemyHealth>();
        agent = GetComponent<NavMeshAgent>();
    }

    private void Start()
    {
        GameObject p = GameObject.FindWithTag("Player");
        if (p != null) playerTransform = p.transform;
    }

    private void Update()
    {
        if (enemyHealth.IsDead || playerTransform == null) return;

        // 🎯 Alerted(추격) 상태이고 공격 중이 아닐 때 2m 접근 판단
        if (enemyAI.CurrentState == EnemyAI.State.Alerted && !isAttacking)
        {
            float distToPlayer = Vector3.Distance(transform.position, playerTransform.position);

            if (distToPlayer <= meleeAttackRange && Time.time >= lastAttackTime + meleeCooldown)
            {
                StartCoroutine(MeleeAttackRoutine());
            }
        }
    }

    private IEnumerator MeleeAttackRoutine()
    {
        isAttacking = true;
        agent.isStopped = true; // 단검을 내리치는 0.6초 동안만 잠시 정지!

        // 순간적으로 플레이어 정면을 부드럽게 바라봄
        Vector3 dirToPlayer = (playerTransform.position - transform.position).normalized;
        dirToPlayer.y = 0;
        if (dirToPlayer != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(dirToPlayer);
        }

        Debug.Log($"🗡️ [{gameObject.name}] 단검 공격 시도!");
        yield return new WaitForSeconds(meleeWindUpTime);

        // 실시간 명중 판정 (유격 +0.3m)
        float currentDist = Vector3.Distance(transform.position, playerTransform.position);
        if (currentDist <= meleeAttackRange + 0.3f)
        {
            Debug.Log($"💥 [{gameObject.name}] 단검 타격 명중! (데미지: {meleeDamage})");

            // 플레이어 Health 컴포넌트에 데미지 전달
            PlayerHealth playerHealth = playerTransform.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.TakeDamage(meleeDamage);
            }
        }
        else
        {
            Debug.Log($"💨 [{gameObject.name}] 플레이어 회피 성공!");
        }

        yield return new WaitForSeconds(meleeRecoveryTime);

        lastAttackTime = Time.time;
        agent.isStopped = false; // 🎯 공격 종료 즉시 이동 풀기! EnemyAI가 추격을 이어받음
        isAttacking = false;
    }
}