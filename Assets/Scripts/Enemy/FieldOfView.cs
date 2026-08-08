using UnityEngine;

/// <summary>
/// 거리별 동적 시야 감지(Zone 3단계), 자세별 가중치, 소음 수신을 담당하는 스마트 감지 모듈
/// </summary>
public class FieldOfView : MonoBehaviour
{
    public enum NoiseType { Caution, Alert } // Caution: 수색(Suspicious), Alert: 즉시 발각(Alerted)

    [Header("Vision Settings")]
    [SerializeField] private float viewRadius = 15.0f;       // 최대 시야 거리 (15m)
    [Range(0, 360)]
    [SerializeField] private float viewAngle = 90.0f;        // 시야 각도 (부채꼴 90도)
    [SerializeField] private LayerMask targetMask;           // 감지 대상 (Player)
    [SerializeField] private LayerMask obstacleMask;         // 시야 장애물 (Wall, Default)
    [SerializeField] private Transform eyeTransform;         // 적의 눈 위치

    [Header("Detection Multipliers")]
    [SerializeField] private float baseDetectionSpeed = 1.0f; // 기본 게이지 차오르는 속도
    [SerializeField] private float crouchMultiplier = 0.4f;   // 앉았을 때 감지 속도 감쇄 (60% 감소)
    [SerializeField] private float sprintMultiplier = 1.8f;   // 달릴 때 감지 속도 증가 (80% 증가)

    private Transform visiblePlayer;
    private bool canSeePlayer = false;
    private float currentNormalizedDistance = 1.0f; // 0.0(초근접) ~ 1.0(최대 거리)

    // 소음 수신 변수
    private bool hasHeardNoise = false;
    private Vector3 lastHeardNoisePosition;
    private NoiseType lastHeardNoiseType = NoiseType.Caution;

    public bool CanSeePlayer => canSeePlayer;
    public Transform VisiblePlayer => visiblePlayer;
    public float ViewRadius => viewRadius;
    public float ViewAngle => viewAngle;
    public float CurrentNormalizedDistance => currentNormalizedDistance;

    public bool HasHeardNoise => hasHeardNoise;
    public Vector3 LastHeardNoisePosition => lastHeardNoisePosition;
    public NoiseType LastHeardNoiseType => lastHeardNoiseType;

    private void Start()
    {
        if (eyeTransform == null)
            eyeTransform = transform;
    }

    private void Update()
    {
        FindVisiblePlayer();
    }

    private void FindVisiblePlayer()
    {
        canSeePlayer = false;
        visiblePlayer = null;
        currentNormalizedDistance = 1.0f;

        Collider[] targetsInViewRadius = Physics.OverlapSphere(eyeTransform.position, viewRadius, targetMask);

        for (int i = 0; i < targetsInViewRadius.Length; i++)
        {
            Transform target = targetsInViewRadius[i].transform;
            Vector3 dirToTarget = (target.position - eyeTransform.position).normalized;

            if (Vector3.Angle(eyeTransform.forward, dirToTarget) < viewAngle / 2f)
            {
                float distToTarget = Vector3.Distance(eyeTransform.position, target.position);

                // 시야 장애물 체크 (Line of Sight)
                if (!Physics.Raycast(eyeTransform.position, dirToTarget, distToTarget, obstacleMask))
                {
                    canSeePlayer = true;
                    visiblePlayer = target;

                    // 0.0(눈앞) ~ 1.0(최대 시야 끝) 정규화 거리 계산
                    currentNormalizedDistance = Mathf.Clamp01(distToTarget / viewRadius);
                    break;
                }
            }
        }
    }

    /// <summary>
    /// 플레이어의 거리 및 자세(앉기/달리기)를 반영하여 실시간 감지 게이지 증가량을 계산
    /// </summary>
    public float CalculateDetectionDelta(PlayerController player)
    {
        if (!canSeePlayer) return -1.0f; // 안 보일 때는 감소 신호

        // 거리가 가까울수록 가속도가 제곱으로 치솟음 (거리 가중치)
        float distanceWeight = Mathf.Pow(1.0f - currentNormalizedDistance, 2f) + 0.2f;

        // 플레이어 자세 반영
        float postureWeight = 1.0f;
        if (player != null)
        {
            if (player.IsCrouching) postureWeight = crouchMultiplier;
            else if (player.IsSprinting) postureWeight = sprintMultiplier;
        }

        return baseDetectionSpeed * distanceWeight * postureWeight * Time.deltaTime * 100f;
    }

    /// <summary>
    /// 외부 소음 수신 (Caution: 수색, Alert: 즉시 발각)
    /// </summary>
    public void ListenNoise(Vector3 noisePosition, float noiseRadius, NoiseType noiseType)
    {
        float distance = Vector3.Distance(transform.position, noisePosition);
        if (distance <= noiseRadius)
        {
            hasHeardNoise = true;
            lastHeardNoisePosition = noisePosition;
            lastHeardNoiseType = noiseType;
            Debug.Log($"👂 [{gameObject.name}] 소음 수신! 유형: {noiseType}, 위치: {noisePosition}");
        }
    }

    public void ClearNoiseMemory()
    {
        hasHeardNoise = false;
    }

    private void OnDrawGizmosSelected()
    {
        Transform eye = eyeTransform != null ? eyeTransform : transform;

        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(eye.position, viewRadius);

        Vector3 viewAngleA = DirFromAngle(-viewAngle / 2, false);
        Vector3 viewAngleB = DirFromAngle(viewAngle / 2, false);

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(eye.position, eye.position + viewAngleA * viewRadius);
        Gizmos.DrawLine(eye.position, eye.position + viewAngleB * viewRadius);

        if (canSeePlayer && visiblePlayer != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(eye.position, visiblePlayer.position);
        }
    }

    public Vector3 DirFromAngle(float angleInDegrees, bool angleIsGlobal)
    {
        if (!angleIsGlobal)
        {
            angleInDegrees += transform.eulerAngles.y;
        }
        return new Vector3(Mathf.Sin(angleInDegrees * Mathf.Deg2Rad), 0, Mathf.Cos(angleInDegrees * Mathf.Deg2Rad));
    }
}
