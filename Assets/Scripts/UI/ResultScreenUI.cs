using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static ScoreManager;

/// <summary>
/// 🎯 [게임 클리어 결과 화면 UI 연출 모듈]
/// 세부 점수 집계 카운팅, 랭크 도장 연출, 신기록 표시 및 재시작/메인메뉴 이동을 제어합니다.
/// </summary>
public class ResultScreenUI : MonoBehaviour
{
    public static ResultScreenUI Instance { get; private set; }

    [Header("🎯 Root UI Panels")]
    [SerializeField] private GameObject resultCanvasRoot;
    [SerializeField] private GameObject newRecordBadge;
    [SerializeField] private GameObject buttonsPanel;

    [Header("📝 TextMeshPro Displays")]
    [SerializeField] private TextMeshProUGUI clearTimeText;
    [SerializeField] private TextMeshProUGUI killBonusText;
    [SerializeField] private TextMeshProUGUI detectionPenaltyText;
    [SerializeField] private TextMeshProUGUI healthBonusText;
    [SerializeField] private TextMeshProUGUI finalScoreText;
    [SerializeField] private TextMeshProUGUI rankText;
    [SerializeField] private TextMeshProUGUI highScoreText;

    [Header("🔘 Action Buttons")]
    [SerializeField] private Button restartButton;
    [SerializeField] private Button mainMenuButton;

    [Header("🔊 Result Audio Settings (선택 사항)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip countTickSFX;
    [SerializeField] private AudioClip rankStampSFX;
    [SerializeField] private AudioClip newRecordSFX;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // 버튼 리스너 바인딩
        if (restartButton != null) restartButton.onClick.AddListener(OnRestartClicked);
        if (mainMenuButton != null) mainMenuButton.onClick.AddListener(OnMainMenuClicked);

        // 시작 시 결과창 숨김
        if (resultCanvasRoot != null) resultCanvasRoot.SetActive(false);
    }

    /// <summary>
    /// 🎯 탈출 시네머신 종료 시 외부(GameManager/ExitZone)에서 호출하는 진입 함수
    /// </summary>
    public void OpenResultScreen()
    {
        // 1. 플레이어 체력 정보 가져오기
        PlayerHealth playerHealth = FindFirstObjectByType<PlayerHealth>();
        float currentHP = playerHealth != null ? playerHealth.CurrentHealth : 100f;
        float maxHP = playerHealth != null ? playerHealth.MaxHealth : 100f;

        // 2. ScoreManager에서 최종 점수 산출
        if (ScoreManager.Instance != null)
        {
            ScoreResult result = ScoreManager.Instance.CalculateFinalScore(currentHP, maxHP);
            StartCoroutine(ShowResultSequenceRoutine(result));
        }
        else
        {
            Debug.LogError("⚠️ [ResultScreenUI] ScoreManager를 찾을 수 없습니다!");
        }
    }

    /// <summary>
    /// 🎯 도파민 폭발! 순차적 점수 집계 연출 코루틴
    /// </summary>
    private IEnumerator ShowResultSequenceRoutine(ScoreResult result)
    {
        // 0. 커서 잠금 해제 & UI 켜기
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (resultCanvasRoot != null) resultCanvasRoot.SetActive(true);
        if (newRecordBadge != null) newRecordBadge.SetActive(false);
        if (buttonsPanel != null) buttonsPanel.SetActive(false);
        if (rankText != null) rankText.gameObject.SetActive(false);

        // 텍스트 초기화
        finalScoreText.text = "0";
        highScoreText.text = $"BEST: {result.highScore:N0}";

        yield return new WaitForSeconds(0.3f);

        // 1. 🎯 세부 지표 출력 (라벨 및 기호 명확화!)
        int totalSeconds = Mathf.FloorToInt(result.clearTime);
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;

        // 타임 보너스가 양수(+)면 +기호 붙이기
        string timeBonusSign = result.timeBonusOrPenalty >= 0 ? "+" : "";
        if (clearTimeText != null)
            clearTimeText.text = $"TIME {minutes:00}:{seconds:00} ({timeBonusSign}{result.timeBonusOrPenalty:N0})";

        if (killBonusText != null)
            killBonusText.text = $"{result.killCount} KILLS (+{result.killBonus:N0})";

        if (detectionPenaltyText != null)
            detectionPenaltyText.text = $"{result.detectionCount} ALERTS (-{result.detectionPenalty:N0})";

        if (healthBonusText != null)
            healthBonusText.text = $"HP BONUS (+{result.healthBonus:N0})";

        PlaySound(countTickSFX);
        yield return new WaitForSeconds(0.5f);

        // 2. 최종 점수 드르륵- 롤링 카운팅 애니메이션
        float duration = 1.2f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            int currentRollingScore = Mathf.RoundToInt(Mathf.Lerp(0, result.finalScore, t));
            finalScoreText.text = $"{currentRollingScore:N0}";
            yield return null;
        }

        finalScoreText.text = $"{result.finalScore:N0}";
        PlaySound(countTickSFX);
        yield return new WaitForSeconds(0.4f);

        // 3. 랭크 도장 쾅-! (색상 차별화)
        if (rankText != null)
        {
            rankText.text = result.rank.ToString();
            rankText.color = GetRankColor(result.rank);
            rankText.gameObject.SetActive(true);

            // 랭크 도장 스탬프 펀치 애니메이션 효과
            rankText.transform.localScale = Vector3.one * 2.5f;
            float stampDuration = 0.2f;
            float stampElapsed = 0f;
            while (stampElapsed < stampDuration)
            {
                stampElapsed += Time.deltaTime;
                rankText.transform.localScale = Vector3.Lerp(Vector3.one * 2.5f, Vector3.one, stampElapsed / stampDuration);
                yield return null;
            }
            rankText.transform.localScale = Vector3.one;

            PlaySound(rankStampSFX);
        }

        yield return new WaitForSeconds(0.3f);

        // 4. 신기록 갱신 뱃지 활성화
        if (result.isNewRecord && newRecordBadge != null)
        {
            newRecordBadge.SetActive(true);
            PlaySound(newRecordSFX);
        }

        // 5. 버튼 패널 활성화
        if (buttonsPanel != null)
        {
            buttonsPanel.SetActive(true);
        }
    }

    private Color GetRankColor(ScoreManager.Rank rank)
    {
        switch (rank)
        {
            case ScoreManager.Rank.S: return new Color(1f, 0.84f, 0f);      // 골드/옐로우 (S랭크)
            case ScoreManager.Rank.A: return new Color(0.2f, 0.9f, 1f);     // 시안/스카이블루 (A랭크)
            case ScoreManager.Rank.B: return new Color(0.4f, 1f, 0.4f);     // 에메랄드 그린 (B랭크)
            default: return new Color(0.8f, 0.8f, 0.8f);                  // 그레이 (C랭크)
        }
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    public void OnRestartClicked()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void OnMainMenuClicked()
    {
        Time.timeScale = 1f;
        Debug.Log("🏠 [ResultScreenUI] 메인 메뉴로 이동합니다.");
        SceneManager.LoadScene(0);
    }
}