using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 적 AI의 4단계 FSM(Patrol, Suspicious, Detecting, Alerted), LKP 추적, 동료 경보를 제어하는 핵심 두뇌
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(FieldOfView))]
public class EnemyAI : MonoBehaviour
{
    public enum State { Patrol, Suspicious, Detecting, Alerted }

    [Header("Current FSM State")]
    [SerializeField] private State currentState = State.Patrol;

    [Header("Patrol Settings")]
    [SerializeField] private Transform[] waypoints;          // 순찰 경로 지점들
    [SerializeField] private float patrolSpeed = 2.0f;
    [SerializeField] private float waypointWaitTime = 2.0f;  // 지점 도착 후 대기 시간

    [Header("Chase & Investigation Settings")]
    [SerializeField] private float chaseSpeed = 4.5f;
    [SerializeField] private float investigateTime = 5.0f;   // LKP/소음 지점 수색 시간 (5초)
    [SerializeField] private float alertBroadcastRadius = 15.0f; // 주변 동료 경보 반경 (15m)

    [Header("Detection Settings")]
    [SerializeField] private float currentDetectionGauge = 0f; // 0 ~ 100%
    [SerializeField] private float gaugeDecaySpeed = 25f;      // 안 보일 때 게이지 감소 속도

    private NavMeshAgent agent;
    private FieldOfView fov;
    private PlayerController targetPlayer;

    private int currentWaypointIndex = 0;
    private bool isWaitingAtWaypoint = false;
    private float investigateTimer = 0f;
    private Vector3 lastKnownPosition; // 마지막 목격 지점 (LKP)

    // UI/사운드 연동용 이벤트
    public event Action<State> OnStateChanged;
    public event Action<float> OnDetectionGaugeChanged; // (0 ~ 100)

