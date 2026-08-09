using UnityEngine;

/// <summary>
/// 거리별 동적 시야 감지, Alerted 상태 시 360도 전방위 확장, 벽 반응형 디버그 메쉬를 담당
/// </summary>
public class FieldOfView : MonoBehaviour
{
    public enum NoiseType { Caution, Alert }

    [Header("Vision Settings")]
    [SerializeField] private float viewRadius = 15.0f;
    [Range(0, 360)]
    [SerializeField] private float viewAngle = 90.0f;
    [SerializeField] private LayerMask targetMask;
    [SerializeField] private LayerMask obstacleMask;
    [SerializeField] private Transform eyeTransform;

    [Header("Detection Multipliers")]
    [SerializeField] private float baseDetectionSpeed = 2.0f;
    [SerializeField] private float crouchMultiplier = 0.4f;
    [SerializeField] private float sprintMultiplier = 1.8f;
    [SerializeField] private float suspiciousDetectionMultiplier = 1.5f;

    [Header("Debug FOV Mesh Visualizer (Dev Only)")]
    [SerializeField] private bool showDebugFOV = true; // 🎯 나중에 머리위 UI 만들면 이거 끄면 됨!
    [SerializeField] private int meshResolution = 30;
    [SerializeField] private float meshHeightOffset = 0.05f;

    private Transform visiblePlayer;
    private bool canSeePlayer = false;
    private float currentNormalizedDistance = 1.0f;

    private bool hasHeardNoise = false;
    private Vector3 lastHeardNoisePosition;
    private NoiseType lastHeardNoiseType = NoiseType.Caution;

    private MeshFilter outerMeshFilter;
    private MeshFilter innerMeshFilter;
    private MeshRenderer outerMeshRenderer;
    private MeshRenderer innerMeshRenderer;
    private Mesh outerMesh;
    private Mesh innerMesh;

    /// <summary>
    /// 🎯 [핵심] Alerted(추격) 상태일 때는 시야각을 360도로 자동 확장!
    /// </summary>
    public float EffectiveViewAngle
    {
        get
        {
            EnemyAI ai = GetComponent<EnemyAI>();
            if (ai != null && ai.CurrentState == EnemyAI.State.Alerted)
            {
                return 360.0f; // Alerted 상태 시 전방위 360도
            }
            return viewAngle;  // 평소엔 기본 90도
        }
    }

    public bool CanSeePlayer => canSeePlayer;
    public Transform VisiblePlayer => visiblePlayer;
    public float ViewRadius => viewRadius;
    public float CurrentNormalizedDistance => currentNormalizedDistance;

    public bool HasHeardNoise => hasHeardNoise;
    public Vector3 LastHeardNoisePosition => lastHeardNoisePosition;
    public NoiseType LastHeardNoiseType => lastHeardNoiseType;

    private void Start()
    {
        if (eyeTransform == null) eyeTransform = transform;

        if (showDebugFOV)
        {
            SetupFOVMeshObjects();
        }
    }

    private void Update()
    {
        FindVisiblePlayer();
    }

    private void LateUpdate()
    {
        if (showDebugFOV && outerMeshFilter != null)
        {
            DrawFOVMesh();
        }
    }

    private void FindVisiblePlayer()
    {
        canSeePlayer = false;
        visiblePlayer = null;
        currentNormalizedDistance = 1.0f;

        Collider[] targetsInViewRadius = Physics.OverlapSphere(eyeTransform.position, viewRadius, targetMask);

        float currentAngle = EffectiveViewAngle; // 동적 시야각 적용

        for (int i = 0; i < targetsInViewRadius.Length; i++)
        {
            Transform target = targetsInViewRadius[i].transform;
            Vector3 dirToTarget = (target.position - eyeTransform.position).normalized;

            // 360도일 때는 무조건 Angle 판정 통과!
            if (Vector3.Angle(eyeTransform.forward, dirToTarget) < currentAngle / 2f)
            {
                float distToTarget = Vector3.Distance(eyeTransform.position, target.position);

                if (!Physics.Raycast(eyeTransform.position, dirToTarget, distToTarget, obstacleMask))
                {
                    canSeePlayer = true;
                    visiblePlayer = target;
                    currentNormalizedDistance = Mathf.Clamp01(distToTarget / viewRadius);
                    break;
                }
            }
        }
    }

    public float CalculateDetectionDelta(PlayerController player, EnemyAI.State currentState)
    {
        if (!canSeePlayer) return -1.0f;

        float distanceWeight = Mathf.Pow(1.0f - currentNormalizedDistance, 2f) + 0.2f;

        float postureWeight = 1.0f;
        if (player != null)
        {
            if (player.IsCrouching) postureWeight = crouchMultiplier;
            else if (player.IsSprinting) postureWeight = sprintMultiplier;
        }

        float stateMultiplier = (currentState == EnemyAI.State.Suspicious) ? suspiciousDetectionMultiplier : 1.0f;

        return baseDetectionSpeed * distanceWeight * postureWeight * stateMultiplier * Time.deltaTime * 100f;
    }

