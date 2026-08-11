using UnityEngine;

/// <summary>
/// 🎯 [본 게임 시작 트리거]
/// 튜토리얼 구역을 지나 Zone 1 입구를 통과하는 순간 점수 및 클리어 타임 측정을 시작합니다.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ZoneStartTrigger : MonoBehaviour
{
    private bool isTriggered = false;

    private void OnTriggerEnter(Collider other)
    {
        if (isTriggered || !other.CompareTag("Player")) return;

        isTriggered = true;

        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.StartScoreTracking();
            Debug.Log("🚀 [ZoneStartTrigger] 튜토리얼 종료! Zone 1 진입 - 본격적인 점수 및 시간 측정을 시작합니다!");
        }
        else
        {
            Debug.LogWarning("[ZoneStartTrigger] ScoreManager Instance를 찾을 수 없습니다!");
        }
    }
}