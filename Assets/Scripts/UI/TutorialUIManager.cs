using System.Collections;
using UnityEngine;

/// <summary>
/// 튜토리얼 UI 패널을 열고 닫는 매니저.
/// C키로 닫을 때 1프레임 딜레이를 주어 플레이어가 같은 프레임에 앉아버리는 현상을 방지합니다.
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
        // 튜토리얼 창이 활성화되어 있을 때
        if (isTutorialActive && currentActivePanel != null)
        {
            // 열린 패널에 MultiPageTutorial 컴포넌트가 붙어있다면,
            // 페이지 넘김 처리를 위해 TutorialUIManager는 C키 입력을 가로채지 않고 패스!
            if (currentActivePanel.GetComponent<MultiPageTutorial>() != null) return;

            // 일반 1페이지짜리 패널일 때만 C키로 바로 닫기!
            if (Input.GetKeyDown(KeyCode.C))
            {
                CloseTutorial();
            }
        }
    }

    public void OpenTutorialPanel(GameObject tutorialPanel)
    {
        if (tutorialPanel == null) return;

        currentActivePanel = tutorialPanel;
        currentActivePanel.SetActive(true);

        Time.timeScale = 0f;
        isTutorialActive = true;
    }

    public void CloseTutorial()
    {
        if (currentActivePanel != null)
        {
            currentActivePanel.SetActive(false);
            currentActivePanel = null;
        }

        // 바로 시간을 풀지 않고, 이번 프레임의 C키 입력이 소진되도록 코루틴 실행
        StartCoroutine(ResumeGameAfterFrameRoutine());
    }

    private IEnumerator ResumeGameAfterFrameRoutine()
    {
        // 1프레임 대기 (모든 스크립트의 Update()가 지나갈 때까지 대기)
        yield return null;

        Time.timeScale = 1f;
        isTutorialActive = false;
    }
}