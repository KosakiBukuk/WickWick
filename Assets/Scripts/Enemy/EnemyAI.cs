using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// [최종 버그 수정완료] 추격 끊김 해결 & 순찰(Patrol) 웨이포인트 정상 작동 AI
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

    [Header("Stationary Guard Sweep Settings")]
    [Tooltip("체크 시 제자리에서 좌우로 고개를 돌리며 정찰합니다. (해제 시 정면 고정)")]
    [SerializeField] private bool enableSweep = true; // 🎯 좌우 정찰 온/오프 스위치!

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

        // 🎯 [핵심] 웨이포인트가 1개일 때, 시작하자마자 해당 위치를 바라보도록 초기 회전 세팅
        if (waypoints != null && waypoints.Length == 1 && waypoints[0] != null)
        {
            Vector3 dir = (waypoints[0].position - transform.position).normalized;
            dir.y = 0; // 고개가 위아래로 꺾이지 않도록 평면 보정!

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
        if (fov.CanSeePlayer)
        {
            Vector3 moveDir = (playerTransform.position - transform.position).normalized;
            if (moveDir != Vector3.zero) lastPlayerDirection = moveDir;

            lastKnownPosition = playerTransform.position;
            currentDetectionGauge = Mathf.Clamp(currentDetectionGauge + fov.CalculateDetectionDelta(), 0f, 100f);
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
                agent.stoppingDistance = 0.1f; // 🎯 순찰 시에는 0.1m까지 확실히 다가가도록 설정!
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
                agent.stoppingDistance = 0.8f; // 🎯 추격 시 플레이어 몸과 비비지 않도록 정지거리 확보
                currentDetectionGauge = 100f;
                lostSightTimer = 0f;
                repathTimer = repathRate;
                break;
        }
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

    /// <summary>
    /// 🎯 [수정 완료] enableSweep 체크 유무에 따라 좌우 정찰 / 정면 고정 선택 가능!
    /// </summary>
    private void UpdatePatrol()
    {
        if (waypoints == null || waypoints.Length == 0) return;

        // 🎯 1개짜리 제자리 문지기 웨이포인트일 때
        if (waypoints.Length == 1)
        {
            if (!agent.pathPending && agent.remainingDistance <= (agent.stoppingDistance + 0.3f))
            {
                // 기준이 되는 기본 정면 방향 계산 (웨이포인트 위치 바라보기)
                Vector3 baseDir = (waypoints[0].position - transform.position).normalized;
                baseDir.y = 0; // 평면 보정

                if (baseDir != Vector3.zero)
                {
                    Quaternion baseRotation = Quaternion.LookRotation(baseDir);

                    // 🎯 [핵심] enableSweep 이 체크되어 있을 때만 좌우로 두리번거림!
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
                    // 🎯 enableSweep 체크가 해제되어 있으면 지정된 위치만 묵직하게 고정 감시!
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

        // 🎯 2개 이상 순찰형 웨이포인트일 때
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
}