using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// [최종 완결판] 추격 끊김 해결, 순찰/좌우정찰, 소음 감지, 
/// 거리 비례 발각 속도 가속 & 주변 동료 적 비상 전파(Alert Propagation) 통합 AI
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

    [Header("Suspicious Settings")]
    [SerializeField] private float suspiciousSpeed = 2.8f;
    [SerializeField] private float suspiciousThreshold = 30f;

    [Header("Chase Settings")]
    [SerializeField] private float chaseSpeed = 4.5f;
    [SerializeField] private float alertMemoryTime = 4.0f; // 시야 이탈 후 수색 전환 대기시간 (4초)
    [SerializeField] private float repathRate = 0.15f;     // 0.15초마다 경로 재계산 (추격 끊김 방지)

    [Header("Detection Gauge Settings")]
    [SerializeField] private float currentDetectionGauge = 0f;
    [SerializeField] private float gaugeDecaySpeed = 20f;

    [Header("🎯 [신규 기능 1] Distance Detection Multiplier Settings")]
    [Tooltip("발각 게이지 가속 연산용 최대 인식 거리 (이 거리보다 멀어지면 최소 가속율 적용)")]
    [SerializeField] private float maxDetectionDistance = 10.0f;

    [Tooltip("플레이어가 바짝 붙어있을 때 적용할 최대 발각 속도 배수 (예: 3배 빠르게 차오름!)")]
    [SerializeField] private float maxDistanceMultiplier = 1.5f;

    [Tooltip("플레이어가 인식 거리 끝자락에 있을 때 적용할 최소 발각 속도 배수")]
    [SerializeField] private float minDistanceMultiplier = 0.5f;

    [Header("🎯 [신규 기능 2] Alert Propagation Settings (동료 지원 요청)")]
    [Tooltip("경계(Alerted) 상태 전환 시 주변 동료들에게 비상을 알릴 전파 범위")]
    [SerializeField] private float alertPropagationRadius = 12.0f;

    [Tooltip("적 AI 오브젝트가 속한 Layer (Physics.OverlapSphere 검사용)")]
    [SerializeField] private LayerMask enemyLayer;

    [Header("Stationary Guard Sweep Settings")]
    [Tooltip("체크 시 제자리에서 좌우로 고개를 돌리며 정찰합니다. (해제 시 정면 고정)")]
    [SerializeField] private bool enableSweep = true; // 좌우 정찰 온/오프 스위치!

    [Tooltip("좌우로 회전할 최대 각도 (예: 45도 지정 시 -45도 ~ +45도 회전)")]
    [SerializeField] private float sweepAngle = 45f;

    [Tooltip("고개를 돌리는 회전 속도")]
    [SerializeField] private float sweepSpeed = 1.5f;

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

    public State CurrentState => currentState;
    public float CurrentDetectionGauge => currentDetectionGauge;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        fov = GetComponent<FieldOfView>();
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

        // 웨이포인트가 1개일 때, 시작하자마자 해당 위치를 바라보도록 초기 회전 세팅
        if (waypoints != null && waypoints.Length == 1 && waypoints[0] != null)
        {
            Vector3 dir = (waypoints[0].position - transform.position).normalized;
            dir.y = 0; // 평면 보정

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
    }

    private void UpdateDetectionGauge()
    {
        // 1. 시야 내 감지 중
        if (fov.CanSeePlayer && playerTransform != null)
        {
            Vector3 moveDir = (playerTransform.position - transform.position).normalized;
            if (moveDir != Vector3.zero) lastPlayerDirection = moveDir;

            lastKnownPosition = playerTransform.position;

            // 🎯 [거리 비례 차등 발각 게이지 연산]
            float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

            // 거리가 가까울수록 1.0 (바짝 붙음), 멀어질수록 0.0 (끝자락)
            float distanceNormalized = Mathf.Clamp01(1.0f - (distanceToPlayer / maxDetectionDistance));

            // 거리 비율에 따라 최소 배수(0.5x) ~ 최대 배수(3.0x) 보간!
            float distanceMultiplier = Mathf.Lerp(minDistanceMultiplier, maxDistanceMultiplier, distanceNormalized);

            float baseDelta = fov.CalculateDetectionDelta();
            currentDetectionGauge = Mathf.Clamp(currentDetectionGauge + (baseDelta * distanceMultiplier), 0f, 100f);
            lostSightTimer = 0f;
        }
        // 2. 시야 차단 (기둥/벽 뒤로 숨음)
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

        switch (currentState)
        {
            case State.Patrol:
                isFromAlerted = false;
                agent.speed = patrolSpeed;
                agent.stoppingDistance = 0.1f;
                if (waypoints != null && waypoints.Length > 0)
                {
                    agent.SetDestination(waypoints[currentWaypointIndex].position);
                }
                break;

            case State.Suspicious:
                agent.speed = suspiciousSpeed;
                agent.stoppingDistance = 0.5f;
                StartCoroutine(SuspiciousRoutine());
                break;

            case State.Alerted:
                isFromAlerted = false;
                agent.speed = chaseSpeed;
                agent.stoppingDistance = 0.8f;
                currentDetectionGauge = 100f;
                lostSightTimer = 0f;
                repathTimer = repathRate;

                // 🎯 [핵심] 주변 동료 적들에게 비상 신호 전파!
                AlertNearbyEnemies();
                break;
        }
    }

    /// <summary>
    /// 🎯 [신규 기능 2] 주변 일정 거리 내의 동료 적들에게 비상 전파!
    /// </summary>
    private void AlertNearbyEnemies()
    {
        Collider[] hitEnemies = Physics.OverlapSphere(transform.position, alertPropagationRadius, enemyLayer);

        foreach (Collider col in hitEnemies)
        {
            EnemyAI ally = col.GetComponentInParent<EnemyAI>();
            if (ally != null && ally != this)
            {
                // 동료 적이 아직 Alerted 상태가 아니라면 즉시 비상 전파 전달!
                ally.OnAlertedByAlly(lastKnownPosition);
            }
        }
    }

    /// <summary>
    /// 🎯 동료 적의 비상 라디오 수신 시 호출되는 함수
    /// </summary>
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

        // 1개짜리 제자리 문지기 웨이포인트일 때
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

        // 2개 이상 순찰형 웨이포인트일 때
        if (!agent.pathPending && agent.remainingDistance <= (agent.stoppingDistance + 0.3f))
        {
            currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
            agent.SetDestination(waypoints[currentWaypointIndex].position);
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

    // 🎯 씬 뷰에서 동료 비상 전파 범위를 눈으로 확인할 수 있는 기즈모
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, alertPropagationRadius);
    }
}