using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 메인 타이틀 화면 UI 및 씬 진입 컨트롤러
/// 일반 시작 / 튜토리얼 스킵 시작 / 설정창 팝업 / 게임 종료를 제어합니다.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    // 인게임 씬에서 참조할 튜토리얼 스킵 플래그 (static)
    public static bool IsTutorialSkipped { get; private set; } = false;

    [Header("🎯 Main Menu Buttons")]
    [Tooltip("튜토리얼 포함 일반 시작 버튼")]
    [SerializeField] private Button normalStartButton;

    [Tooltip("튜토리얼 스킵 본게임 직행 버튼")]
    [SerializeField] private Button skipTutorialStartButton;

    [Tooltip("설정 창 열기 버튼")]
    [SerializeField] private Button settingsButton;

    [Tooltip("게임 종료 버튼")]
    [SerializeField] private Button exitButton;

    [Header("⚙️ Settings UI Panel")]
    [Tooltip("설정(사운드/감도) 팝업 패널")]
    [SerializeField] private GameObject settingsPanel;

    [Header("🎮 Scene Load Settings")]
    [Tooltip("로드할 인게임 씬 번호 (Build Settings 기준, 보통 1번)")]
    [SerializeField] private int gameSceneIndex = 1;

    private void Awake()
    {
        // 마우스 커서 해제 & 표시
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // 버튼 리스너 연결
        if (normalStartButton != null)
            normalStartButton.onClick.AddListener(OnNormalStartClicked);

        if (skipTutorialStartButton != null)
            skipTutorialStartButton.onClick.AddListener(OnSkipTutorialStartClicked);

        if (settingsButton != null)
            settingsButton.onClick.AddListener(OnSettingsClicked);

        if (exitButton != null)
            exitButton.onClick.AddListener(OnExitClicked);

        // 설정 패널 닫아두기
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }

    /// <summary>
    /// 1. 튜토리얼 포함 시작 (Zone 0부터)
    /// </summary>
    public void OnNormalStartClicked()
    {
        IsTutorialSkipped = false;
        Time.timeScale = 1f;
        Debug.Log("🎮 [MainMenu] 튜토리얼 포함 모드로 출격!");
        SceneManager.LoadScene(gameSceneIndex);
    }

    /// <summary>
    /// 2. 튜토리얼 스킵 시작 (Zone 1로 바로 진입)
    /// </summary>
    public void OnSkipTutorialStartClicked()
    {
        IsTutorialSkipped = true;
        Time.timeScale = 1f;
        Debug.Log("⚡ [MainMenu] 튜토리얼 스킵! SkipTuto 위치로 직행합니다!");
        SceneManager.LoadScene(gameSceneIndex);
    }

    /// <summary>
    /// 3. 설정 창 토글
    /// </summary>
    public void OnSettingsClicked()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
        }
    }

    /// <summary>
    /// 4. 게임 종료
    /// </summary>
    public void OnExitClicked()
    {
        Debug.Log("🚪 [MainMenu] 게임을 종료합니다.");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}