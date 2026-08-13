using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// [최종 완결판] 추격 끊김 해결, 순찰 지점 대기/좌우정찰, 소음 감지, 
/// 거리 비례 발각 속도 가속 & 주변 동료 적 비상 전파 & 게이지 비례 사운드 연동 통합 AI
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

    // ========================================================================
    // 🔊 [🔊 신규 사운드 설정 파트]
    // ========================================================================
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

    // 루프 전용 별도 AudioSource (단발성 SFX와 루프음이 서로 안 씹히도록 처리!)
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

    public State CurrentState => currentState;
    public float CurrentDetectionGauge => currentDetectionGauge;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        fov = GetComponent<FieldOfView>();

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
        UpdateSuspiciousLoopAudio(); // 🔊 30% 이상 시 실시간 음량 루프 제어!
    }

    private void UpdateDetectionGauge()
    {
        // 1. 시야 내 감지 중
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
        // 2. 시야 차단
        else
        {
            if (currentState == State.Alerted)
            {
                lostSightTimer += Time.deltaTime;

                if (lostSightTimer >= alertMemoryTime)
                {
                    Debug.Log($"❓ [{gameObject.name}] 4초간 놓침! 기둥 뒤 수색(Suspicious) 전환!");
                    currentDetectionGauge = suspiciousThreshold + 5f;
                    lostSightTimer = 0f;
                    isFromAlerted = true; // 🎯 Alerted 출신 플래그 ON!
                    SetState(State.Suspicious);
                    return;
                }
            }
            else if (!isInvestigating)
            {
                currentDetectionGauge = Mathf.Clamp(currentDetectionGauge - (gaugeDecaySpeed * Time.deltaTime), 0f, 100f);
            }
        }

        // FSM 상태 전환
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
        Debug.Log($"🤖 [{gameObject.name}] 상태 전환: {newState}");

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

                StopSuspiciousLoopAudio();

                if (waypoints != null && waypoints.Length > 0)
                {
                    agent.SetDestination(waypoints[currentWaypointIndex].position);
                }
                break;

            case State.Suspicious:
                agent.speed = suspiciousSpeed;
                agent.stoppingDistance = 0.5f;

                // 🎯 [원칙 1] Alerted 이후 다시 돌아올 때는 최초 의심 사운드가 안 나도록 차단! (!isFromAlerted)
                if (!isFromAlerted && audioSource != null && suspiciousStartSFX != null)
                {
                    audioSource.PlayOneShot(suspiciousStartSFX, suspiciousStartVolume);
                }

                StartCoroutine(SuspiciousRoutine());
                break;

            case State.Alerted:
                isFromAlerted = false;
                agent.speed = chaseSpeed;
                agent.stoppingDistance = 0.8f;
                currentDetectionGauge = 100f;
                lostSightTimer = 0f;
                repathTimer = repathRate;

                StopSuspiciousLoopAudio();

                if (audioSource != null && alertSFX != null)
                {
                    audioSource.PlayOneShot(alertSFX, alertVolume);
                }

                AlertNearbyEnemies();
                break;
        }
    }

    // ========================================================================
    // 🔊 [🔊 게이지 비례 실시간 볼륨 조절 루프 사운드 제어]
    // ========================================================================
    private void UpdateSuspiciousLoopAudio()
    {
        if (suspiciousLoopSFX == null || loopAudioSource == null) return;

        // 🎯 [원칙 2] 게이지가 30%(suspiciousThreshold) 이상이고 Suspicious 상태일 때 계속 우우웅~ 소리 출력!
        if (currentState == State.Suspicious && currentDetectionGauge >= suspiciousThreshold)
        {
            // 1. 소리가 재생 중이지 않다면 루프 시작
            if (!loopAudioSource.isPlaying)
            {
                loopAudioSource.clip = suspiciousLoopSFX;
                loopAudioSource.volume = 0f;
                loopAudioSource.Play();
            }

            // 2. 게이지 차오름 수치(30% ~ 100%)에 비례하는 볼륨 실시간 계산!
            float targetVolume = Mathf.Clamp01(currentDetectionGauge / 100f) * suspiciousLoopVolume;

            // 3. 부드러운 음량 전환
            loopAudioSource.volume = Mathf.Lerp(loopAudioSource.volume, targetVolume, Time.deltaTime * 8f);
        }
        else
        {
            // 게이지가 30% 미만으로 떨어지거나 다른 상태가 되면 감쇄 후 정지
            StopSuspiciousLoopAudio();
        }
    }

    private void StopSuspiciousLoopAudio()
    {
        if (loopAudioSource != null && loopAudioSource.isPlaying)
        {
            loopAudioSource.volume = Mathf.Lerp(loopAudioSource.volume, 0f, Time.deltaTime * 10f);

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
        Debug.Log($"🚨 [{gameObject.name}] 동료의 비상 라디오를 수신! 함께 Alerted 상태로 전환!");
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
                Debug.Log($"🚶 [{gameObject.name}] 정찰 대기 완료! 다음 웨이포인트({currentWaypointIndex})로 출발합니다.");
            }
            return;
        }

        if (!agent.pathPending && agent.remainingDistance <= (agent.stoppingDistance + 0.3f))
        {
            isPatrolWaiting = true;
            patrolWaitTimer = 0f;
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            Debug.Log($"🛑 [{gameObject.name}] 웨이포인트({currentWaypointIndex}) 도착! {patrolWaitTime}초 동안 정찰 대기 시작.");
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
        Debug.Log($"🔍 [{gameObject.name}] LKP 도착! 4.5초간 주변 수색 중...");

        Quaternion baseRot = transform.rotation;

        yield return FastRotateRoutine(baseRot * Quaternion.Euler(0, -45f, 0), 0.3f);
        yield return new WaitForSeconds(1.7f);

        yield return FastRotateRoutine(baseRot * Quaternion.Euler(0, 45f, 0), 0.3f);
        yield return new WaitForSeconds(1.7f);

        yield return FastRotateRoutine(baseRot, 0.2f);
        yield return new WaitForSeconds(0.3f);

        agent.isStopped = false;
        isInvestigating = false;
        isFromAlerted = false; // 수색 종료 후 Patrol 복귀 시 세팅 초기화

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

        Debug.Log($"👂 [{gameObject.name}] 소음을 감지함! 수색 지점: {noisePosition}");
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, alertPropagationRadius);
    }
}