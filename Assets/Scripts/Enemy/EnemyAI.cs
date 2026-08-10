using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// [최종 최적화] 기둥/벽 모퉁이 우회 예측 추격 및 Alerted -> Suspicious 자연스러운 연동 AI
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

    [Header("Detection Gauge Settings")]
    [SerializeField] private float currentDetectionGauge = 0f;
    [SerializeField] private float gaugeDecaySpeed = 20f;

    private NavMeshAgent agent;
    private FieldOfView fov;
    private Transform playerTransform;
    private int currentWaypointIndex = 0;
    private Vector3 lastKnownPosition;
    private Vector3 lastPlayerDirection; // 🎯 플레이어의 마지막 이동 방향
    private bool isInvestigating = false;
    private bool isFromAlerted = false;   // 🎯 Alerted 상태에서 넘어왔는지 여부
    private float lostSightTimer = 0f;

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
        agent.stoppingDistance = 0f;
        agent.autoBraking = false;
        agent.speed = patrolSpeed;

        if (waypoints != null && waypoints.Length > 0)
        {
            agent.SetDestination(waypoints[0].position);
        }
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

                // 4초 동안 놓쳤을 경우에만 수색(Suspicious)으로 전환
                if (lostSightTimer >= alertMemoryTime)
                {
                    Debug.Log($"❓ [{gameObject.name}] 4초간 놓침! 기둥 뒤 수색(Suspicious) 전환!");
                    currentDetectionGauge = suspiciousThreshold + 5f;
                    lostSightTimer = 0f;
                    isFromAlerted = true; // 추격 중 놓침 플래그
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
                agent.speed = patrolSpeed;
                if (waypoints != null && waypoints.Length > 0)
                    agent.SetDestination(waypoints[currentWaypointIndex].position);
                break;

            case State.Suspicious:
                agent.speed = suspiciousSpeed;
                StartCoroutine(SuspiciousRoutine());
                break;

            case State.Alerted:
                agent.speed = chaseSpeed;
                currentDetectionGauge = 100f;
                lostSightTimer = 0f;
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

    private void UpdatePatrol()
    {
        if (waypoints == null || waypoints.Length == 0) return;

        if (!agent.pathPending && agent.remainingDistance <= 0.3f)
        {
            currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
            agent.SetDestination(waypoints[currentWaypointIndex].position);
        }
    }

    private IEnumerator SuspiciousRoutine()
    {
        isInvestigating = true;

        // 🎯 순찰 중 감지 시에만 1초 "어...?" 멈칫 연출! (추격 중 놓쳤을 때는 즉시 이동)
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

        // LKP 위치로 이동
        agent.SetDestination(lastKnownPosition);

        while (agent.pathPending || agent.remainingDistance > 0.5f)
        {
            yield return null;
        }

        // LKP 도착 후 4.5초간 두리번 수색
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

        if (fov.CanSeePlayer)
        {
            agent.SetDestination(playerTransform.position);
        }
        else
        {
            // 🎯 [핵심] 시야가 막혀도 플레이어의 진행 방향으로 2.5m 더 나아가 기둥 뒤를 자연스럽게 돈다!
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

    // EnemyAI.cs 클래스 내부에 아래 메서드를 추가해 줘!

    /// <summary>
    /// 외부 소음(벽돌 낙하 등)을 들었을 때 호출되는 소음 수신 메서드
    /// </summary>
    /// <param name="noisePosition">소음이 발생한 월드 좌표</param>
    public void OnHearNoise(Vector3 noisePosition)
    {
        // 이미 발각(Alerted)되어 전투 중이라면 소음에 신경 쓰지 않음
        if (currentState == State.Alerted) return;

        // 1. 소음 발생 지점으로 수색 목표 위치(LKP) 설정
        lastKnownPosition = noisePosition;

        // 2. 감지 게이지를 Suspicious 진입 임계값(35%) 이상으로 즉시 상향!
        if (currentDetectionGauge < 35f)
        {
            currentDetectionGauge = 35f;
        }

        // 3. FSM 상태를 Suspicious(의심)로 전환
        SetState(State.Suspicious);

        // 4. 소음 발생 지점으로 NavMeshAgent 이동 명령 및 도착 후 4.5초 두리번 수색 실행!
        if (agent != null && agent.isOnNavMesh)
        {
            agent.speed = patrolSpeed * 1.2f; // 의심 수색 속도로 설정 (약 60% 속도)
            agent.SetDestination(noisePosition);
        }

        Debug.Log($"👂 [{gameObject.name}] 소음을 감지함! 수색 지점: {noisePosition}");
    }
}