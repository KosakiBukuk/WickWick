using System;
using UnityEngine;

/// <summary>
/// [전투 및 상호작용 전담 모듈] 근접 단검 공격, 백스탭, E키 상호작용, 슬롯 전환(1:단검, 2:투척물) 및 투척
/// </summary>
public class PlayerCombat : MonoBehaviour
{
    public enum WeaponType { Dagger, Throwable }

    [Header("Weapon Settings")]
    [SerializeField] private WeaponType currentWeapon = WeaponType.Dagger;
    [SerializeField] private Transform playerCamera;

    [Header("Dagger Settings")]
    [SerializeField] private float daggerRange = 2.0f;
    [SerializeField] private float daggerDamage = 35.0f;
    [SerializeField] private float daggerBackstabDamage = 999.0f;
    [SerializeField] private float daggerCooldown = 0.8f;
    [SerializeField] private float backstabAngleThreshold = 60.0f;

    [Header("Throwable Settings")]
    [SerializeField] private Transform throwPoint;
    [SerializeField] private float throwForce = 12.0f;
    [SerializeField] private float throwUpwardForce = 2.5f; // 자연스러운 포물선 투척 힘
    [SerializeField] private float pickupRange = 2.5f;

    private GameObject currentThrowablePrefab; // 습득한 투척물의 발사용 프리팹
    private bool hasThrowable = false;          // 최대 소지 수량: 1개
    private float lastDaggerTime = -999f;

    public event Action<WeaponType> OnWeaponChanged;
    public event Action OnAttack;
    public event Action OnThrow;
    public event Action<bool> OnThrowableStateChanged;

    public bool HasThrowable => hasThrowable;
    public WeaponType CurrentWeapon => currentWeapon;

    private void Start()
    {
        if (playerCamera == null)
            playerCamera = Camera.main.transform;

        // ThrowPoint가 없을 경우 카메라 전방에 자동 생성
        if (throwPoint == null && playerCamera != null)
        {
            GameObject autoPoint = new GameObject("AutoThrowPoint");
            autoPoint.transform.SetParent(playerCamera);
            autoPoint.transform.localPosition = new Vector3(0.2f, -0.2f, 0.6f);
            autoPoint.transform.localRotation = Quaternion.identity;
            throwPoint = autoPoint.transform;
            Debug.Log("🎯 [PlayerCombat] ThrowPoint가 비어있어 카메라 정면에 자동 생성했습니다!");
        }
    }

    private void Update()
    {
        HandleWeaponSwitch();
        HandleAttack();
        HandleInteraction();
    }

    /// <summary>
    /// 1번 키(단검), 2번 키(투척물 - 소지 시에만) 슬롯 교체
    /// </summary>
    private void HandleWeaponSwitch()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1) && currentWeapon != WeaponType.Dagger)
        {
            SwitchWeapon(WeaponType.Dagger);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2) && currentWeapon != WeaponType.Throwable)
        {
            if (hasThrowable)
            {
                SwitchWeapon(WeaponType.Throwable);
            }
            else
            {
                Debug.Log("⚠️ 던질 오브젝트가 없습니다! (1개만 소지 가능)");
            }
        }
    }

    private void SwitchWeapon(WeaponType newWeapon)
    {
        currentWeapon = newWeapon;
        OnWeaponChanged?.Invoke(currentWeapon);
        Debug.Log($"🔄 [무기 교체] 현재 슬롯: {currentWeapon}");
    }

    /// <summary>
    /// 마우스 좌클릭 시 슬롯 상태에 따라 공격 또는 투척 실행
    /// </summary>
    private void HandleAttack()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (currentWeapon == WeaponType.Dagger)
            {
                if (Time.time >= lastDaggerTime + daggerCooldown)
                {
                    PerformDaggerAttack();
                }
            }
            else if (currentWeapon == WeaponType.Throwable)
            {
                ThrowObject();
            }
        }
    }

    private void PerformDaggerAttack()
    {
        lastDaggerTime = Time.time;
        OnAttack?.Invoke();
        Debug.Log("🗡️ 단검 휘두르기!");

        RaycastHit hit;
        if (Physics.Raycast(playerCamera.position, playerCamera.forward, out hit, daggerRange))
        {
            Vector3 enemyForward = hit.transform.forward;
            Vector3 attackDirection = playerCamera.forward;

            float angle = Vector3.Angle(enemyForward, attackDirection);
            bool isBackstab = angle < backstabAngleThreshold;
            float damage = isBackstab ? daggerBackstabDamage : daggerDamage;

            Debug.Log($"🗡️ 단검 명중! ({hit.collider.name}) | 백스탭: {isBackstab} | 데미지: {damage}");

            EnemyHealth enemyHealth = hit.transform.GetComponentInParent<EnemyHealth>();
            if (enemyHealth != null && !enemyHealth.IsDead)
            {
                enemyHealth.TakeDamage(damage);
            }
        }
    }

    /// <summary>
    /// E키 입력 시 레이캐스트로 IInteractable(벽돌 등) 탐색 및 상호작용
    /// </summary>
    private void HandleInteraction()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            RaycastHit hit;
            if (Physics.Raycast(playerCamera.position, playerCamera.forward, out hit, pickupRange))
            {
                IInteractable interactable = hit.collider.GetComponent<IInteractable>();
                if (interactable != null)
                {
                    interactable.Interact(gameObject);
                }
            }
        }
    }

    /// <summary>
    /// IInteractable 오브젝트에서 습득 성공 시 호출
    /// </summary>
    public bool PickupThrowable(GameObject prefab)
    {
        if (hasThrowable)
        {
            Debug.Log("🎒 이미 투척물을 소지하고 있습니다!");
            return false;
        }

        hasThrowable = true;
        currentThrowablePrefab = prefab;
        OnThrowableStateChanged?.Invoke(true);

        // 습득 시 편의성을 위해 즉시 투척물 슬롯으로 전환!
        SwitchWeapon(WeaponType.Throwable);
        Debug.Log($"📦 [상호작용] {prefab.name} 획득 완료! (2번 슬롯 장착됨)");
        return true;
    }

    /// <summary>
    /// 투척물 발사 후 소지 해제 및 단검 슬롯 원복
    /// </summary>
    private void ThrowObject()
    {
        if (currentThrowablePrefab == null || throwPoint == null || !hasThrowable) return;

        hasThrowable = false;
        OnThrowableStateChanged?.Invoke(false);
        OnThrow?.Invoke();

        // 물리 투척물 인스턴스화
        GameObject thrownObj = Instantiate(currentThrowablePrefab, throwPoint.position, throwPoint.rotation);

        Rigidbody rb = thrownObj.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = thrownObj.AddComponent<Rigidbody>();
        }

        // 전방 힘 + 상향 포물선 힘 가하기
        Vector3 throwDirection = playerCamera.forward * throwForce + Vector3.up * throwUpwardForce;
        rb.AddForce(throwDirection, ForceMode.Impulse);

        Debug.Log($"💥 {currentThrowablePrefab.name} 투척 완료!");

        // 사용 후 데이터 비우고 기본 단검 슬롯으로 자동 복귀
        currentThrowablePrefab = null;
        SwitchWeapon(WeaponType.Dagger);
    }
}