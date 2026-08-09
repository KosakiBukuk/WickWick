using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 적 체력, 거리별 공격(단검/권총), 조준 선모션 정지, 사격 오차(Accuracy Spread), 사망 및 탄약 드랍 제어
/// </summary>
[RequireComponent(typeof(EnemyAI))]
[RequireComponent(typeof(FieldOfView))]
public class EnemyCombat : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth = 100f;

    [Header("Attack Range Settings")]
    [SerializeField] private float meleeAttackRange = 2.0f;    // 2m 이내 단검
    [SerializeField] private float rangedStartRange = 12.0f;   // 🎯 사격 시작 거리 (12m)
    [SerializeField] private float rangedKeepRange = 16.0f;    // 🎯 사격 유지/취소 거리 (16m)

    [Header("Melee Attack Settings")]
    [SerializeField] private float meleeDamage = 25f;
    [SerializeField] private float meleeCooldown = 1.2f;

    [Header("Ranged Attack Settings")]
    [SerializeField] private float rangedDamage = 15f;
    [SerializeField] private float aimDuration = 0.4f;         // 🎯 사격 전 조준 대기 시간 (0.4초)
    [SerializeField] private float fireRateCooldown = 1.5f;    // 2연사 후 다음 공격까지 쿨타임
    [SerializeField] private float accuracySpread = 0.08f;     // 🎯 사격 오차 범위 (탄착군 분산)
    [SerializeField] private Transform gunMuzzle;

    [Header("Item Drop Settings")]
    [SerializeField] private GameObject ammoItemPrefab;

    private EnemyAI enemyAI;
    private FieldOfView fov;
    private NavMeshAgent agent;
    private PlayerController targetPlayer;

    private float lastMeleeTime = 0f;
    private float lastRangedTime = 0f;
    private bool isDead = false;
    private bool isAimingOrAttacking = false; // 조준/공격 중 중복 실행 방지
    private bool hasTriggeredInitialAlertShot = false; // 최초 발각 멈춤 사격 실행 여부

    public bool IsDead => isDead;
    public float CurrentHealth => currentHealth;

    private void Awake()
    {
        enemyAI = GetComponent<EnemyAI>();
        fov = GetComponent<FieldOfView>();
        agent = GetComponent<NavMeshAgent>();
    }

    private void Start()
    {
        currentHealth = maxHealth;
        targetPlayer = FindObjectOfType<PlayerController>();

        // AI 상태 변경 이벤트 구독 (Alerted에 처음 들어갈 때 멈춤 사격 준비)
        if (enemyAI != null)
        {
            enemyAI.OnStateChanged += HandleStateChanged;
        }
    }

    private void OnDestroy()
    {
        if (enemyAI != null)
        {
            enemyAI.OnStateChanged -= HandleStateChanged;
        }
    }

    private void HandleStateChanged(EnemyAI.State newState)
    {
        if (newState == EnemyAI.State.Alerted)
        {
            hasTriggeredInitialAlertShot = false;
        }
    }

    private void Update()
    {
        if (isDead || targetPlayer == null) return;

        if (enemyAI.CurrentState == EnemyAI.State.Alerted)
        {
            HandleCombatBehavior();
        }
    }

    private void HandleCombatBehavior()
    {
        float distToPlayer = Vector3.Distance(transform.position, targetPlayer.transform.position);

        // 조준 또는 사격 중이 아닐 때 플레이어를 향해 부드럽게 회전
        if (!isAimingOrAttacking)
        {
            Vector3 lookDir = (targetPlayer.transform.position - transform.position).normalized;
            lookDir.y = 0;
            if (lookDir != Vector3.zero)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * 8f);
            }
        }

        // 🎯 1. 최초 발각 시 제자리 1초 멈춤 경고 사격!
        if (!hasTriggeredInitialAlertShot && fov.CanSeePlayer)
        {
            StartCoroutine(InitialAlertShotRoutine());
            return;
        }

        if (isAimingOrAttacking) return;

        // 🎯 2. 근접 단검 공격 (2m 이내)
        if (distToPlayer <= meleeAttackRange)
        {
            if (Time.time >= lastMeleeTime + meleeCooldown)
            {
                StartCoroutine(PerformMeleeAttackRoutine());
            }
        }
        // 🎯 3. 원거리 권총 사격 (12m 이내 진입 시)
        else if (distToPlayer <= rangedStartRange && fov.CanSeePlayer)
        {
            if (Time.time >= lastRangedTime + fireRateCooldown)
            {
                StartCoroutine(PerformAimAndRangedBurstRoutine());
            }
        }
    }

    #region Attack Routines

    /// <summary>
    /// 최초 발각 시 1.0초간 정지 후 경고 사격 1발 발사
    /// </summary>
    private IEnumerator InitialAlertShotRoutine()
    {
        hasTriggeredInitialAlertShot = true;
        isAimingOrAttacking = true;
        agent.isStopped = true;

        Debug.Log($"🚨 [{gameObject.name}] 발각! 제자리에 멈춰서 조준 시작!");
        yield return new WaitForSeconds(0.6f);

        FireSingleShotWithSpread(); // 경고 사격 1발

        yield return new WaitForSeconds(0.4f);
        agent.isStopped = false;
        isAimingOrAttacking = false;
    }

    /// <summary>
    /// 근접 단검 공격 루틴
    /// </summary>
    private IEnumerator PerformMeleeAttackRoutine()
    {
        isAimingOrAttacking = true;
        lastMeleeTime = Time.time;

        Debug.Log($"🗡️ [{gameObject.name}] 쉭! 단검 베기 공격! (데미지: {meleeDamage})");
        yield return new WaitForSeconds(0.3f);

        isAimingOrAttacking = false;
    }

    /// <summary>
    /// 이동 정지 ➔ 0.4초 조준(중간 이탈 검사) ➔ 권총 2연사 탕! 탕! ➔ 이동 재개
    /// </summary>
    private IEnumerator PerformAimAndRangedBurstRoutine()
    {
        isAimingOrAttacking = true;
        agent.isStopped = true; // 🎯 사격 조준 중 이동 완전 정지!

        Debug.Log($"🎯 [{gameObject.name}] 멈춰서 0.4초간 조준 중...");

        // 0.4초 조준 시간 동안 플레이어가 벽 뒤로 숨거나 16m 밖으로 나가면 조준 취소!
        float timer = 0f;
        while (timer < aimDuration)
        {
            timer += Time.deltaTime;
            float currentDist = Vector3.Distance(transform.position, targetPlayer.transform.position);

            // 조준 취소 조건 (벽 뒤로 숨거나 16m 유지 사거리 이탈)
            if (!fov.CanSeePlayer || currentDist > rangedKeepRange)
            {
                Debug.Log($"❌ [{gameObject.name}] 조준 취소! (시야 차단 또는 16m 이탈)");
                agent.isStopped = false;
                isAimingOrAttacking = false;
                yield break;
            }

            // 조준 중 플레이어를 부드럽게 바라봄
            Vector3 lookDir = (targetPlayer.transform.position - transform.position).normalized;
            lookDir.y = 0;
            if (lookDir != Vector3.zero)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * 10f);
            }

            yield return null;
        }

        // 🎯 0.4초 조준 완료! 2연사 사격! (탕! ... 탕!)
        FireSingleShotWithSpread();
        yield return new WaitForSeconds(0.15f);
        FireSingleShotWithSpread();

        lastRangedTime = Time.time;
        agent.isStopped = false; // 이동 재개
        isAimingOrAttacking = false;
    }

    /// <summary>
    /// 명중률 오차(Accuracy Spread)가 적용된 단발 사격
    /// </summary>
    private void FireSingleShotWithSpread()
    {
        Vector3 fireOrigin = gunMuzzle != null ? gunMuzzle.position : transform.position + Vector3.up * 1.2f;
        Vector3 targetPos = targetPlayer.transform.position + Vector3.up * 1.0f;
        Vector3 baseDir = (targetPos - fireOrigin).normalized;

        // 🎯 [핵심] 랜덤 오차(Accuracy Spread) 적용
        Vector3 spreadOffset = Random.insideUnitSphere * accuracySpread;
        Vector3 finalDir = (baseDir + spreadOffset).normalized;

        // 레이캐스트 및 시각화 디버그 선
        if (Physics.Raycast(fireOrigin, finalDir, out RaycastHit hit, rangedKeepRange))
        {
            Debug.DrawLine(fireOrigin, hit.point, Color.red, 0.25f);

            if (hit.transform.CompareTag("Player"))
            {
                Debug.Log($"💥 [{gameObject.name}] 탕! 권총 명중! (데미지: {rangedDamage})");
            }
            else
            {
                Debug.Log($"💨 [{gameObject.name}] 빗나감! (벽/바닥 피격: {hit.transform.name})");
            }
        }
        else
        {
            Debug.DrawRay(fireOrigin, finalDir * rangedKeepRange, Color.yellow, 0.25f);
        }
    }

    #endregion

    #region Health & Damage

    public void TakeDamage(float damage)
    {
        if (isDead) return;

        currentHealth -= damage;
        Debug.Log($"🩸 [{gameObject.name}] 피격! 남은 체력: {currentHealth}/{maxHealth}");

        if (targetPlayer != null && enemyAI.CurrentState != EnemyAI.State.Alerted)
        {
            enemyAI.TriggerAlert(targetPlayer.transform.position);
        }

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    private void Die()
    {
        isDead = true;
        Debug.Log($"💀 [{gameObject.name}] 적 처치 완료!");

        if (ammoItemPrefab != null)
        {
            Vector3 dropPos = transform.position + Vector3.up * 0.5f;
            Instantiate(ammoItemPrefab, dropPos, Quaternion.identity);
            Debug.Log($"🎁 [{gameObject.name}] AmmoItem 드랍 완료!");
        }

        enemyAI.enabled = false;
        if (agent != null) agent.enabled = false;

        Destroy(gameObject, 0.5f);
    }

    #endregion
}