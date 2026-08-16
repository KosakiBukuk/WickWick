using UnityEngine;

/// <summary>
/// 암살 UI 전담 모듈.
/// PlayerCombat의 OnAssassinateHovered 이벤트를 수신하여 암살 가능 UI를 On/Off 합니다.
/// </summary>
public class AssassinateUI : MonoBehaviour
{
    [Header("Player Combat Reference")]
    [Tooltip("플레이어의 PlayerCombat 스크립트 (비워둘 시 자동 탐색)")]
    [SerializeField] private PlayerCombat playerCombat;

    [Header("UI Group Reference")]
    [Tooltip("화면에 껐다 켰다 할 실제 UI 패널/아이콘 오브젝트 (자식 오브젝트)")]
    [SerializeField] private GameObject uiGroup;

    private void Awake()
    {
        // 1. PlayerCombat 캐싱
        if (playerCombat == null)
        {
            playerCombat = FindFirstObjectByType<PlayerCombat>();
        }

        // 2. uiGroup이 비어있다면 에러 방지를 위해 자식 오브젝트 중 첫 번째를 자동 할당
        if (uiGroup == null && transform.childCount > 0)
        {
            uiGroup = transform.GetChild(0).gameObject;
        }

        // 3. 시작 시 UI 숨김 처리
        SetUIVisible(false);
    }

    private void OnEnable()
    {
        // 플레이어 이벤트 구독 (암살 상태 실시간 수신)
        if (playerCombat != null)
        {
            playerCombat.OnAssassinateHovered += OnAssassinateStateChanged;
        }
    }

    private void OnDisable()
    {
        // 이벤트 구독 해제 (메모리 누수 방지)
        if (playerCombat != null)
        {
            playerCombat.OnAssassinateHovered -= OnAssassinateStateChanged;
        }
    }

    /// <summary>
    /// PlayerCombat에서 암살 가능 여부가 바뀔 때마다 호출되는 함수
    /// </summary>
    private void OnAssassinateStateChanged(bool canAssassinate)
    {
        SetUIVisible(canAssassinate);
    }

    private void SetUIVisible(bool isVisible)
    {
        if (uiGroup != null)
        {
            uiGroup.SetActive(isVisible);
        }
    }
}