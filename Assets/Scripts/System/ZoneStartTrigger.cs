using UnityEngine;

/// <summary>
/// 🎯 [Zone 1 시작 & 역주행 방지 차단벽 활성화 트리거]
/// 1. 플레이어 구역 번호를 Zone 1로 갱신
/// 2. 점수 및 클리어 타임 집계 시작
/// 3. Zone 0(튜토리얼 구역) 재진입을 막는 차단벽 오브젝트 활성화
/// </summary>
[RequireComponent(typeof(Collider))]
public class ZoneStartTrigger : MonoBehaviour
{
    [Header("🎯 Zone Settings")]
    [Tooltip("진입할 목표 구역 번호 (기본값: 1)")]
    [SerializeField] private int targetZone = 1;

    [Header("🚪 Backtrack Blocker Settings")]
    [Tooltip("Zone 1 진입 시 켜서 Zone 0 재진입을 막을 차단 오브젝트들 (문, 벽, 콜라이더 등)")]
    [SerializeField] private GameObject[] blockersToActivate;

    private bool isTriggered = false;

    private void Awake()
    {
        // 콜라이더를 트리거 모드로 자동 세팅
        Collider col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isTriggered || !other.CompareTag("Player")) return;

        PlayerController player = other.GetComponent<PlayerController>();
        if (player != null)
        {
            // 🎯 1. 플레이어 구역 번호를 Zone 1로 갱신!
            player.SetCurrentZone(targetZone);
        }

        // 🎯 2. 구역 번호가 확실하게 갱신되었을 때 시스템 작동!
        if (player != null && player.CurrentZone == targetZone)
        {
            isTriggered = true;

            // 점수 및 시간 측정 시작!
            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.StartScoreTracking();
                Debug.Log("🚀 [ZoneStartTrigger] Zone 1 진입! 점수 및 시간 측정을 시작합니다!");
            }

            // Zone 0 역주행 방지 차단벽 일괄 활성화!
            ActivateBlockers();
        }
    }

    /// <summary>
    /// 🎯 등록된 차단벽 오브젝트들을 활성화하는 함수
    /// </summary>
    private void ActivateBlockers()
    {
        if (blockersToActivate == null || blockersToActivate.Length == 0) return;

        foreach (GameObject blocker in blockersToActivate)
        {
            if (blocker != null)
            {
                blocker.SetActive(true);
                Debug.Log($"🔒 [ZoneStartTrigger] Zone 0 차단벽 활성화: '{blocker.name}'");
            }
        }
    }
}