    public void ListenNoise(Vector3 noisePosition, float noiseRadius, NoiseType noiseType)
    {
        float distance = Vector3.Distance(transform.position, noisePosition);
        if (distance <= noiseRadius)
        {
            hasHeardNoise = true;
            lastHeardNoisePosition = noisePosition;
            lastHeardNoiseType = noiseType;
        }
    }

    public void ClearNoiseMemory()
    {
        hasHeardNoise = false;
    }

    #region Procedural FOV Mesh Visualizer

    private void SetupFOVMeshObjects()
    {
        GameObject outerObj = new GameObject("FOV_Outer_Mesh");
        outerObj.transform.SetParent(transform, false);
        outerMeshFilter = outerObj.AddComponent<MeshFilter>();
        outerMeshRenderer = outerObj.AddComponent<MeshRenderer>();
        outerMesh = new Mesh { name = "Outer FOV Mesh" };
        outerMeshFilter.mesh = outerMesh;

        GameObject innerObj = new GameObject("FOV_Inner_Mesh");
        innerObj.transform.SetParent(transform, false);
        innerMeshFilter = innerObj.AddComponent<MeshFilter>();
        innerMeshRenderer = innerObj.AddComponent<MeshRenderer>();
        innerMesh = new Mesh { name = "Inner FOV Mesh" };
        innerMeshFilter.mesh = innerMesh;

        Material transMat = new Material(Shader.Find("Sprites/Default"));
        outerMeshRenderer.material = transMat;
        innerMeshRenderer.material = transMat;
    }

    private void DrawFOVMesh()
    {
        EnemyAI ai = GetComponent<EnemyAI>();
        EnemyAI.State state = ai != null ? ai.CurrentState : EnemyAI.State.Patrol;
        float gaugePercent = ai != null ? ai.CurrentDetectionGauge / 100f : 0f;

        Color outerColor = GetStateColor(state, isInner: false);
        Color innerColor = GetStateColor(state, isInner: true);

        outerMeshRenderer.material.color = outerColor;
        innerMeshRenderer.material.color = innerColor;

        // 동적 시야각(EffectiveViewAngle)을 전달하여 메쉬 생성!
        GenerateArcMesh(outerMesh, viewRadius, EffectiveViewAngle);

        if (gaugePercent > 0f && state != EnemyAI.State.Alerted)
        {
            innerMeshRenderer.enabled = true;
            GenerateArcMesh(innerMesh, viewRadius * gaugePercent, EffectiveViewAngle);
        }
        else
        {
            innerMeshRenderer.enabled = false;
        }
    }

    private void GenerateArcMesh(Mesh mesh, float radius, float angleDegree)
    {
        int stepCount = Mathf.RoundToInt(angleDegree * meshResolution / 90f);
        float stepAngleSize = angleDegree / stepCount;

        Vector3[] vertices = new Vector3[stepCount + 2];
        int[] triangles = new int[stepCount * 3];

        vertices[0] = transform.InverseTransformPoint(transform.position + Vector3.up * meshHeightOffset);

        int vertexIndex = 1;
        int triangleIndex = 0;

        for (int i = 0; i <= stepCount; i++)
        {
            float angle = transform.eulerAngles.y - angleDegree / 2f + stepAngleSize * i;
            Vector3 dir = DirFromAngle(angle, true);

            Vector3 hitPoint;
            if (Physics.Raycast(eyeTransform.position, dir, out RaycastHit hit, radius, obstacleMask))
            {
                hitPoint = hit.point;
            }
            else
            {
                hitPoint = eyeTransform.position + dir * radius;
            }

            hitPoint.y = transform.position.y + meshHeightOffset;
            vertices[vertexIndex] = transform.InverseTransformPoint(hitPoint);

            if (i < stepCount)
            {
                triangles[triangleIndex] = 0;
                triangles[triangleIndex + 1] = vertexIndex;
                triangles[triangleIndex + 2] = vertexIndex + 1;
                triangleIndex += 3;
            }

            vertexIndex++;
        }

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
    }

    private Color GetStateColor(EnemyAI.State state, bool isInner)
    {
        switch (state)
        {
            case EnemyAI.State.Patrol:
                return isInner
                    ? new Color(1f, 0.6f, 0f, 0.6f)
                    : new Color(1f, 0.92f, 0.015f, 0.25f);
            case EnemyAI.State.Suspicious:
                return isInner
                    ? new Color(1f, 0.35f, 0f, 0.8f)
                    : new Color(1f, 0.5f, 0f, 0.4f);
            case EnemyAI.State.Alerted:
                return new Color(1f, 0f, 0f, 0.45f); // 강렬한 빨간색 (360도 전방위)
            default:
                return new Color(1f, 1f, 1f, 0.2f);
        }
    }

    #endregion

    public Vector3 DirFromAngle(float angleInDegrees, bool angleIsGlobal)
    {
        if (!angleIsGlobal) angleInDegrees += transform.eulerAngles.y;
        return new Vector3(Mathf.Sin(angleInDegrees * Mathf.Deg2Rad), 0, Mathf.Cos(angleInDegrees * Mathf.Deg2Rad));
    }
}