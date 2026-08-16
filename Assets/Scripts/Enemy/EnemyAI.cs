using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 적 AI 전담 모듈. 순찰/의심/추격 상태를 관리하며,
/// 소음 감지, 거리 비례 발각 속도, 주변 동료에게 경보 전파, 상태별 사운드 재생을 처리합니다.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(FieldOfView))]
public class EnemyAI : MonoBehaviour
{
    public enum State { Patrol, Suspicious, Alerted }

    [Header("Current State")]
    [SerializeField] private State currentState = State.Patrol;

    [Header("Patrol Settings")]
    [SerializeField] private Transform[] waypoints;
    [SerializeField] private float patrolSpeed = 2.0f;
    [Tooltip("🎯 웨이포인트 도착 시 멈춰서 정찰 대기하는 시간 (초)")]
    [SerializeField] private float patrolWaitTime = 2.5f;

    [Header("Suspicious Settings")]
    [SerializeField] private float suspiciousSpeed = 2.8f;
    [SerializeField] private float suspiciousThreshold = 30f;

    [Header("Chase Settings")]
    [SerializeField] private float chaseSpeed = 4.5f;
    [SerializeField] private float alertMemoryTime = 4.0f; // 시야 이탈 후 수색 전환 대기시간 (4초)
    [SerializeField] private float repathRate = 0.15f;     // 0.15초마다 경로 재계산

    [Header("Detection Gauge Settings")]
    [SerializeField] private float currentDetectionGauge = 0f;
    [SerializeField] private float gaugeDecaySpeed = 20f;

    [Header("🎯 Distance Detection Multiplier Settings")]
    [SerializeField] private float maxDetectionDistance = 10.0f;
    [SerializeField] private float maxDistanceMultiplier = 1.5f;
    [SerializeField] private float minDistanceMultiplier = 0.5f;

    [Header("🎯 Alert Propagation Settings")]
    [SerializeField] private float alertPropagationRadius = 12.0f;
    [SerializeField] private LayerMask enemyLayer;

    [Header("Stationary Guard Sweep Settings")]
    [SerializeField] private bool enableSweep = true;
    [SerializeField] private float sweepAngle = 45f;
    [SerializeField] private float sweepSpeed = 1.5f;

    [Header("Combat Reference")]
    [SerializeField] private EnemyCombat enemyCombat; // EnemyCombat 연동 참조

    [Header("🔊 State Audio Settings")]
    [Tooltip("사운드가 출력될 AudioSource (비워두면 자동 찾기)")]
    [SerializeField] private AudioSource audioSource;

    [Space(10)]
    [Tooltip("의심(Suspicious) 상태 최초 진입 SFX 및 개별 음량")]
    [SerializeField] private AudioClip suspiciousStartSFX;
    [Range(0f, 1f)][SerializeField] private float suspiciousStartVolume = 1.0f;

    [Space(5)]
    [Tooltip("의심 상태 우우웅~ 루프 SFX 및 개별 음량")]
    [SerializeField] private AudioClip suspiciousLoopSFX;
    [Range(0f, 1f)][SerializeField] private float suspiciousLoopVolume = 0.7f;

    [Space(5)]
    [Tooltip("발각(Alerted) 상태 비상 SFX 및 개별 음량")]
    [SerializeField] private AudioClip alertSFX;
    [Range(0f, 1f)][SerializeField] private float alertVolume = 1.0f;

    private AudioSource loopAudioSource;

    private NavMeshAgent agent;
    private FieldOfView fov;
    private Transform playerTransform;
    private int currentWaypointIndex = 0;
    private Vector3 lastKnownPosition;
    private Vector3 lastPlayerDirection;
    private bool isInvestigating = false;
    private bool isFromAlerted = false;
    private float lostSightTimer = 0f;
    private float repathTimer = 0f;

    private bool isPatrolWaiting = false;
    private float patrolWaitTimer = 0f;

    // 외부 참조용 프로퍼티
    public State CurrentState => currentState;
    public bool IsAlerted => currentState == State.Alerted;
    public float CurrentDetectionGauge => currentDetectionGauge;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        fov = GetComponent<FieldOfView>();

