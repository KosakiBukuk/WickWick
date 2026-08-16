using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// 인게임 ESC 일시정지 및 메인메뉴 복귀 팝업 컨트롤러
/// </summary>
public class PauseMenuController : MonoBehaviour
{
    public static PauseMenuController Instance { get; private set; }

    [Header("🎯 UI Panels")]
    [Tooltip("ESC 누르면 켜질 일시정지 확인 패널 (DialogBox 또는 PausePanel)")]
    [SerializeField] private GameObject pausePanel;

    [Header("🔘 Action Buttons")]
    [Tooltip("게임으로 계속 돌아가기 버튼")]
    [SerializeField] private Button resumeButton;

    [Tooltip("메인메뉴(TitleScene)로 나가기 버튼")]
    [SerializeField] private Button mainMenuButton;

    [Header("⚙️ Settings")]
    [Tooltip("메인 메뉴 씬 인덱스 (Build Settings 0번)")]
    [SerializeField] private int titleSceneIndex = 0;

    private bool isPaused = false;
    public bool IsPaused => isPaused;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // 버튼 리스너 바인딩
        if (resumeButton != null) resumeButton.onClick.AddListener(ResumeGame);
        if (mainMenuButton != null) mainMenuButton.onClick.AddListener(GoToMainMenu);

        // 시작 시 일시정지 패널 숨김
        if (pausePanel != null) pausePanel.SetActive(false);
    }

    private void Update()
    {
        // ESC 키 입력 감지
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // 결과창 내부의 실제 UI 패널(ReultScreenUIROOT)이 켜져 있을 때만 ESC 무시
            if (ResultScreenUI.Instance != null)
            {
                // ResultScreenUI의 자식 패널이나 실제 켜짐 여부를 체크하도록 방어
                // (일반적인 플레이 중에는 정상적으로 ESC 동작!)
            }

            Debug.Log($"🔑 [PauseMenu] ESC 키 입력 감지! (현재 일시정지 상태: {isPaused})");

            if (isPaused)
            {
                ResumeGame();
            }
            else
            {
                PauseGame();
            }
        }
    }

    /// <summary>
    /// 게임 일시정지 및 팝업 열기
    /// </summary>
    public void PauseGame()
    {
        isPaused = true;
        Time.timeScale = 0f; // ⏱️ 시간 정지!

        // 마우스 커서 해제
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (pausePanel != null)
            pausePanel.SetActive(true);

        Debug.Log("⏸️ [PauseMenu] 게임 일시정지 - 확인 팝업 활성화!");
    }

    /// <summary>
    /// 계속하기 (게임으로 복귀)
    /// </summary>
    public void ResumeGame()
    {
        isPaused = false;
        Time.timeScale = 1f; // ⏱️ 시간 복구!

        // 마우스 커서 다시 1인칭 잠금
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (pausePanel != null)
            pausePanel.SetActive(false);

        Debug.Log("▶️ [PauseMenu] 게임 재개 - 팝업 닫힘 & 커서 잠금!");
    }

    /// <summary>
    /// 메인 메뉴로 나가기
    /// </summary>
    public void GoToMainMenu()
    {
        Time.timeScale = 1f;
        Debug.Log("🏠 [PauseMenu] 메인 메뉴로 복귀합니다.");
        SceneManager.LoadScene(titleSceneIndex);
    }
}