using UnityEngine;

/// <summary>
/// 🎯 [점수 집계 & 랭크 산출 & 최고 기록 저장 시스템 - 고스트 어쌔신 특화 밸런스판]
/// 무발각 상태에서의 적 은신 처치(Kill)에 가장 높은 가중치를 부여합니다.
/// </summary>
public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    public enum Rank { S, A, B, C }

    [Header("Base Score Settings")]
    [Tooltip("기본 부여 점수")]
    [SerializeField] private float baseScore = 10000f;

    [Header("Clear Time Settings")]
    [Tooltip("목표 기준 클리어 타임 (초 단위, 기본: 300초 = 5분)")]
    [SerializeField] private float targetTime = 300f;

    [Tooltip("목표 시간보다 빠를 때 초당 보너스 점수 (킬 보너스에 집중하도록 10점으로 완화)")]
    [SerializeField] private float timeBonusPerSec = 10f;

    [Tooltip("목표 시간 초과 시 초당 감점 점수")]
    [SerializeField] private float timePenaltyPerSec = 15f;

    [Tooltip("시간 초과 최대 감점 한도")]
    [SerializeField] private float maxTimePenalty = 2000f;

    [Header("Detection Settings")]
    [Tooltip("적에게 발각(Alerted)될 때마다 차감되는 감점 (난투 방지용 2,500점)")]
    [SerializeField] private float detectionPenalty = 2500f;

    [Header("Kill Bonus Settings")]
    [Tooltip("🎯 적 1명 처치 시 획득하는 대량 보너스 점수! (무발각 암살 최고 가중치)")]
    [SerializeField] private float killBonus = 1500f;

    [Header("Health Bonus Settings")]
    [Tooltip("체력이 100% 보존되었을 때 최대 보너스")]
    [SerializeField] private float maxHealthBonus = 2000f;

    [Header("Rank Thresholds (개편된 랭크 컷)")]
    [SerializeField] private float sRankThreshold = 18000f; // 무발각 다처치 시 달성 가능!
    [SerializeField] private float aRankThreshold = 13000f; // 무처치 스피드런 달성 가능
    [SerializeField] private float bRankThreshold = 8000f;

    // 실시간 기록 변수
    private float elapsedTime = 0f;
    private int detectionCount = 0;
    private int killCount = 0;
    private bool isTimerRunning = false;

    // 프로퍼티
    public float ElapsedTime => elapsedTime;
    public int DetectionCount => detectionCount;
    public int KillCount => killCount;
    public bool IsTimerRunning => isTimerRunning;

    private const string HIGH_SCORE_KEY = "STEALTH_HIGH_SCORE";
    private const string BEST_TIME_KEY = "STEALTH_BEST_TIME";
    private const string BEST_RANK_KEY = "STEALTH_BEST_RANK";

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // Zone 1 진입 전까지는 대기
        isTimerRunning = false;
        elapsedTime = 0f;
    }

    private void Update()
    {
        if (isTimerRunning)
        {
            elapsedTime += Time.deltaTime;
        }
    }

    public void StartScoreTracking()
    {
        elapsedTime = 0f;
        detectionCount = 0;
        killCount = 0;
        isTimerRunning = true;
        Debug.Log("⏱️ [ScoreManager] Zone 1 진입 감지 -> 점수 및 시간 측정 시작!");
    }

    public void AddDetection()
    {
        detectionCount++;
        Debug.Log($"🚨 [ScoreManager] 감지 횟수 증가! 현재 감지: {detectionCount}회");
    }

    public void AddKill()
    {
        killCount++;
        Debug.Log($"⚔️ [ScoreManager] 적 제압 성공! (+{killBonus}점 누적 | 현재 처치: {killCount}명)");
    }

    public ScoreResult CalculateFinalScore(float currentHP, float maxHP)
    {
        isTimerRunning = false;

        // 1. 타임 점수 연산
        float timeScore = 0f;
        if (elapsedTime <= targetTime)
        {
            timeScore = (targetTime - elapsedTime) * timeBonusPerSec;
        }
        else
        {
            float overTimePenalty = (elapsedTime - targetTime) * timePenaltyPerSec;
            timeScore = -Mathf.Min(overTimePenalty, maxTimePenalty);
        }

        // 2. 감지 감점
        float totalDetectionPenalty = detectionCount * detectionPenalty;

        // 3. 처치 보너스 (대량 가산!)
        float totalKillBonus = killCount * killBonus;

        // 4. 잔여 체력 보너스
        float hpRatio = Mathf.Clamp01(currentHP / maxHP);
        float healthBonus = hpRatio * maxHealthBonus;

        // 5. 최종 총점 계산
        float totalScore = Mathf.Max(0f, baseScore + timeScore - totalDetectionPenalty + totalKillBonus + healthBonus);

        // 6. 랭크 판정
        Rank finalRank = Rank.C;
        if (totalScore >= sRankThreshold) finalRank = Rank.S;
        else if (totalScore >= aRankThreshold) finalRank = Rank.A;
        else if (totalScore >= bRankThreshold) finalRank = Rank.B;

        int finalScoreInt = Mathf.RoundToInt(totalScore);

        // 7. 최고 기록(DB) 갱신 및 저장
        int previousHighScore = PlayerPrefs.GetInt(HIGH_SCORE_KEY, 0);
        bool isNewRecord = finalScoreInt > previousHighScore;

        if (isNewRecord)
        {
            PlayerPrefs.SetInt(HIGH_SCORE_KEY, finalScoreInt);
            PlayerPrefs.SetFloat(BEST_TIME_KEY, elapsedTime);
            PlayerPrefs.SetString(BEST_RANK_KEY, finalRank.ToString());
            PlayerPrefs.Save();
            Debug.Log($"🎉 [ScoreManager] 신기록 갱신! 최고 점수: {finalScoreInt}점 (Rank {finalRank})");
        }

        ScoreResult result = new ScoreResult
        {
            finalScore = finalScoreInt,
            rank = finalRank,
            clearTime = elapsedTime,
            detectionCount = detectionCount,
            killCount = killCount,
            timeBonusOrPenalty = Mathf.RoundToInt(timeScore),
            detectionPenalty = Mathf.RoundToInt(totalDetectionPenalty),
            killBonus = Mathf.RoundToInt(totalKillBonus),
            healthBonus = Mathf.RoundToInt(healthBonus),
            isNewRecord = isNewRecord,
            highScore = Mathf.Max(previousHighScore, finalScoreInt)
        };

        return result;
    }

    public int GetSavedHighScore() => PlayerPrefs.GetInt(HIGH_SCORE_KEY, 0);
    public float GetSavedBestTime() => PlayerPrefs.GetFloat(BEST_TIME_KEY, 0f);
    public string GetSavedBestRank() => PlayerPrefs.GetString(BEST_RANK_KEY, "None");

    /// <summary>
    /// 🎯 [UI 및 결과 연동용 데이터 구조체]
    /// </summary>
    public struct ScoreResult
    {
        public int finalScore;
        public ScoreManager.Rank rank;
        public float clearTime;
        public int detectionCount;
        public int killCount;
        public int timeBonusOrPenalty;
        public int detectionPenalty;
        public int killBonus;
        public int healthBonus;

        // 🌟 [이 2줄이 추가되어야 빨간 줄이 사라져요!]
        public bool isNewRecord; // 🎯 신기록 달성 여부 (True/False)
        public int highScore;    // 🎯 역대 최고 점수
    }
}