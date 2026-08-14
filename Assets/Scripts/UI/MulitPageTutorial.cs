using UnityEngine;

/// <summary>
/// 🎯 [다중 페이지 튜토리얼 컴포넌트]
/// 여러 개의 튜토리얼 이미지/페이지를 C키로 순서대로 넘겨보고 종료합니다.
/// </summary>
public class MultiPageTutorial : MonoBehaviour
{
    [Header("🎯 Tutorial Pages")]
    [Tooltip("순서대로 보여줄 페이지 오브젝트들 (Page1, Page2)")]
    [SerializeField] private GameObject[] pages;

    private int currentPageIndex = 0;

    private void OnEnable()
    {
        // 팝업이 켜질 때 1페이지부터 초기화
        currentPageIndex = 0;
        UpdatePageDisplay();
    }

    private void Update()
    {
        // 🎯 C키 (또는 Space키)를 누르면 다음 페이지로 넘김!
        if (Input.GetKeyDown(KeyCode.C))
        {
            NextPage();
        }
    }

    private void NextPage()
    {
        currentPageIndex++;

        // 🎯 아직 볼 페이지가 남아있다면 다음 페이지 켜기!
        if (currentPageIndex < pages.Length)
        {
            UpdatePageDisplay();
        }
        else
        {
            // 🎯 모든 페이지를 다 봤다면 매니저를 통해 튜토리얼 창 닫기!
            if (TutorialUIManager.Instance != null)
            {
                TutorialUIManager.Instance.CloseTutorial();
            }
        }
    }

    private void UpdatePageDisplay()
    {
        for (int i = 0; i < pages.Length; i++)
        {
            if (pages[i] != null)
            {
                pages[i].SetActive(i == currentPageIndex);
            }
        }
    }
}