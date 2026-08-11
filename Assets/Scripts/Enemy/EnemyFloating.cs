using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 🎯 [땅 뚫림 완벽 방지] 적 둥둥 모듈
/// Mathf.Sin을 0~1 범위로 변환하여 땅 밑으로 들어가지 않고 공중에서만 부드럽게 둥둥 뜹니다.
/// </summary>
public class EnemyFloating : MonoBehaviour
{
    [Header("Visual Mesh Reference")]
    [Tooltip("★필수★ 자식 3D 모델(Capsule)을 드래그해 넣으세요!")]
    [SerializeField] private Transform visualMesh;

    [Header("Floating Motion Settings")]
    [Tooltip("기본 공중 부양 높이 (최저점일 때 바닥에서 얼마나 떨어질지)")]
    [SerializeField] private float hoverOffset = 0.1f;

    [Tooltip("둥둥거리는 움직임 크기 (공중에서 위로 움직이는 범주)")]
    [SerializeField] private float floatAmount = 0.2f;

    [Tooltip("둥둥거리는 속도")]
    [SerializeField] private float floatSpeed = 3.5f;

    [Tooltip("이동 중일 때 둥둥거리는 속도 배수")]
    [SerializeField] private float moveSpeedMultiplier = 1.5f;

    private NavMeshAgent agent;
    private float defaultLocalY;

    private void Start()
    {
        agent = GetComponent<NavMeshAgent>();

        if (visualMesh == null || visualMesh == transform)
        {
            if (transform.childCount > 0)
            {
                visualMesh = transform.GetChild(0);
            }
        }

        if (visualMesh != null && visualMesh != transform)
        {
            defaultLocalY = visualMesh.localPosition.y;
        }
    }

    private void LateUpdate()
    {
        if (visualMesh == null || visualMesh == transform) return;

        // NavMeshAgent 이동 중 여부
        bool isMoving = agent != null && agent.velocity.sqrMagnitude > 0.1f;
        float currentSpeed = isMoving ? (floatSpeed * moveSpeedMultiplier) : floatSpeed;

        // 🎯 [핵심 수학 보정] -1 ~ +1 범위를 0 ~ 1 범위로 변환!
        float sin01 = (Mathf.Sin(Time.time * currentSpeed) + 1.0f) * 0.5f;

        // 🎯 원래 바닥 위치 + 기본 붕 뜨는 높이(hoverOffset) + 둥둥 움직임(floatAmount)
        Vector3 currentPos = visualMesh.localPosition;
        currentPos.y = defaultLocalY + hoverOffset + (sin01 * floatAmount);

        visualMesh.localPosition = currentPos;
    }
}