    public State CurrentState => currentState;
    public float CurrentDetectionGauge => currentDetectionGauge;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        fov = GetComponent<FieldOfView>();
    }

    private void Start()
    {
        // 씬에서 플레이어 자동 탐색
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
        UpdateFSM();
    }

    #region FSM Logic

    private void UpdateFSM()
    {
        switch (currentState)
        {
            case State.Patrol:
                UpdatePatrolState();
                break;
            case State.Suspicious:
                UpdateSuspiciousState();
                break;
            case State.Detecting:
                UpdateDetectingState();
                break;
            case State.Alerted:
                UpdateAlertedState();
                break;
        }
    }

    private void ChangeState(State newState)
    {
        if (currentState == newState) return;

        currentState = newState;
        Debug.Log($"🤖 [{gameObject.name}] AI 상태 변경: {newState}");
        OnStateChanged?.Invoke(currentState);

        // 상태 변경에 따른 속도 설정
        switch (currentState)
        {
            case State.Patrol:
                agent.speed = patrolSpeed;
                break;
            case State.Suspicious:
                agent.speed = patrolSpeed * 1.2f; // 수색 시 약간 빠르게 걸음
                break;
            case State.Detecting:
                agent.speed = patrolSpeed * 0.8f; // 주춤하며 경계
                break;
            case State.Alerted:
                agent.speed = chaseSpeed; // 전속력 추격!
                break;
        }
    }

    #endregion

    #region 1. Patrol State

    private void UpdatePatrolState()
    {
        // 1. 시야 감지 검사 ➔ Detecting 상태 전환
        if (fov.CanSeePlayer)
        {
            ChangeState(State.Detecting);
            return;
        }

        // 2. 순찰 경로 이동
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

    #region 2. Suspicious State (수색 및 LKP 추적)

    private void UpdateSuspiciousState()
    {
        // 시야에 플레이어가 다시 보이면 즉시 Detecting으로 전환!
        if (fov.CanSeePlayer)
        {
            ChangeState(State.Detecting);
            return;
        }

        // 의심 지점(LKP 또는 소음 위치)으로 이동
        agent.SetDestination(lastKnownPosition);

        // 도착 후 5초간 수색(두리번거림)
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
        {
            investigateTimer += Time.deltaTime;

            if (investigateTimer >= investigateTime)
            {
                investigateTimer = 0f;
                Debug.Log($"🤖 [{gameObject.name}] 수색 종료! 원래 순찰 노선으로 복귀합니다.");

                // 원래 순찰 지점으로 복귀
                if (waypoints != null && waypoints.Length > 0)
                {
                    agent.SetDestination(waypoints[currentWaypointIndex].position);
                }
                ChangeState(State.Patrol);
            }
        }
    }

    #endregion

    #region 3. Detecting State (감지 게이지 누적)

    private void UpdateDetectingState()
    {
        if (fov.CanSeePlayer)
        {
            // 거리 및 플레이어 자세(앉기/달리기) 반영 게이지 증가
            float delta = fov.CalculateDetectionDelta(targetPlayer);
            currentDetectionGauge = Mathf.Clamp(currentDetectionGauge + delta, 0f, 100f);
            OnDetectionGaugeChanged?.Invoke(currentDetectionGauge);

            // 게이지 100% 달성 시 ➔ Alerted(발각) 전환!
            if (currentDetectionGauge >= 100f)
            {
                TriggerAlert(fov.VisiblePlayer.position);
            }
        }
        else
        {
            // 시야에서 벗어나면 게이지 감소
            currentDetectionGauge = Mathf.Clamp(currentDetectionGauge - (gaugeDecaySpeed * Time.deltaTime), 0f, 100f);
            OnDetectionGaugeChanged?.Invoke(currentDetectionGauge);

            // 게이지가 0이 되면 LKP 수색(Suspicious)으로 이행
            if (currentDetectionGauge <= 0f)
            {
                ChangeState(State.Suspicious);
            }
        }
    }

    #endregion

    #region 4. Alerted State (추격 & 동료 경보)

    private void UpdateAlertedState()
    {
        if (fov.CanSeePlayer)
        {
            // 실시간 플레이어 위치 갱신
            lastKnownPosition = fov.VisiblePlayer.position;
            agent.SetDestination(lastKnownPosition);
        }
        else
        {
            // 시야에서 사라져도 마지막 목격 지점(LKP)까지 전속력 이동!
            agent.SetDestination(lastKnownPosition);

            // LKP 도착 후 플레이어가 없으면 Suspicious(수색) 모드로 이행
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
            {
                Debug.Log($"🤖 [{gameObject.name}] LKP 지점에 도착했지만 플레이어가 없습니다. 수색 모드로 전환합니다.");
                investigateTimer = 0f;
                ChangeState(State.Suspicious);
            }
        }
    }

    /// <summary>
    /// 발각 순간 자신 및 주변 동료 적들에게 즉시 경보 발송!
    /// </summary>
    public void TriggerAlert(Vector3 targetPos)
    {
        lastKnownPosition = targetPos;
        currentDetectionGauge = 100f;
        ChangeState(State.Alerted);

        // 주변 동료 AI 연쇄 경보 (Broadcast)
        BroadcastAlertToNearbyEnemies(targetPos);
    }

    private void BroadcastAlertToNearbyEnemies(Vector3 alertPosition)
    {
        Collider[] nearbyCols = Physics.OverlapSphere(transform.position, alertBroadcastRadius);

        foreach (var col in nearbyCols)
        {
            EnemyAI enemy = col.GetComponent<EnemyAI>();
            // 자기 자신이 아니고, 아직 발각 상태가 아닌 동료 적에게 신호 전달
            if (enemy != null && enemy != this && enemy.CurrentState != State.Alerted)
            {
                Debug.Log($"📢 [{gameObject.name}] ➔ [{enemy.name}] 동료에게 플레이어 발각 위치 전달!");
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
                // 총기 사격음 ➔ 즉시 Alerted(발각)
                TriggerAlert(lastKnownPosition);
            }
            else if (fov.LastHeardNoiseType == FieldOfView.NoiseType.Caution)
            {
                // 일반 소음 ➔ Suspicious(수색)
                if (currentState != State.Alerted)
                {
                    investigateTimer = 0f;
                    ChangeState(State.Suspicious);
                }
            }

            fov.ClearNoiseMemory();
        }
    }

    #endregion

    private void OnDrawGizmosSelected()
    {
        // LKP 및 수색 지점 Gizmo 표시
        if (currentState == State.Suspicious || currentState == State.Alerted)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(lastKnownPosition, 0.8f);
            Gizmos.DrawLine(transform.position, lastKnownPosition);
        }
    }
}