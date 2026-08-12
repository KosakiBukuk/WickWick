using UnityEngine;

/// <summary>
/// 🎯 [암살 UI 전담 모듈] 
/// PlayerCombat의 CanAssassinateTarget() 상태를 감지하여 암살 UI를 On/Off 시킵니다.
/// </summary>
public class AssassinateUI : MonoBehaviour
{
    [Header("Player Combat Reference")]
    [Tooltip("플레이어의 PlayerCombat 스크립트를 드래그해 넣으세요. (비워두면 자동 탐색)")]
    [SerializeField] private PlayerCombat playerCombat;

    [Header("UI Group Reference")]
    [Tooltip("화면에 껐다 켰다 할 AssassinateUI 패널(또는 자식 오브젝트)을 드래그해 넣으세요.")]
    [SerializeField] private GameObject uiGroup;

    private void Start()
    {
        // 1. PlayerCombat 자동 캐싱
        if (playerCombat == null)
        {
            playerCombat = FindFirstObjectByType<PlayerCombat>();
        }

        // 2. uiGroup을 따로 지정하지 않았다면 자기 자신을 지정!
        if (uiGroup == null)
        {
            uiGroup = gameObject;
        }

        // 3. 게임 시작 시에는 암살 UI 일단 숨겨두기!
        if (uiGroup != null)
        {
            uiGroup.SetActive(false);
        }
    }

    private void Update()
    {
        if (playerCombat == null || uiGroup == null) return;

        // 🎯 플레이어가 현재 적 뒤 암살 가능한 위치인지 체크!
        bool canAssassinate = playerCombat.CanAssassinateTarget();

        // 상태가 바뀔 때만 SetActive 호출 (매 프레임 호출 방지로 성능 최적화!)
        if (uiGroup.activeSelf != canAssassinate)
        {
            uiGroup.SetActive(canAssassinate);
        }
    }
}