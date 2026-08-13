using System.Collections;
using UnityEngine;

/// <summary>
/// 🎯 [플레이어 무기 스위칭 시각 연출 모듈]
/// PlayerCombat의 OnWeaponChanged 이벤트를 수신하여 화면상 무기 Pivot을 스르륵 내리고 올립니다.
/// </summary>
public class PlayerEquipmentManager : MonoBehaviour
{
    [Header("Script References")]
    [SerializeField] private PlayerCombat playerCombat;
    [SerializeField] private WeaponAttack weaponAttack;

    [Header("⚔️ Weapon Pivots (Parent Objects)")]
    [Tooltip("1번 단검의 부모 피벗 (Knife_Pivot)")]
    [SerializeField] private Transform knifePivot;

    [Tooltip("2번 투척물의 부모 피벗 (Throwable_Pivot)")]
    [SerializeField] private Transform throwablePivot;

    [Header("🧯 Switching Motion Settings")]
    [Tooltip("무기 스위칭 내리기/올리기 속도")]
    [SerializeField] private float switchSpeed = 12f;

    [Tooltip("화면 아래로 내려갈 위치 오프셋")]
    [SerializeField] private Vector3 loweredOffset = new Vector3(0f, -0.6f, 0f);

    private PlayerCombat.WeaponType currentVisualWeapon = PlayerCombat.WeaponType.Dagger;
    private bool isSwitching = false;

    private Vector3 knifeOriginPos;
    private Vector3 throwableOriginPos;

    public bool IsSwitching => isSwitching;

    private void Awake()
    {
        if (playerCombat == null)
            playerCombat = GetComponentInParent<PlayerCombat>();

        if (knifePivot != null) knifeOriginPos = knifePivot.localPosition;
        if (throwablePivot != null) throwableOriginPos = throwablePivot.localPosition;
    }

    private void OnEnable()
    {
        // 🎯 PlayerCombat의 무기 변경 이벤트 구독!
        if (playerCombat != null)
        {
            playerCombat.OnWeaponChanged += HandleWeaponChanged;
        }
    }

    private void OnDisable()
    {
        // 이벤트 해제
        if (playerCombat != null)
        {
            playerCombat.OnWeaponChanged -= HandleWeaponChanged;
        }
    }

    private void Start()
    {
        // 시작 시 투척물 피벗은 화면 아래로 내려서 숨기기!
        if (throwablePivot != null)
        {
            throwablePivot.localPosition = throwableOriginPos + loweredOffset;
            throwablePivot.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 🎯 PlayerCombat에서 슬롯이 바뀌면 호출되는 이벤트 수신기!
    /// </summary>
    private void HandleWeaponChanged(PlayerCombat.WeaponType newWeapon)
    {
        if (currentVisualWeapon != newWeapon)
        {
            StopAllCoroutines();
            StartCoroutine(SwitchWeaponRoutine(newWeapon));
        }
    }

    private IEnumerator SwitchWeaponRoutine(PlayerCombat.WeaponType targetWeapon)
    {
        isSwitching = true;

        Transform currentPivot = (currentVisualWeapon == PlayerCombat.WeaponType.Dagger) ? knifePivot : throwablePivot;
        Vector3 currentOrigin = (currentVisualWeapon == PlayerCombat.WeaponType.Dagger) ? knifeOriginPos : throwableOriginPos;

        Transform targetPivot = (targetWeapon == PlayerCombat.WeaponType.Dagger) ? knifePivot : throwablePivot;
        Vector3 targetOrigin = (targetWeapon == PlayerCombat.WeaponType.Dagger) ? knifeOriginPos : throwableOriginPos;

        // 1. 📉 현재 무기 피벗을 화면 아래로 스르륵 내리기
        if (currentPivot != null && currentPivot.gameObject.activeSelf)
        {
            float t = 0f;
            Vector3 startPos = currentPivot.localPosition;
            Vector3 hidePos = currentOrigin + loweredOffset;

            while (t < 1f)
            {
                t += Time.deltaTime * switchSpeed;
                currentPivot.localPosition = Vector3.Lerp(startPos, hidePos, t);
                yield return null;
            }
            currentPivot.gameObject.SetActive(false);
        }

        // 2. 📈 타겟 무기 피벗 활성화 후 화면 아래에서 올리기
        currentVisualWeapon = targetWeapon;
        if (targetPivot != null)
        {
            targetPivot.gameObject.SetActive(true);
            targetPivot.localPosition = targetOrigin + loweredOffset;

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime * switchSpeed;
                targetPivot.localPosition = Vector3.Lerp(targetOrigin + loweredOffset, targetOrigin, t);
                yield return null;
            }
            targetPivot.localPosition = targetOrigin;
        }

        isSwitching = false;
    }
}