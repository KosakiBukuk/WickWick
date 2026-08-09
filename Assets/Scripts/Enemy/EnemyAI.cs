using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 감지 게이지 기반 FSM, LKP 수색, 360도 Alerted 추격 및 LKP 4초 대기/35m 이탈 추격 중단 Trigger 제어
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(FieldOfView))]
public class EnemyAI : MonoBehaviour
{
    public enum State { Patrol, Suspicious, Alerted }

    [Header("Current FSM State")]
    [SerializeField] private State currentState = State.Patrol;

    [Header("Patrol Settings")]
    [SerializeField] private Transform[] waypoints;
    [SerializeField] private float patrolSpeed = 2.0f;
    [SerializeField] private float waypointWaitTime = 2.0f;

    [Header("Investigation (Suspicious) Settings")]
    [SerializeField] private float suspiciousSpeedMultiplier = 0.6f;
    [SerializeField] private float maxInvestigateDistance = 4.0f;
    [SerializeField] private float lookAroundAngle = 45.0f;
    [SerializeField] private float lookAroundWaitTime = 2.0f;

    [Header("Chase (Alerted) Settings")]
    [SerializeField] private float chaseSpeed = 4.5f;
    [SerializeField] private float alertBroadcastRadius = 15.0f;
    [SerializeField] private float alertMemoryTime = 4.0f;      // 🎯 LKP 도착 후 대기하는 시간 (4초)
    [SerializeField] private float maxChaseDistance = 35.0f;      // 🎯 최대 추격 허용 거리 (35m)
    [SerializeField] private float proximityAlertRadius = 3.0f;

    [Header("Detection Gauge Master Settings")]
    [SerializeField] private float currentDetectionGauge = 0f;
    [SerializeField] private float gaugeDecaySpeed = 20f;
    [SerializeField] private float suspiciousThreshold = 30f;
    [SerializeField] private float noiseGaugeBoost = 35f;
    [SerializeField] private float suspiciousHoldTime = 6.5f;

    private NavMeshAgent agent;
    private FieldOfView fov;
    private PlayerController targetPlayer;

    private int currentWaypointIndex = 0;
    private bool isWaitingAtWaypoint = false;
    private bool isInvestigatingRoutineActive = false;
    private float suspiciousHoldTimer = 0f;
    private float alertMemoryTimer = 0f;
    private float lostSightTimer = 0f; // 시야에서 놓친 시간 측정용
    private Vector3 lastKnownPosition;

    public event Action<State> OnStateChanged;
    public event Action<float> OnDetectionGaugeChanged;

