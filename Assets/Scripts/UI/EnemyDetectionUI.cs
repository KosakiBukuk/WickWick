using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 🎯 [적 머리 위 발각 마름모 UI 연동 모듈]
/// - 0%: UI 완전 숨김
/// - >0%: 마름모 틀(BG) 표시 + 안쪽에 노란색 게이지가 위로 차오름 (Fill)
/// - >=30%: 중앙 눈 아이콘 짠! + 수색 경고음
/// - 게이지 상승 시 노란색 -> 주황색 -> 빨간색으로 부드럽게 물듬!
/// </summary>
public class EnemyDetectionUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("부모의 EnemyAI 스크립트 (비워두면 자동 캐싱)")]
    [SerializeField] private EnemyAI enemyAI;

    [Tooltip("고정된 마름모 배경/테두리 Image")]
    [SerializeField] private Image bgImage;

    [Tooltip("안쪽에서 차오를 게이지 Image (Image Type = Filled 세팅 필수!)")]
    [SerializeField] private Image fillImage;

    [Tooltip("마름모 중앙 '눈 아이콘' GameObject")]
    [SerializeField] private GameObject eyeIconObject;

    [Header("Audio Settings")]
    [Tooltip("30% 수색(Suspicious) 상태 진입 시 울릴 경고 SFX")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip suspiciousSFX;

    [Header("Color Settings")]
    [Tooltip("기본 게이지 색상 (노란색)")]
    [SerializeField] private Color normalColor = new Color(1f, 0.92f, 0.016f, 1f);

    [Tooltip("발각(Alerted) 게이지 색상 (빨간색)")]
    [SerializeField] private Color alertColor = new Color(1f, 0f, 0f, 1f);

    private Transform mainCameraTransform;
    private bool hasPlayedSuspiciousSFX = false;

    private void Awake()
    {
        if (enemyAI == null)
            enemyAI = GetComponentInParent<EnemyAI>();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    private void Start()
    {
        if (Camera.main != null)
            mainCameraTransform = Camera.main.transform;

        // 시작 시 UI 요소 모두 숨기기
        SetUIVisible(false);
    }

    private void Update()
    {
        UpdateDetectionUI();
    }

    private void LateUpdate()
    {
        // 🎯 빌보드: 카메라 정면 바라보기
        if (mainCameraTransform != null)
        {
            transform.rotation = Quaternion.LookRotation(transform.position - mainCameraTransform.position);
        }
    }

    /// <summary>
    /// UI 표시/숨김 일괄 처리
    /// </summary>
    private void SetUIVisible(bool visible)
    {
        if (bgImage != null) bgImage.enabled = visible;
        if (fillImage != null) fillImage.enabled = visible;
        if (!visible && eyeIconObject != null) eyeIconObject.SetActive(false);
    }

    /// <summary>
    /// 🎯 실시간 게이지 연산 및 연출
    /// </summary>
    private void UpdateDetectionUI()
    {
        if (enemyAI == null || fillImage == null) return;

        float gauge = enemyAI.CurrentDetectionGauge; // 0 ~ 100
        float gaugePercent = Mathf.Clamp01(gauge / 100f);

        // 1. 🎯 0%일 때는 틀과 채우기 모두 숨김!
        if (gauge <= 0f)
        {
            SetUIVisible(false);
            hasPlayedSuspiciousSFX = false;
            return;
        }

        // 0% 초과면 마름모 틀과 채우기 켜기!
        SetUIVisible(true);

        // 2. 🎯 게이지 비율(0~1)에 따라 아래에서 위로 차오름!
        fillImage.fillAmount = gaugePercent;

        // 3. 🎯 게이지 비율에 따라 노란색 -> 주황색 -> 빨간색으로 물듬!
        if (enemyAI.CurrentState == EnemyAI.State.Alerted || gauge >= 100f)
        {
            fillImage.color = alertColor; // 100% 완전한 빨간색!
            fillImage.fillAmount = 1.0f;
            if (eyeIconObject != null && !eyeIconObject.activeSelf)
                eyeIconObject.SetActive(true);
        }
        else
        {
            fillImage.color = Color.Lerp(normalColor, alertColor, gaugePercent);

            // 🎯 30% 이상 진입 시 눈 아이콘 + 사운드!
            if (gauge >= 30f)
            {
                if (eyeIconObject != null && !eyeIconObject.activeSelf)
                    eyeIconObject.SetActive(true);

                if (!hasPlayedSuspiciousSFX)
                {
                    hasPlayedSuspiciousSFX = true;
                    if (audioSource != null && suspiciousSFX != null)
                    {
                        audioSource.PlayOneShot(suspiciousSFX);
                    }
                    Debug.Log($"👁️ [{enemyAI.gameObject.name}] 게이지 30% 달성! 수색 경고음 & 눈 아이콘!");
                }
            }
            else
            {
                if (eyeIconObject != null && eyeIconObject.activeSelf)
                    eyeIconObject.SetActive(false);

                hasPlayedSuspiciousSFX = false;
            }
        }
    }
}