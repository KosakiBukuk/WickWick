using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 감지 게이지 기반 FSM, 2배 가속 감지, 수색 및 순찰 복귀 후 6.5초 게이지 유지(Hold) 시스템
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
    [SerializeField] private float alertMemoryTime = 2.5f;
    [SerializeField] private float proximityAlertRadius = 3.0f;

    [Header("Detection Gauge Master Settings")]
    [SerializeField] private float currentDetectionGauge = 0f;
    [SerializeField] private float gaugeDecaySpeed = 20f;
    [SerializeField] private float suspiciousThreshold = 30f;
    [SerializeField] private float noiseGaugeBoost = 35f;
    [SerializeField] private float suspiciousHoldTime = 6.5f; // 🎯 수색 종료 후 6.5초간 게이지 유지!

    private NavMeshAgent agent;
    private FieldOfView fov;
    private PlayerController targetPlayer;

    private int currentWaypointIndex = 0;
    private bool isWaitingAtWaypoint = false;
    private bool isInvestigatingRoutineActive = false;
    private float suspiciousHoldTimer = 0f; // 🎯 6.5초 유지 타이머
    private float alertMemoryTimer = 0f;
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
            // 시야에 보일 때: 실시간 게이지 상승
            float delta = fov.CalculateDetectionDelta(targetPlayer, currentState);
            currentDetectionGauge = Mathf.Clamp(currentDetectionGauge + delta, 0f, 100f);
        }
        else if (currentState == State.Suspicious && isInvestigatingRoutineActive)
        {
            // 수색 진행 중: 30% 밑으로 안 내려가게 고정
            currentDetectionGauge = Mathf.Clamp(currentDetectionGauge - (gaugeDecaySpeed * Time.deltaTime), suspiciousThreshold, 100f);
        }
        else if (suspiciousHoldTimer > 0f)
        {
            // 🎯 [핵심] 순찰로 복귀했더라도 6.5초 동안은 게이지가 깎이지 않고 그대로 유지(Hold)!
            suspiciousHoldTimer -= Time.deltaTime;
        }
        else if (currentState != State.Alerted)
        {
            // 6.5초가 지난 후: 게이지 서서히 감쇄
            currentDetectionGauge = Mathf.Clamp(currentDetectionGauge - (gaugeDecaySpeed * Time.deltaTime), 0f, 100f);
        }
        else
        {
            // Alerted 추격 유예 타이머 종료 후 감쇄
            alertMemoryTimer -= Time.deltaTime;
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
        yield return new WaitForSeconds(waypointWaitTime);

        currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
        agent.SetDestination(waypoints[currentWaypointIndex].position);
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

        // 좌측 2초 대기
        Quaternion leftRot = baseRotation * Quaternion.Euler(0, -lookAroundAngle, 0);
        yield return SmoothRotateRoutine(leftRot, 0.5f);
        yield return new WaitForSeconds(lookAroundWaitTime);

        // 우측 2초 대기
        Quaternion rightRot = baseRotation * Quaternion.Euler(0, lookAroundAngle, 0);
        yield return SmoothRotateRoutine(rightRot, 0.5f);
        yield return new WaitForSeconds(lookAroundWaitTime);

        // 정면 원위치 복귀
        yield return SmoothRotateRoutine(baseRotation, 0.5f);

        agent.isStopped = false;
        isInvestigatingRoutineActive = false;

        // 🎯 [핵심] 수색이 끝나고 순찰로 복귀하는 순간부터 6.5초 타이머 시작!
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

    #region Alerted Behavior & Broadcast

    private void UpdateAlertedBehavior()
    {
        if (targetPlayer == null) return;

        float distToPlayer = Vector3.Distance(transform.position, targetPlayer.transform.position);
        bool isPlayerInProximity = distToPlayer <= proximityAlertRadius;

        if (fov.CanSeePlayer || isPlayerInProximity)
        {
            lastKnownPosition = targetPlayer.transform.position;
            agent.SetDestination(lastKnownPosition);
            alertMemoryTimer = alertMemoryTime;
        }
        else
        {
            agent.SetDestination(lastKnownPosition);
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