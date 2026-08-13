using UnityEngine;

/// <summary>
/// 🎯 [오브젝트 활성화 방식 튜토리얼 UI 매니저]
/// 지정된 튜토리얼 UI 패널 오브젝트를 켜고 시간을 정지하며, C키로 닫고 재개합니다.
/// </summary>
public class TutorialUIManager : MonoBehaviour
{
    public static TutorialUIManager Instance { get; private set; }

    private GameObject currentActivePanel;
    private bool isTutorialActive = false;

    public bool IsTutorialActive => isTutorialActive;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Update()
    {
        // 🎯 튜토리얼 UI가 켜져 있을 때만 'C'키 입력을 받아 닫기!
        if (isTutorialActive && currentActivePanel != null)
        {
            if (Input.GetKeyDown(KeyCode.C))
            {
                CloseTutorial();
            }
        }
    }

    /// <summary>
    /// 🛑 트리거에서 넘겨받은 UI 패널 오브젝트를 켜고 시간 정지!
    /// </summary>
    public void OpenTutorialPanel(GameObject tutorialPanel)
    {
        if (tutorialPanel == null) return;

        currentActivePanel = tutorialPanel;
        currentActivePanel.SetActive(true);

        // 🛑 게임 물리 및 시간 완전 정지!
        Time.timeScale = 0f;
        isTutorialActive = true;
    }

    /// <summary>
    /// ▶️ C키 클릭 시 현재 켜진 튜토리얼 패널을 끄고 게임 재개
    /// </summary>
    public void CloseTutorial()
    {
        if (currentActivePanel != null)
        {
            currentActivePanel.SetActive(false);
            currentActivePanel = null;
        }

        // ▶️ 게임 시간 다시 흘러가게 복구!
        Time.timeScale = 1f;
        isTutorialActive = false;
    }
}