using UnityEngine;

/// <summary>
/// Alerted(발각) 상태 진입 시 360도 전방위 시야 및 사거리 150% 동적 확장 지원 FOV 센서
/// </summary>
public class FieldOfView : MonoBehaviour
{
    [Header("👁️ FOV Base Settings")]
    [SerializeField] private float baseViewRadius = 8.0f;       // 기본 시야 거리 (8m)
    [SerializeField] private float baseViewAngle = 90.0f;       // 기본 시야각 (90도)
    [SerializeField] private int meshResolution = 30;          // 메쉬 부드러움 (삼각형 개수)
    [SerializeField] private LayerMask obstacleMask;           // 장애물 레이어

    [Header("🎨 Color Settings")]
    [SerializeField] private Color baseColor = new Color(1f, 0.92f, 0.016f, 0.25f); // 노랑 (평시)
    [SerializeField] private Color fillColor = new Color(1f, 0.5f, 0f, 0.45f);      // 주황 (의심)
    [SerializeField] private Color alertColor = new Color(1f, 0f, 0f, 0.35f);       // 빨강 (발각)

    private Transform playerTransform;
    private bool canSeePlayer = false;
    private EnemyAI enemyAI;

    private GameObject baseFOVObj;
    private GameObject fillFOVObj;
    private MeshFilter baseMeshFilter;
    private MeshFilter fillMeshFilter;
    private MeshRenderer baseMeshRenderer;
    private MeshRenderer fillMeshRenderer;

    // 🎯 Alerted 상태 시 사거리 1.5배 확장 및 360도 전방위 시야 동적 반환
    public float CurrentViewRadius => (enemyAI != null && enemyAI.CurrentState == EnemyAI.State.Alerted) ? baseViewRadius * 1.5f : baseViewRadius;
    public float CurrentViewAngle => (enemyAI != null && enemyAI.CurrentState == EnemyAI.State.Alerted) ? 360f : baseViewAngle;

    public bool CanSeePlayer => canSeePlayer;

    private void Awake()
    {
        enemyAI = GetComponent<EnemyAI>();
    }

    private void Start()
    {
        GameObject p = GameObject.FindWithTag("Player");
        if (p != null) playerTransform = p.transform;

        SetupFOVMeshRenderers();
    }

    private void Update()
    {
        FindTargetPlayer();
    }

    private void LateUpdate()
    {
        DrawFOVMeshVisuals();
    }

    #region Detection Logic

    private void FindTargetPlayer()
    {
        canSeePlayer = false;
        if (playerTransform == null) return;

        float radius = CurrentViewRadius;
        float angle = CurrentViewAngle;

        Vector3 dirToTarget = (playerTransform.position - transform.position).normalized;
        float distToTarget = Vector3.Distance(transform.position, playerTransform.position);

        if (distToTarget <= radius)
        {
            // 🎯 360도 시야 상태이거나 지정된 부채꼴 각도 내 진입 시 체크
            if (angle >= 360f || Vector3.Angle(transform.forward, dirToTarget) < angle / 2f)
            {
                // 장애물 레이캐스트 검사
                if (!Physics.Raycast(transform.position + Vector3.up * 0.5f, dirToTarget, distToTarget, obstacleMask))
                {
                    canSeePlayer = true;
                }
            }
        }
    }

    public float CalculateDetectionDelta()
    {
        if (!canSeePlayer || playerTransform == null) return 0f;

        float radius = CurrentViewRadius;
        float dist = Vector3.Distance(transform.position, playerTransform.position);
        float distanceFactor = Mathf.Clamp01(1.0f - (dist / radius));
        return 80f * (0.5f + distanceFactor * 0.5f) * Time.deltaTime;
    }

    #endregion

    #region FOV Mesh Renderer

    private void SetupFOVMeshRenderers()
    {
        baseFOVObj = new GameObject("FOV_Base_Mesh");
        baseFOVObj.transform.SetParent(transform, false);
        baseFOVObj.transform.localPosition = new Vector3(0, 0.05f, 0);

        baseMeshFilter = baseFOVObj.AddComponent<MeshFilter>();
        baseMeshRenderer = baseFOVObj.AddComponent<MeshRenderer>();
        baseMeshRenderer.material = CreateTransparentMaterial(baseColor);

        fillFOVObj = new GameObject("FOV_Fill_Mesh");
        fillFOVObj.transform.SetParent(transform, false);
        fillFOVObj.transform.localPosition = new Vector3(0, 0.06f, 0);

        fillMeshFilter = fillFOVObj.AddComponent<MeshFilter>();
        fillMeshRenderer = fillFOVObj.AddComponent<MeshRenderer>();
        fillMeshRenderer.material = CreateTransparentMaterial(fillColor);
    }

    private Material CreateTransparentMaterial(Color col)
    {
        Shader transparentShader = Shader.Find("Sprites/Default");
        if (transparentShader == null) transparentShader = Shader.Find("Unlit/Transparent");

        Material mat = new Material(transparentShader);
        mat.color = col;
        return mat;
    }

    private void DrawFOVMeshVisuals()
    {
        if (enemyAI == null) return;

        float radius = CurrentViewRadius;
        float angle = CurrentViewAngle;

        if (enemyAI.CurrentState == EnemyAI.State.Alerted)
        {
            // 🔴 Alerted 상태: 1.5배 커진 360도 전방위 빨간색 거대 원형 시야 표시!
            baseMeshRenderer.material.color = alertColor;
            baseMeshFilter.mesh = GenerateFOVMesh(radius, angle);
            fillMeshFilter.mesh = null;
        }
        else
        {
            // 💛 Patrol / Suspicious 상태: 기존 90도 부채꼴 시야
            float gaugePercent = enemyAI.CurrentDetectionGauge / 100f;
            baseMeshRenderer.material.color = baseColor;
            baseMeshFilter.mesh = GenerateFOVMesh(radius, angle);

            if (gaugePercent > 0.01f)
            {
                fillMeshRenderer.material.color = fillColor;
                fillMeshFilter.mesh = GenerateFOVMesh(radius * gaugePercent, angle);
            }
            else
            {
                fillMeshFilter.mesh = null;
            }
        }
    }

    private Mesh GenerateFOVMesh(float radius, float angle)
    {
        Mesh mesh = new Mesh();
        int stepCount = Mathf.RoundToInt(angle * meshResolution / 90f);
        float stepAngleSize = angle / stepCount;

        Vector3[] vertices = new Vector3[stepCount + 2];
        int[] triangles = new int[stepCount * 3];

        vertices[0] = Vector3.zero;

        for (int i = 0; i <= stepCount; i++)
        {
            float currentAngle = -angle / 2f + stepAngleSize * i;
            Vector3 dir = Quaternion.Euler(0, currentAngle, 0) * Vector3.forward;
            vertices[i + 1] = dir * radius;

            if (i < stepCount)
            {
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = i + 2;
            }
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        return mesh;
    }

    #endregion
}