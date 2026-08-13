using UnityEngine;

/// <summary>
/// 🎯 [구역 감지 트리거]
/// 진입 시 지정된 튜토리얼 UI 패널 오브젝트를 켭니다.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class TutorialTrigger : MonoBehaviour
{
    [Header("🎯 Target UI Panel")]
    [Tooltip("이 구역에 들어왔을 때 활성화시킬 튜토리얼 UI 패널 오브젝트")]
    [SerializeField] private GameObject targetTutorialPanel;

    [Tooltip("한 번만 실행하고 트리거를 끌지 여부")]
    [SerializeField] private bool triggerOnce = true;

    private bool hasTriggered = false;

    private void Awake()
    {
        BoxCollider col = GetComponent<BoxCollider>();
        if (col != null) col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasTriggered && triggerOnce) return;

        if (other.CompareTag("Player"))
        {
            hasTriggered = true;

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
}