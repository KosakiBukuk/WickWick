using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 플레이어 근접 단검 공격, 백스탭 타격, 상호작용 및 투척 전담 모듈 (총기 기능 제거완료)
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
    [SerializeField] private float throwForce = 15.0f;
    [SerializeField] private float pickupRange = 2.5f;

    private GameObject currentThrowablePrefab;
    private bool hasThrowable = false;

    private float lastDaggerTime = -999f;
    private WeaponType previousWeapon = WeaponType.Dagger;

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

        if (throwPoint == null && playerCamera != null)
        {
            GameObject autoPoint = new GameObject("AutoThrowPoint");
            autoPoint.transform.SetParent(playerCamera);
            autoPoint.transform.localPosition = new Vector3(0f, -0.2f, 0.8f);
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

    private void HandleWeaponSwitch()
    {
        // 1번 키: 단검 장착
        if (Input.GetKeyDown(KeyCode.Alpha1) && currentWeapon != WeaponType.Dagger)
        {
            SwitchWeapon(WeaponType.Dagger);
        }
        // 2번 키: 투척물 장착
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
        if (currentWeapon != WeaponType.Throwable)
        {
            previousWeapon = currentWeapon;
        }

        currentWeapon = newWeapon;
        OnWeaponChanged?.Invoke(currentWeapon);
        Debug.Log($"🔄 [무기 교체] 현재 슬롯: {currentWeapon}");
    }

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

            // 🎯 적 부모 오브젝트의 EnemyHealth를 찾아 실시간 데미지 가하기!
            EnemyHealth enemyHealth = hit.transform.GetComponentInParent<EnemyHealth>();
            if (enemyHealth != null && !enemyHealth.IsDead)
            {
                enemyHealth.TakeDamage(damage);
            }
        }
    }

    public void PickupThrowable(GameObject prefab)
    {
        hasThrowable = true;
        currentThrowablePrefab = prefab;
        OnThrowableStateChanged?.Invoke(true);
        Debug.Log($"📦 [상호작용] {prefab.name} 획득! (2번 키로 장착 가능)");
    }

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

    private void ThrowObject()
    {
        if (currentThrowablePrefab == null || throwPoint == null || !hasThrowable) return;

        hasThrowable = false;
        OnThrowableStateChanged?.Invoke(false);
        OnThrow?.Invoke();

        GameObject thrownObj = Instantiate(currentThrowablePrefab, throwPoint.position, throwPoint.rotation);

        Rigidbody rb = thrownObj.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = thrownObj.AddComponent<Rigidbody>();
        }

        rb.AddForce(playerCamera.forward * throwForce, ForceMode.Impulse);

        Debug.Log($"🧱 {currentThrowablePrefab.name} 투척 완료!");

        currentThrowablePrefab = null;
        SwitchWeapon(previousWeapon);
    }
}