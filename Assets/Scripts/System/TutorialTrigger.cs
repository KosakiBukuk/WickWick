using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 🎯 [구역 감지 트리거 - 씬 리로드 후에도 '이미 본 튜토리얼' 기억 버전]
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class TutorialTrigger : MonoBehaviour
{
    [Header("🎯 Tutorial ID")]
    [Tooltip("이 튜토리얼의 고유 이름 (예: Crouch_Guide, Assassinate_Guide 등)")]
    [SerializeField] private string tutorialID = "Tutorial_01";

    [Header("🎯 Target UI Panel")]
    [Tooltip("이 구역에 들어왔을 때 활성화시킬 튜토리얼 UI 패널 오브젝트")]
    [SerializeField] private GameObject targetTutorialPanel;

    [Tooltip("한 번만 실행하고 트리거를 끌지 여부")]
    [SerializeField] private bool triggerOnce = true;

    // 🌟 [핵심 마법] 씬이 다시 로드되어도 메모리에 살아남는 정적(static) 기록장!
    private static HashSet<string> seenTutorials = new HashSet<string>();

    private bool hasTriggered = false;

    private void Awake()
    {
        BoxCollider col = GetComponent<BoxCollider>();
        if (col != null) col.isTrigger = true;
    }

    private void Start()
    {
        // 🎯 [핵심] 씬이 다시 로드되었을 때, 이미 본 적 있는 튜토리얼이라면 즉시 트리거 비활성화!
        if (seenTutorials.Contains(tutorialID))
        {
            gameObject.SetActive(false);
            Debug.Log($"⏩ [Tutorial] '{tutorialID}'는 이미 확인한 튜토리얼이므로 스킵합니다.");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasTriggered && triggerOnce) return;

        if (other.CompareTag("Player"))
        {
            hasTriggered = true;

            // 🎯 static 기록장에 이 튜토리얼 ID를 '열람 완료'로 등록!
            if (!seenTutorials.Contains(tutorialID))
            {
                seenTutorials.Add(tutorialID);
            }

            if (TutorialUIManager.Instance != null && targetTutorialPanel != null)
            {
                TutorialUIManager.Instance.OpenTutorialPanel(targetTutorialPanel);
            }

            if (triggerOnce)
            {
                gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// 🎯 (선택) 타이틀 화면으로 나가거나 게임을 완전히 처음부터 시작할 때 기록을 초기화하는 함수
    /// </summary>
    public static void ResetSeenTutorialHistory()
    {
        seenTutorials.Clear();
        Debug.Log("🧹 [Tutorial] 튜토리얼 열람 기록이 초기화되었습니다.");
    }
}