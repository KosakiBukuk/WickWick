using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 🎯 [게임오버 연출 및 UI 전담 모듈]
/// PlayerHealth.OnDie 이벤트를 구독하여 화면 페이드아웃 →
/// GAMEOVER UI 표시 → 메인메뉴(TitleScene) 복귀 / 게임 종료를 처리합니다.
/// </summary>
public class GameOverUIManager : MonoBehaviour
{
    [Header("🎯 Player 참조")]
    [Tooltip("씬에 있는 플레이어의 PlayerHealth 컴포넌트. 비워두면 자동으로 찾습니다.")]
    [SerializeField] private PlayerHealth playerHealth;

    [Header("🖤 페이드 연출")]
    [Tooltip("화면 전체를 덮는 검은색 Image가 붙어있는 CanvasGroup (알파 0 -> 1)")]
    [SerializeField] private CanvasGroup fadeCanvasGroup;
    [SerializeField] private float fadeDuration = 1.5f;

    [Header("💀 GAME OVER UI")]
    [Tooltip("GAMEOVER 텍스트 + 버튼들이 들어있는 패널. 평소엔 비활성화 상태여야 합니다.")]
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private CanvasGroup gameOverPanelCanvasGroup;
    [SerializeField] private float panelFadeInDuration = 0.5f;

    [Header("🔘 버튼 연결")]
    [Tooltip("메인 메뉴(TitleScene)로 돌아가는 버튼")]
    [SerializeField] private Button backToMenuButton;

    [Tooltip("게임을 완전히 종료하는 버튼 (선택 사항)")]
    [SerializeField] private Button quitGameButton;

    [Header("🚪 씬 설정")]
    [Tooltip("메인 메뉴 TitleScene의 Build Index (보통 0번)")]
    [SerializeField] private int titleSceneIndex = 0;

    private void Awake()
    {
        if (playerHealth == null)
        {
            playerHealth = FindFirstObjectByType<PlayerHealth>();
        }

        // 시작 시 페이드 화면 투명화, GameOver 패널 비활성화
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.alpha = 0f;
            fadeCanvasGroup.blocksRaycasts = false;
        }

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }

        // 버튼 리스너 바인딩
        if (backToMenuButton != null)
            backToMenuButton.onClick.AddListener(OnBackToMenuClicked);

        if (quitGameButton != null)
            quitGameButton.onClick.AddListener(OnQuitGameClicked);
    }

    private void OnEnable()
    {
        if (playerHealth != null)
        {
            playerHealth.OnDie += HandlePlayerDie;
        }
    }

    private void OnDisable()
    {
        if (playerHealth != null)
        {
            playerHealth.OnDie -= HandlePlayerDie;
        }
    }

    private void HandlePlayerDie()
    {
        Debug.Log("💀 [GameOverUIManager] OnDie 이벤트 수신! 게임오버 암전 연출 시작.");
        StartCoroutine(GameOverSequence());
    }

    private IEnumerator GameOverSequence()
    {
        // 1. 검은 화면 서서히 덮기 (unscaledDeltaTime 사용)
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.blocksRaycasts = true;
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                fadeCanvasGroup.alpha = Mathf.Clamp01(elapsed / fadeDuration);
                yield return null;
            }
            fadeCanvasGroup.alpha = 1f;
        }

        // 2. GAMEOVER 패널 활성화 + 부드럽게 페이드 인
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);

            if (gameOverPanelCanvasGroup != null)
            {
                gameOverPanelCanvasGroup.alpha = 0f;
                float elapsed = 0f;
                while (elapsed < panelFadeInDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    gameOverPanelCanvasGroup.alpha = Mathf.Clamp01(elapsed / panelFadeInDuration);
                    yield return null;
                }
                gameOverPanelCanvasGroup.alpha = 1f;
            }
        }

        // 3. 커서 표시 (클릭 가능하게)
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        // 4. 연출 완료 후 시간 정지
        Time.timeScale = 0f;
    }

    /// <summary>
    /// 🔘 [메인 메뉴로 복귀] 버튼
    /// </summary>
    private void OnBackToMenuClicked()
    {
        Debug.Log("🏠 [GameOverUIManager] TitleScene(0번 씬)으로 복귀합니다!");
        Time.timeScale = 1f; // ⚠️ 시간 복구 필수!
        SceneManager.LoadScene(titleSceneIndex);
    }

    /// <summary>
    /// 🔘 [게임 완전 종료] 버튼
    /// </summary>
    private void OnQuitGameClicked()
    {
        Debug.Log("🚪 [GameOverUIManager] 게임 종료!");
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}