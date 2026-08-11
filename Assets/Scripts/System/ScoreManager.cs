using UnityEngine;

/// <summary>
/// 🎯 [점수 집계 & 랭크 산출 시스템 - 처치 보너스(Kill Bonus) 적용판]
/// 클리어 타임(5분 기준), 감지 횟수 차감, 처치 보너스 가산, 잔여 체력 보너스 종합 연산
/// </summary>
public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    public enum Rank { S, A, B, C }

    [Header("Base Score Settings")]
    [Tooltip("기본 부여 점수")]
    [SerializeField] private float baseScore = 10000f;

    [Header("Clear Time Settings")]
    [Tooltip("목표 기준 클리어 타임 (초 단위, 예: 300초 = 5분)")]
    [SerializeField] private float targetTime = 300f;

    [Tooltip("목표 시간보다 빠르게 클리어 시 초당 보너스 점수")]
    [SerializeField] private float timeBonusPerSec = 20f;

    [Tooltip("목표 시간 초과 시 초당 감점 점수")]
    [SerializeField] private float timePenaltyPerSec = 15f;

    [Tooltip("시간 초과로 인한 최대 감점 한도")]
    [SerializeField] private float maxTimePenalty = 2000f;

    [Header("Detection Settings")]
    [Tooltip("적에게 발각(Alerted)될 때마다 차감되는 감점 점수")]
    [SerializeField] private float detectionPenalty = 1500f;

    [Header("Kill Bonus Settings")]
    [Tooltip("🎯 적 1명 처치 시 획득하는 보너스 점수! (위험을 무릅쓴 제압 보상)")]
    [SerializeField] private float killBonus = 500f;

    [Header("Health Bonus Settings")]
    [Tooltip("체력이 100% 보존되었을 때 받을 수 있는 최대 체력 보너스")]
    [SerializeField] private float maxHealthBonus = 2000f;

    [Header("Rank Thresholds")]
    [SerializeField] private float sRankThreshold = 12000f;
    [SerializeField] private float aRankThreshold = 9000f;
    [SerializeField] private float bRankThreshold = 6000f;

    // 실시간 기록 변수
    private float elapsedTime = 0f;
    private int detectionCount = 0;
    private int killCount = 0;
    private bool isTimerRunning = false;

    // 프로퍼티
    public float ElapsedTime => elapsedTime;
    public int DetectionCount => detectionCount;
    public int KillCount => killCount;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        StartScoreTracking();
    }

    private void Update()
    {
        if (isTimerRunning)
        {
            elapsedTime += Time.deltaTime;
        }
    }

    /// <summary>
    /// 게임 시작 시 타이머 및 기록 리셋
    /// </summary>
    public void StartScoreTracking()
    {
        elapsedTime = 0f;
        detectionCount = 0;
        killCount = 0;
        isTimerRunning = true;
        Debug.Log("⏱️ [ScoreManager] 점수 측정 시작!");
    }

    /// <summary>
    /// 적에게 발각(Alerted)되었을 때 호출
    /// </summary>
    public void AddDetection()
    {
        detectionCount++;
        Debug.Log($"🚨 [ScoreManager] 감지 횟수 증가! 현재 감지: {detectionCount}회");
    }

    /// <summary>
    /// 적을 처치했을 때 호출
    /// </summary>
    public void AddKill()
    {
        killCount++;
        Debug.Log($"⚔️ [ScoreManager] 적 제압 성공! 보너스 누적 (현재 처치: {killCount}명)");
    }

    /// <summary>
    /// 탈출 성공 시 최종 점수 및 랭크 산출
    /// </summary>
    public ScoreResult CalculateFinalScore(float currentHP, float maxHP)
    {
        isTimerRunning = false;

        // 1. 타임 점수 연산 (5분 기준)
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

        // 2. 감지 감점 연산
        float totalDetectionPenalty = detectionCount * detectionPenalty;

        // 3. 🎯 [수정 완료] 처치 보너스 연산! (처치한 수만큼 점수 추가 가산)
        float totalKillBonus = killCount * killBonus;

        // 4. 잔여 체력 보너스 연산
        float hpRatio = Mathf.Clamp01(currentHP / maxHP);
        float healthBonus = hpRatio * maxHealthBonus;

        // 5. 최종 총점 계산 (+ totalKillBonus 가산!)
        float totalScore = Mathf.Max(0f, baseScore + timeScore - totalDetectionPenalty + totalKillBonus + healthBonus);

        // 6. 랭크 판정
        Rank finalRank = Rank.C;
        if (totalScore >= sRankThreshold) finalRank = Rank.S;
        else if (totalScore >= aRankThreshold) finalRank = Rank.A;
        else if (totalScore >= bRankThreshold) finalRank = Rank.B;

        ScoreResult result = new ScoreResult
        {
            finalScore = Mathf.RoundToInt(totalScore),
            rank = finalRank,
            clearTime = elapsedTime,
            detectionCount = detectionCount,
            killCount = killCount,
            timeBonusOrPenalty = Mathf.RoundToInt(timeScore),
            detectionPenalty = Mathf.RoundToInt(totalDetectionPenalty),
            killBonus = Mathf.RoundToInt(totalKillBonus),
            healthBonus = Mathf.RoundToInt(healthBonus)
        };

        Debug.Log($"🏆 [ScoreManager] 최종 점수: {result.finalScore} | 랭크: {result.rank}");
        return result;
    }
}

/// <summary>
/// UI 연동용 결과 데이터 구조체
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
    public int killBonus; // 🎯 killPenalty 대신 killBonus 로 변경!
    public int healthBonus;
}