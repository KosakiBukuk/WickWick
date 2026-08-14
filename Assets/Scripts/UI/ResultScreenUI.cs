using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static ScoreManager;

/// <summary>
/// 🎯 [게임 클리어 결과 화면 UI 연출 모듈 - 완벽 복구 & 디버깅 강화 버전]
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
    [SerializeField] private Button mainMenuButton;

    [Header("🔊 Result Audio Settings (선택 사항)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip countTickSFX;
    [SerializeField] private AudioClip rankStampSFX;
    [SerializeField] private AudioClip newRecordSFX;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            Debug.Log("✅ [ResultScreenUI] 싱글톤 등록 완료!");
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        if (mainMenuButton != null)
            mainMenuButton.onClick.AddListener(OnMainMenuClicked);

        // 시작 시 결과창 숨김
        if (resultCanvasRoot != null)
            resultCanvasRoot.SetActive(false);
    }

    /// <summary>
    /// 🎯 탈출 구역(ExitZone) 도달 시 호출되는 진입 함수
    /// </summary>
    public void OpenResultScreen()
    {
        Debug.Log("🎬 [ResultScreenUI] OpenResultScreen 정상 호출됨!");

        // 1. 루트 패널 활성화
        if (resultCanvasRoot != null)
        {
            resultCanvasRoot.SetActive(true);
        }

        // 2. 플레이어 체력 정보 가져오기
        PlayerHealth playerHealth = FindFirstObjectByType<PlayerHealth>();
        float currentHP = playerHealth != null ? playerHealth.CurrentHealth : 100f;
        float maxHP = playerHealth != null ? playerHealth.MaxHealth : 100f;

        // 3. 점수 산출 및 코루틴 실행
        if (ScoreManager.Instance != null)
        {
            ScoreResult result = ScoreManager.Instance.CalculateFinalScore(currentHP, maxHP);
            Debug.Log($"📊 [ResultScreenUI] 점수 계산 성공! 최종점수: {result.finalScore}, 랭크: {result.rank}");
            StartCoroutine(ShowResultSequenceRoutine(result));
        }
        else
        {
            Debug.LogError("🚨 [ResultScreenUI] ScoreManager.Instance가 null입니다! Hierarchy에 @ScoreManager가 있는지 확인하세요!");
        }
    }

    /// <summary>
    /// 🎯 unscaledDeltaTime 기반 순차 집계 코루틴
    /// </summary>
    private IEnumerator ShowResultSequenceRoutine(ScoreResult result)
    {
        Debug.Log("▶️ [ResultScreenUI] 연출 코루틴 시작!");

        // 마우스 커서 활성화
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (newRecordBadge != null) newRecordBadge.SetActive(false);
        if (buttonsPanel != null) buttonsPanel.SetActive(false);
        if (rankText != null) rankText.gameObject.SetActive(false);

        if (finalScoreText != null) finalScoreText.text = "0";
        if (highScoreText != null) highScoreText.text = $"BEST: {result.highScore:N0}";

        yield return new WaitForSecondsRealtime(0.3f);

        // 1. 세부 지표 출력
        int totalSeconds = Mathf.FloorToInt(result.clearTime);
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;

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
        yield return new WaitForSecondsRealtime(0.5f);

        // 2. 최종 점수 롤링 애니메이션
        float duration = 1.2f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            int currentRollingScore = Mathf.RoundToInt(Mathf.Lerp(0, result.finalScore, t));

            if (finalScoreText != null)
                finalScoreText.text = $"{currentRollingScore:N0}";

            yield return null;
        }

        if (finalScoreText != null)
            finalScoreText.text = $"{result.finalScore:N0}";

        PlaySound(countTickSFX);
        yield return new WaitForSecondsRealtime(0.4f);

        // 3. 랭크 도장 스탬프 연출
        if (rankText != null)
        {
            rankText.text = result.rank.ToString();
            rankText.color = GetRankColor(result.rank);
            rankText.gameObject.SetActive(true);

            rankText.transform.localScale = Vector3.one * 2.5f;
            float stampDuration = 0.2f;
            float stampElapsed = 0f;
            while (stampElapsed < stampDuration)
            {
                stampElapsed += Time.unscaledDeltaTime;
                rankText.transform.localScale = Vector3.Lerp(Vector3.one * 2.5f, Vector3.one, stampElapsed / stampDuration);
                yield return null;
            }
            rankText.transform.localScale = Vector3.one;

            PlaySound(rankStampSFX);
        }

        yield return new WaitForSecondsRealtime(0.3f);

        // 4. 신기록 뱃지
        if (result.isNewRecord && newRecordBadge != null)
        {
            newRecordBadge.SetActive(true);
            PlaySound(newRecordSFX);
        }

        // 5. 버튼 활성화
        if (buttonsPanel != null)
        {
            buttonsPanel.SetActive(true);
        }

        Debug.Log("🎉 [ResultScreenUI] 모든 결과창 연출 완료!");
    }

    private Color GetRankColor(ScoreManager.Rank rank)
    {
        switch (rank)
        {
            case ScoreManager.Rank.S: return new Color(1f, 0.84f, 0f);
            case ScoreManager.Rank.A: return new Color(0.2f, 0.9f, 1f);
            case ScoreManager.Rank.B: return new Color(0.4f, 1f, 0.4f);
            default: return new Color(0.8f, 0.8f, 0.8f);
        }
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    public void OnMainMenuClicked()
    {
        Time.timeScale = 1f;
        Debug.Log("🏠 [ResultScreenUI] 메인 메뉴로 이동합니다.");
        SceneManager.LoadScene(0);
    }
}