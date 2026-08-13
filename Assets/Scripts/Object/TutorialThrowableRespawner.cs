using System.Collections;
using UnityEngine;

/// <summary>
/// 🎯 [튜토리얼 전용] 필드의 소화기가 사라지면(획득/파괴) 자동 감지하여 일정 시간 후 원위치 리스폰!
/// </summary>
public class TutorialThrowableRespawner : MonoBehaviour
{
    [Header("🎯 Respawn Settings")]
    [Tooltip("스폰시킬 소화기(투척물) 프리팹")]
    [SerializeField] private GameObject throwablePrefab;

    [Tooltip("소화기를 주운 후 다음 소화기가 생성될 때까지의 대기시간 (초)")]
    [SerializeField] private float respawnDelay = 3.0f;

    private Vector3 spawnPosition;
    private Quaternion spawnRotation;
    private GameObject currentInstance;
    private bool isRespawning = false;

    private void Start()
    {
        // 빈 오브젝트(Respawner)의 현재 위치와 회전값을 기억!
        spawnPosition = transform.position;
        spawnRotation = transform.rotation;

        // 게임 시작 시 첫 소화기 생성!
        SpawnThrowable();
    }

    private void Update()
    {
        // 🎯 [핵심] 필드에 있던 소화기(currentInstance)가 주워져서 Destroy 되었고,
        // 현재 리스폰 타이머가 돌아가는 중이 아니라면 자동 리스폰 시작!
        if (currentInstance == null && !isRespawning)
        {
            StartCoroutine(RespawnRoutine());
        }
    }

    private void SpawnThrowable()
    {
        if (throwablePrefab != null)
        {
            currentInstance = Instantiate(throwablePrefab, spawnPosition, spawnRotation);
            Debug.Log("🧯 [TutorialRespawner] 새로운 튜토리얼 소화기가 스폰되었습니다!");
        }
    }

    private IEnumerator RespawnRoutine()
    {
        isRespawning = true;
        yield return new WaitForSeconds(respawnDelay);
        SpawnThrowable();
        isRespawning = false;
    }
}