    public State CurrentState => currentState;
    public float CurrentDetectionGauge => currentDetectionGauge;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        fov = GetComponent<FieldOfView>();
    }

    private void Start()
    {
        targetPlayer = FindObjectOfType<PlayerController>();
        agent.speed = patrolSpeed;

        if (waypoints != null && waypoints.Length > 0)
        {
            agent.SetDestination(waypoints[0].position);
        }
    }

    private void Update()
    {
        HandleNoiseEvents();
        UpdateDetectionGauge();
        UpdateFSMStateByGauge();
        UpdateFSMBehavior();
    }

    #region Detection & Gauge Master Logic

    private void UpdateDetectionGauge()
    {
        if (fov.CanSeePlayer)
        {
            float delta = fov.CalculateDetectionDelta(targetPlayer, currentState);
            currentDetectionGauge = Mathf.Clamp(currentDetectionGauge + delta, 0f, 100f);
            lostSightTimer = 0f;
        }
        else if (currentState == State.Suspicious && isInvestigatingRoutineActive)
        {
            currentDetectionGauge = Mathf.Clamp(currentDetectionGauge - (gaugeDecaySpeed * Time.deltaTime), suspiciousThreshold, 100f);
        }
        else if (suspiciousHoldTimer > 0f)
        {
            suspiciousHoldTimer -= Time.deltaTime;
        }
        else if (currentState != State.Alerted)
        {
            currentDetectionGauge = Mathf.Clamp(currentDetectionGauge - (gaugeDecaySpeed * Time.deltaTime), 0f, 100f);
        }
        else
        {
            // 🎯 Alerted 상태일 때: LKP 도착 후 alertMemoryTimer(4초)가 다 깎여야 게이지 감소 시작!
            if (alertMemoryTimer <= 0f)
            {
                currentDetectionGauge = Mathf.Clamp(currentDetectionGauge - (gaugeDecaySpeed * Time.deltaTime), 0f, 100f);
            }
        }

        OnDetectionGaugeChanged?.Invoke(currentDetectionGauge);
    }

    private void UpdateFSMStateByGauge()
    {
        State newState;

        if (currentDetectionGauge >= 100f)
        {
            newState = State.Alerted;
        }
        else if (currentDetectionGauge >= suspiciousThreshold)
        {
            newState = State.Suspicious;
        }
        else
        {
            newState = State.Patrol;
        }

        if (currentState != newState)
        {
            ChangeState(newState);
        }
    }

    private void ChangeState(State newState)
    {
        currentState = newState;
        Debug.Log($"🤖 [{gameObject.name}] AI 상태 변경: {newState} (게이지: {currentDetectionGauge:F1}%)");
        OnStateChanged?.Invoke(currentState);

        StopAllCoroutines();
        agent.isStopped = false;
        isWaitingAtWaypoint = false;

        switch (currentState)
        {
            case State.Patrol:
                agent.speed = patrolSpeed;
                isInvestigatingRoutineActive = false;
                if (waypoints != null && waypoints.Length > 0)
                {
                    agent.SetDestination(waypoints[currentWaypointIndex].position);
                }
                break;

            case State.Suspicious:
                agent.speed = patrolSpeed * suspiciousSpeedMultiplier;
                StartCoroutine(InvestigateAndLookAroundRoutine());
                break;

            case State.Alerted:
                agent.speed = chaseSpeed;
                alertMemoryTimer = alertMemoryTime;
                currentDetectionGauge = 100f;
                isInvestigatingRoutineActive = false;
                suspiciousHoldTimer = 0f;
                lostSightTimer = 0f;
                BroadcastAlertToNearbyEnemies(lastKnownPosition);
                break;
        }
    }

    #endregion

    #region FSM Behavior Updates

    private void UpdateFSMBehavior()
    {
        switch (currentState)
        {
            case State.Patrol:
                UpdatePatrolBehavior();
                break;
            case State.Suspicious:
                break;
            case State.Alerted:
                UpdateAlertedBehavior();
                break;
        }
    }

    private void UpdatePatrolBehavior()
    {
        if (waypoints == null || waypoints.Length == 0) return;

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            if (!isWaitingAtWaypoint)
            {
                StartCoroutine(WaitAtWaypointRoutine());
            }
        }
    }

    private IEnumerator WaitAtWaypointRoutine()
    {
        isWaitingAtWaypoint = true;

        if (waypoints != null && waypoints.Length > 1)
        {
            int nextIndex = (currentWaypointIndex + 1) % waypoints.Length;
            Vector3 dirToNext = (waypoints[nextIndex].position - transform.position).normalized;
            dirToNext.y = 0f;

            if (dirToNext != Vector3.zero)
            {
                Quaternion targetRot = Quaternion.LookRotation(dirToNext);
                yield return SmoothRotateRoutine(targetRot, 0.5f);
            }

            float remainingWait = Mathf.Max(0f, waypointWaitTime - 0.5f);
            yield return new WaitForSeconds(remainingWait);

            currentWaypointIndex = nextIndex;
            agent.SetDestination(waypoints[currentWaypointIndex].position);
        }
        else
        {
            yield return new WaitForSeconds(waypointWaitTime);
        }

        isWaitingAtWaypoint = false;
    }

    #endregion

    #region Suspicious Routine

    private IEnumerator InvestigateAndLookAroundRoutine()
    {
        isInvestigatingRoutineActive = true;

        Vector3 startPos = transform.position;
        Vector3 targetDir = (lastKnownPosition - startPos).normalized;
        float distToLKP = Vector3.Distance(startPos, lastKnownPosition);
        float moveDist = Mathf.Min(distToLKP, maxInvestigateDistance);

        Vector3 investTargetPos = startPos + targetDir * moveDist;
        agent.SetDestination(investTargetPos);

        while (!agent.pathPending && agent.remainingDistance > agent.stoppingDistance)
        {
            if (currentState == State.Alerted) yield break;
            yield return null;
        }

        agent.isStopped = true;
        Quaternion baseRotation = transform.rotation;

        Quaternion leftRot = baseRotation * Quaternion.Euler(0, -lookAroundAngle, 0);
        yield return SmoothRotateRoutine(leftRot, 0.5f);
        yield return new WaitForSeconds(lookAroundWaitTime);

        Quaternion rightRot = baseRotation * Quaternion.Euler(0, lookAroundAngle, 0);
        yield return SmoothRotateRoutine(rightRot, 0.5f);
        yield return new WaitForSeconds(lookAroundWaitTime);

        yield return SmoothRotateRoutine(baseRotation, 0.5f);

        agent.isStopped = false;
        isInvestigatingRoutineActive = false;

        suspiciousHoldTimer = suspiciousHoldTime;
    }

    private IEnumerator SmoothRotateRoutine(Quaternion targetRot, float duration)
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

    #endregion

    #region Alerted Behavior & Pursuit Cancellation Trigger

    private void UpdateAlertedBehavior()
    {
        if (targetPlayer == null) return;

        float distToPlayer = Vector3.Distance(transform.position, targetPlayer.transform.position);
        bool isPlayerInProximity = distToPlayer <= proximityAlertRadius;

        // 플레이어를 눈으로 보거나 바짝 붙어있을 때
        if (fov.CanSeePlayer || isPlayerInProximity)
        {
            lastKnownPosition = targetPlayer.transform.position;
            agent.SetDestination(lastKnownPosition);
            alertMemoryTimer = alertMemoryTime; // 4초 대기 리셋
            lostSightTimer = 0f;
        }
        else
        {
            lostSightTimer += Time.deltaTime;

            // 🎯 [Trigger 1] 플레이어가 35m 이상 멀어지고 3초 이상 안 보이면 추격 즉시 포기!
            if (distToPlayer >= maxChaseDistance && lostSightTimer >= 3.0f)
            {
                Debug.Log($"🏃‍♂️ [{gameObject.name}] 플레이어가 너무 멀어짐({distToPlayer:F1}m). 추격 포기 및 수색 전환!");
                currentDetectionGauge = suspiciousThreshold + 10f; // 즉시 Suspicious로 강하
                return;
            }

            // LKP(마지막 위치)로 전속력 이동
            agent.SetDestination(lastKnownPosition);

            // 🎯 [Trigger 2] LKP 지점에 도착했을 때 4초간 수색 대기 후 게이지 감소 시작
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
            {
                alertMemoryTimer -= Time.deltaTime;
            }
        }
    }

    public void TriggerAlert(Vector3 targetPos)
    {
        lastKnownPosition = targetPos;
        currentDetectionGauge = 100f;
        ChangeState(State.Alerted);
    }

    private void BroadcastAlertToNearbyEnemies(Vector3 alertPosition)
    {
        Collider[] nearbyCols = Physics.OverlapSphere(transform.position, alertBroadcastRadius);

        foreach (var col in nearbyCols)
        {
            EnemyAI enemy = col.GetComponent<EnemyAI>();
            if (enemy != null && enemy != this && enemy.CurrentState != State.Alerted)
            {
                enemy.TriggerAlert(alertPosition);
            }
        }
    }

    #endregion

    #region Noise Event Handling

    private void HandleNoiseEvents()
    {
        if (fov.HasHeardNoise)
        {
            lastKnownPosition = fov.LastHeardNoisePosition;

            if (fov.LastHeardNoiseType == FieldOfView.NoiseType.Alert)
            {
                TriggerAlert(lastKnownPosition);
            }
            else if (fov.LastHeardNoiseType == FieldOfView.NoiseType.Caution)
            {
                currentDetectionGauge = Mathf.Clamp(currentDetectionGauge + noiseGaugeBoost, 0f, 100f);
            }

            fov.ClearNoiseMemory();
        }
    }

    #endregion
}