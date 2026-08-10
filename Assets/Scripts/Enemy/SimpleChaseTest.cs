using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// NavMeshAgent의 순수 추격 능력 및 상태 진단용 테스트 스크립트
/// </summary>
public class SimpleChaseTest : MonoBehaviour
{
    private NavMeshAgent agent;
    private Transform player;

    private void Start()
    {
        agent = GetComponent<NavMeshAgent>();

        // 1. 플레이어 태그 검색 및 진단
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
            Debug.Log($"✅ [진단] 플레이어 찾기 성공!: {player.name}");
        }
        else
        {
            Debug.LogError("❌ [진단 실패] 'Player' 태그를 가진 오브젝트가 없습니다! Player 오브젝트의 Tag를 'Player'로 설정해주세요!");
        }

        // 2. NavMeshAgent 컴포넌트 및 바닥 착지 진단
        if (agent == null)
        {
            Debug.LogError("❌ [진단 실패] NavMeshAgent 컴포넌트가 없습니다!");
            return;
        }

        if (!agent.isOnNavMesh)
        {
            Debug.LogError("❌ [진단 실패] 적이 NavMesh 바닥 위에 올라가있지 않습니다! 적의 Y축 높이를 조정하거나 NavMesh Bake 상태를 확인하세요!");
            return;
        }

        // 3. 강제 추격 스펙 설정
        agent.isStopped = false;
        agent.autoBraking = false;   // 브레이크 끄기
        agent.stoppingDistance = 0f;
        agent.speed = 4.5f;
        agent.angularSpeed = 1000f;
        agent.acceleration = 40f;
    }

    private void Update()
    {
        if (player != null && agent != null && agent.isOnNavMesh)
        {
            agent.SetDestination(player.position);
        }
    }
}