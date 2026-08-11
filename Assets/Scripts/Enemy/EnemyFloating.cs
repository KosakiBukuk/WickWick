using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 🎯 [완벽 방어형 적 둥둥 모듈]
/// Inspector의 Float Amount 수치 및 LateUpdate 연산 적용으로 애니메이션 interference 완벽 차단!
/// </summary>
public class EnemyFloating : MonoBehaviour
{
    [Header("Visual Mesh Reference")]
    [Tooltip("★필수★ 자식 3D 모델(Capsule)을 여기에 드래그해 넣으세요!")]
    [SerializeField] private Transform visualMesh;

    [Header("Floating Motion Settings")]
    [Tooltip("둥둥거리는 높이 (눈에 잘 안 띄면 0.3 ~ 0.5 정도로 올려보세요!)")]
    [SerializeField] private float floatAmount = 0.25f;

    [Tooltip("둥둥거리는 속도")]
    [SerializeField] private float floatSpeed = 4.0f;

    [Tooltip("이동 중일 때 둥둥거리는 속도 배수")]
    [SerializeField] private float moveSpeedMultiplier = 1.5f;

    private NavMeshAgent agent;
    private float defaultLocalY;

    private void Start()
    {
        agent = GetComponent<NavMeshAgent>();

        // visualMesh 슬롯이 비어있거나 자기 자신(Root)이 들어가 있다면 첫 번째 자식(Capsule)을 자동 탐색!
        if (visualMesh == null || visualMesh == transform)
        {
            if (transform.childCount > 0)
            {
                visualMesh = transform.GetChild(0);
            }
            else
            {
                Debug.LogWarning("[EnemyFloating] 자식 오브젝트(Capsule)를 찾지 못했습니다!");
            }
        }

        if (visualMesh != null && visualMesh != transform)
        {
            defaultLocalY = visualMesh.localPosition.y;
        }
    }

    // 🎯 [핵심] Animator 등의 덮어쓰기 방지를 위해 LateUpdate 에서 위치 재연산!
    private void LateUpdate()
    {
        if (visualMesh == null || visualMesh == transform) return;

        // NavMeshAgent 이동 중 여부 판별
        bool isMoving = agent != null && agent.velocity.sqrMagnitude > 0.1f;
        float currentSpeed = isMoving ? (floatSpeed * moveSpeedMultiplier) : floatSpeed;

        // 오직 자식 메쉬의 Y축만 부드럽게 위아래로 둥~둥~!
        Vector3 currentPos = visualMesh.localPosition;
        currentPos.y = defaultLocalY + Mathf.Sin(Time.time * currentSpeed) * floatAmount;
        visualMesh.localPosition = currentPos;
    }
}