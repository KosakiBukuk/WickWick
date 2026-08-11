using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// 🎯 [결과 UI 표시 모듈]
/// ScoreResult 데이터를 받아 UI Text 항목들을 갱신하고 연출합니다.
/// </summary>
public class GameResultUI : MonoBehaviour
{
    [Header("UI Panel Root")]
    [SerializeField] private GameObject resultPanel; // 결과 패널 전체 오브젝트

    [Header("UI Text References")]
    [SerializeField] private Text rankText;          // S, A, B, C 랭크
    [SerializeField] private Text finalScoreText;    // 최종 점수
    [SerializeField] private Text clearTimeText;     // 클리어 타임 (00:00)
    [SerializeField] private Text detectionCountText;// 감지 횟수
    [SerializeField] private Text killCountText;      // 처치 수

    private void Awake()
    {
        // 시작할 때는 결과 창을 숨겨둠
        if (resultPanel != null)
            resultPanel.SetActive(false);
    }

    /// <summary>
    /// 점수 결과를 받아와 UI 텍스트에 파싱하여 표시!
    /// </summary>
    public void ShowResultUI(ScoreResult result)
    {
        if (resultPanel != null)
            resultPanel.SetActive(true);

        // 1. 랭크 표시 (S, A, B, C)
        if (rankText != null)
        {
            rankText.text = result.rank.ToString();

            // 랭크별 색상 포인트!
            switch (result.rank)
            {
                case ScoreManager.Rank.S: rankText.color = Color.yellow; break;
                case ScoreManager.Rank.A: rankText.color = Color.green; break;
                case ScoreManager.Rank.B: rankText.color = Color.cyan; break;
                case ScoreManager.Rank.C: rankText.color = Color.gray; break;
            }
        }

        // 2. 최종 점수
        if (finalScoreText != null)
            finalScoreText.text = $"{result.finalScore:N0} PTS";

        // 3. 클리어 타임 (분:초 변환)
        if (clearTimeText != null)
        {
            int minutes = Mathf.FloorToInt(result.clearTime / 60f);
            int seconds = Mathf.FloorToInt(result.clearTime % 60f);
            clearTimeText.text = $"{minutes:00}:{seconds:00}";
        }

        // 4. 감지 횟수 & 처치 수
        if (detectionCountText != null)
            detectionCountText.text = $"{result.detectionCount} 회";

        if (killCountText != null)
            killCountText.text = $"{result.killCount} 명";
    }

    /// <summary>
    /// UI 버튼용: 다시 하기 (현재 씬 재시작)
    /// </summary>
    public void OnClickRestart()
    {
        Time.timeScale = 1f; // 시간 복구!
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}