        if (enemyCombat == null)
            enemyCombat = GetComponent<EnemyCombat>();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        loopAudioSource = gameObject.AddComponent<AudioSource>();
        loopAudioSource.spatialBlend = 1.0f; // 3D 입체 사운드
        loopAudioSource.loop = true;
        loopAudioSource.playOnAwake = false;
    }

    private void Start()
    {
        GameObject p = GameObject.FindWithTag("Player");
        if (p != null) playerTransform = p.transform;

        agent.isStopped = false;
        agent.updateRotation = true;
        agent.angularSpeed = 1000f;
        agent.acceleration = 40f;
        agent.autoBraking = false;

        if (waypoints != null && waypoints.Length == 1 && waypoints[0] != null)
        {
            Vector3 dir = (waypoints[0].position - transform.position).normalized;
            dir.y = 0;

            if (dir != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(dir);
            }
        }

        SetState(State.Patrol);
    }

    private void Update()
    {
        UpdateDetectionGauge();
        UpdateBehavior();
        UpdateSuspiciousLoopAudio();
    }

    private void UpdateDetectionGauge()
    {
        if (fov.CanSeePlayer && playerTransform != null)
        {
            Vector3 moveDir = (playerTransform.position - transform.position).normalized;
            if (moveDir != Vector3.zero) lastPlayerDirection = moveDir;

            lastKnownPosition = playerTransform.position;

            float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
            float distanceNormalized = Mathf.Clamp01(1.0f - (distanceToPlayer / maxDetectionDistance));
            float distanceMultiplier = Mathf.Lerp(minDistanceMultiplier, maxDistanceMultiplier, distanceNormalized);

            float baseDelta = fov.CalculateDetectionDelta();
            currentDetectionGauge = Mathf.Clamp(currentDetectionGauge + (baseDelta * distanceMultiplier), 0f, 100f);
            lostSightTimer = 0f;
        }
        else
        {
            if (currentState == State.Alerted)
            {
                lostSightTimer += Time.deltaTime;

                if (lostSightTimer >= alertMemoryTime)
                {
                    currentDetectionGauge = suspiciousThreshold + 5f;
                    lostSightTimer = 0f;
                    isFromAlerted = true;
                    SetState(State.Suspicious);
                    return;
                }
            }
            else if (!isInvestigating)
            {
                currentDetectionGauge = Mathf.Clamp(currentDetectionGauge - (gaugeDecaySpeed * Time.deltaTime), 0f, 100f);
            }
        }

        if (currentDetectionGauge >= 100f && currentState != State.Alerted)
        {
            SetState(State.Alerted);
            ScoreManager.Instance?.AddDetection();
        }
        else if (currentDetectionGauge >= suspiciousThreshold && currentDetectionGauge < 100f && currentState == State.Patrol)
        {
            isFromAlerted = false;
            SetState(State.Suspicious);
        }
        else if (currentDetectionGauge < suspiciousThreshold && currentState == State.Suspicious && !isInvestigating)
        {
            SetState(State.Patrol);
        }
    }

    private void SetState(State newState)
    {
        currentState = newState;

        StopAllCoroutines();
        agent.isStopped = false;
        isInvestigating = false;

        isPatrolWaiting = false;
        patrolWaitTimer = 0f;

        switch (currentState)
        {
            case State.Patrol:
                isFromAlerted = false;
                agent.speed = patrolSpeed;
                agent.stoppingDistance = 0.1f;

                StopSuspiciousLoopAudio(true);

                if (waypoints != null && waypoints.Length > 0)
                {
                    agent.SetDestination(waypoints[currentWaypointIndex].position);
                }
                break;

            case State.Suspicious:
                agent.speed = suspiciousSpeed;
                agent.stoppingDistance = 0.5f;

                if (!isFromAlerted && audioSource != null && suspiciousStartSFX != null)
                {
                    audioSource.PlayOneShot(suspiciousStartSFX, suspiciousStartVolume);
                }

                StartCoroutine(SuspiciousRoutine());
                break;

            case State.Alerted:
                isFromAlerted = false;
                agent.speed = chaseSpeed;

                if (enemyCombat != null)
                {
                    agent.stoppingDistance = enemyCombat.MeleeAttackRange * 0.8f;
                }
                else
                {
                    agent.stoppingDistance = 1.3f;
                }

                currentDetectionGauge = 100f;
                lostSightTimer = 0f;
                repathTimer = repathRate;

                StopSuspiciousLoopAudio(true);

                if (audioSource != null && alertSFX != null)
                {
                    audioSource.PlayOneShot(alertSFX, alertVolume);
                }

                AlertNearbyEnemies();
                break;
        }
    }

    private void UpdateSuspiciousLoopAudio()
    {
        if (suspiciousLoopSFX == null || loopAudioSource == null) return;

        if (currentState != State.Alerted && currentDetectionGauge >= suspiciousThreshold)
        {
            if (!loopAudioSource.isPlaying)
            {
                loopAudioSource.clip = suspiciousLoopSFX;
                loopAudioSource.volume = 0f;
                loopAudioSource.Play();
            }

            float normalizedGauge = Mathf.InverseLerp(suspiciousThreshold, 100f, currentDetectionGauge);
            float targetVolume = Mathf.Lerp(0.25f, 1.0f, normalizedGauge) * suspiciousLoopVolume;

            loopAudioSource.volume = Mathf.Lerp(loopAudioSource.volume, targetVolume, Time.deltaTime * 10f);
        }
        else
        {
            StopSuspiciousLoopAudio(false);
        }
    }

    private void StopSuspiciousLoopAudio(bool immediate = false)
    {
        if (loopAudioSource == null || !loopAudioSource.isPlaying) return;

        if (immediate)
        {
            loopAudioSource.volume = 0f;
            loopAudioSource.Stop();
        }
        else
        {
            loopAudioSource.volume = Mathf.Lerp(loopAudioSource.volume, 0f, Time.deltaTime * 12f);

            if (loopAudioSource.volume <= 0.02f)
            {
                loopAudioSource.Stop();
            }
        }
    }

    private void AlertNearbyEnemies()
    {
        Collider[] hitEnemies = Physics.OverlapSphere(transform.position, alertPropagationRadius, enemyLayer);

        foreach (Collider col in hitEnemies)
        {
            EnemyAI ally = col.GetComponentInParent<EnemyAI>();
            if (ally != null && ally != this)
            {
                ally.OnAlertedByAlly(lastKnownPosition);
            }
        }
    }

    public void OnAlertedByAlly(Vector3 targetPos)
    {
        if (currentState == State.Alerted) return;

        lastKnownPosition = targetPos;
        currentDetectionGauge = 100f;

        SetState(State.Alerted);
    }

    private void UpdateBehavior()
    {
        switch (currentState)
        {
            case State.Patrol:
                UpdatePatrol();
                break;
            case State.Suspicious:
                break;
            case State.Alerted:
                UpdateChase();
                break;
        }
    }

    private void UpdatePatrol()
    {
        if (waypoints == null || waypoints.Length == 0) return;

        if (waypoints.Length == 1)
        {
            if (!agent.pathPending && agent.remainingDistance <= (agent.stoppingDistance + 0.3f))
            {
                Vector3 baseDir = (waypoints[0].position - transform.position).normalized;
                baseDir.y = 0;

                if (baseDir != Vector3.zero)
                {
                    Quaternion baseRotation = Quaternion.LookRotation(baseDir);

                    if (enableSweep)
                    {
                        float currentAngle = Mathf.Sin(Time.time * sweepSpeed) * sweepAngle;
                        Quaternion sweepRotation = baseRotation * Quaternion.Euler(0f, currentAngle, 0f);

                        transform.rotation = Quaternion.Slerp(
                            transform.rotation,
                            sweepRotation,
                            Time.deltaTime * 5f
                        );
                    }
                    else
                    {
                        if (Quaternion.Angle(transform.rotation, baseRotation) > 0.1f)
                        {
                            transform.rotation = Quaternion.Slerp(
                                transform.rotation,
                                baseRotation,
                                Time.deltaTime * 5f
                            );
                        }
                    }
                }
            }
            return;
        }

        if (isPatrolWaiting)
        {
            patrolWaitTimer += Time.deltaTime;

            if (patrolWaitTimer >= patrolWaitTime)
            {
                isPatrolWaiting = false;
                patrolWaitTimer = 0f;
                agent.isStopped = false;

                currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
                agent.SetDestination(waypoints[currentWaypointIndex].position);
            }
            return;
        }

        if (!agent.pathPending && agent.remainingDistance <= (agent.stoppingDistance + 0.3f))
        {
            isPatrolWaiting = true;
            patrolWaitTimer = 0f;
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }
    }

    private IEnumerator SuspiciousRoutine()
    {
        isInvestigating = true;

        if (!isFromAlerted)
        {
            agent.isStopped = true;
            Vector3 dirToLKP = (lastKnownPosition - transform.position).normalized;
            dirToLKP.y = 0;
            if (dirToLKP != Vector3.zero)
            {
                Quaternion targetRot = Quaternion.LookRotation(dirToLKP);
                yield return FastRotateRoutine(targetRot, 0.15f);
            }
            yield return new WaitForSeconds(0.85f);
            agent.isStopped = false;
        }

        agent.SetDestination(lastKnownPosition);

        while (agent.pathPending || agent.remainingDistance > 0.5f)
        {
            yield return null;
        }

        agent.isStopped = true;

        Quaternion baseRot = transform.rotation;

        yield return FastRotateRoutine(baseRot * Quaternion.Euler(0, -45f, 0), 0.3f);
        yield return new WaitForSeconds(1.7f);

        yield return FastRotateRoutine(baseRot * Quaternion.Euler(0, 45f, 0), 0.3f);
        yield return new WaitForSeconds(1.7f);

        yield return FastRotateRoutine(baseRot, 0.2f);
        yield return new WaitForSeconds(0.3f);

        agent.isStopped = false;
        isInvestigating = false;
        isFromAlerted = false;

        currentDetectionGauge = 0f;
        SetState(State.Patrol);
    }

    private IEnumerator FastRotateRoutine(Quaternion targetRot, float duration)
    {
        Quaternion startRot = transform.rotation;
        float time = 0f;
        while (time < duration)
        {
            transform.rotation = Quaternion.Slerp(startRot, targetRot, time / duration);
            time += Time.deltaTime;
            yield return null;
        }
        transform.rotation = targetRot;
    }

    private void UpdateChase()
    {
        if (playerTransform == null) return;

        float distToPlayer = Vector3.Distance(transform.position, playerTransform.position);
        float attackRange = (enemyCombat != null) ? enemyCombat.MeleeAttackRange : 2.0f;
        bool isCombatAttacking = (enemyCombat != null && enemyCombat.IsAttacking);

        if (distToPlayer <= attackRange || isCombatAttacking)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;

            Vector3 lookDir = (playerTransform.position - transform.position).normalized;
            lookDir.y = 0;
            if (lookDir != Vector3.zero)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * 10f);
            }
        }
        else
        {
            agent.isStopped = false;

            repathTimer += Time.deltaTime;
            if (repathTimer >= repathRate)
            {
                repathTimer = 0f;

                if (fov.CanSeePlayer)
                {
                    agent.SetDestination(playerTransform.position);
                }
                else
                {
                    Vector3 predictedPos = lastKnownPosition + lastPlayerDirection * 2.5f;

                    if (NavMesh.SamplePosition(predictedPos, out NavMeshHit hit, 3.0f, NavMesh.AllAreas))
                    {
                        agent.SetDestination(hit.position);
                    }
                    else
                    {
                        agent.SetDestination(lastKnownPosition);
                    }
                }
            }
        }
    }

    public void OnHearNoise(Vector3 noisePosition)
    {
        if (currentState == State.Alerted) return;

        lastKnownPosition = noisePosition;

        if (currentDetectionGauge < 35f)
        {
            currentDetectionGauge = 35f;
        }

        SetState(State.Suspicious);

        if (agent != null && agent.isOnNavMesh)
        {
            agent.speed = patrolSpeed * 1.2f;
            agent.SetDestination(noisePosition);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, alertPropagationRadius);
    }
}