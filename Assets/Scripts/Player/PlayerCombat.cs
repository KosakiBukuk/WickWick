using System;
using System.Collections;
using UnityEngine;

public class PlayerCombat : MonoBehaviour
{
    public enum WeaponType { Dagger, Gun, Throwable }

    [Header("Weapon Settings")]
    [SerializeField] private WeaponType currentWeapon = WeaponType.Dagger;
    [SerializeField] private Transform playerCamera;

    [Header("Dagger Settings")]
    [SerializeField] private float daggerRange = 2.0f;
    [SerializeField] private float daggerDamage = 35.0f;
    [SerializeField] private float daggerBackstabDamage = 999.0f;
    [SerializeField] private float daggerCooldown = 0.8f;
    [SerializeField] private float backstabAngleThreshold = 60.0f;

    [Header("Gun Ammo Settings")]
    [SerializeField] private float gunRange = 50.0f;
    [SerializeField] private float gunDamage = 35.0f;
    [SerializeField] private float gunCooldown = 0.25f;
    [SerializeField] private float gunNoiseRadius = 20.0f;

    [SerializeField] private int maxAmmoPerMag = 12;
    [SerializeField] private int currentAmmo = 12;
    [SerializeField] private int reserveMagazines = 2;
    [SerializeField] private float reloadTime = 2.5f;

    [Header("Throwable Settings")]
    [SerializeField] private Transform throwPoint;
    [SerializeField] private float throwForce = 15.0f;
    [SerializeField] private float pickupRange = 2.5f;

    // 🎯 동적으로 획득한 던질 오브젝트 프리팹 저장소
    private GameObject currentThrowablePrefab;
    private bool hasThrowable = false;

    private bool isReloading = false;
    private float lastDaggerTime = -999f;
    private float lastGunTime = -999f;
    private WeaponType previousWeapon = WeaponType.Dagger;

    public event Action<WeaponType> OnWeaponChanged;
    public event Action OnAttack;
    public event Action OnThrow;
    public event Action<bool> OnThrowableStateChanged;
    public event Action<int, int> OnAmmoChanged;
    public event Action<float> OnReloadStart;
    public event Action OnReloadComplete;
    public event Action OnEmptyGunAttempt;

    public bool HasThrowable => hasThrowable;
    public WeaponType CurrentWeapon => currentWeapon;
    public int CurrentAmmo => currentAmmo;
    public int ReserveMagazines => reserveMagazines;
    public bool IsReloading => isReloading;

    private void Start()
    {
        if (playerCamera == null)
            playerCamera = Camera.main.transform;

        // 🎯 throwPoint가 비어있다면 카메라 앞 0.8m 지점에 자동으로 생성!
        if (throwPoint == null && playerCamera != null)
        {
            GameObject autoPoint = new GameObject("AutoThrowPoint");
            autoPoint.transform.SetParent(playerCamera);
            autoPoint.transform.localPosition = new Vector3(0f, -0.2f, 0.8f);
            autoPoint.transform.localRotation = Quaternion.identity;
            throwPoint = autoPoint.transform;
            Debug.Log("🎯 [PlayerCombat] ThrowPoint가 비어있어 카메라 정면에 자동 생성했습니다!");
        }

        OnAmmoChanged?.Invoke(currentAmmo, reserveMagazines);
    }

    private void Update()
    {
        if (isReloading) return;

        HandleWeaponSwitch();
        HandleReloadInput();
        HandleAttack();
        HandleInteraction();
    }

    private void HandleWeaponSwitch()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1) && currentWeapon != WeaponType.Dagger)
        {
            SwitchWeapon(WeaponType.Dagger);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2) && currentWeapon != WeaponType.Gun)
        {
            SwitchWeapon(WeaponType.Gun);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3) && currentWeapon != WeaponType.Throwable)
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

    private void HandleReloadInput()
    {
        if (Input.GetKeyDown(KeyCode.R) && currentWeapon == WeaponType.Gun && !isReloading)
        {
            if (reserveMagazines <= 0)
            {
                Debug.Log("⚠️ 남은 예비 탄창이 없습니다!");
                OnEmptyGunAttempt?.Invoke();
            }
            else if (currentAmmo < maxAmmoPerMag)
            {
                StartCoroutine(ReloadRoutine());
            }
        }
    }

    private IEnumerator ReloadRoutine()
    {
        isReloading = true;
        Debug.Log($"🔄 [재장전 시작] {reloadTime}초 소요...");
        OnReloadStart?.Invoke(reloadTime);

        yield return new WaitForSeconds(reloadTime);

        reserveMagazines--;
        currentAmmo = maxAmmoPerMag;
        isReloading = false;

        Debug.Log($"✅ [재장전 완료] 남은 탄약: {currentAmmo}/{maxAmmoPerMag} | 예비 탄창: {reserveMagazines}");
        OnAmmoChanged?.Invoke(currentAmmo, reserveMagazines);
        OnReloadComplete?.Invoke();
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
            else if (currentWeapon == WeaponType.Gun)
            {
                if (Time.time >= lastGunTime + gunCooldown)
                {
                    PerformGunAttack();
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
        }
    }

    private void PerformGunAttack()
    {
        if (currentAmmo <= 0)
        {
            if (reserveMagazines > 0 && !isReloading)
            {
                StartCoroutine(ReloadRoutine());
            }
            else if (reserveMagazines <= 0)
            {
                Debug.Log("🚫 [딸깍] 탄약과 예비 탄창이 모두 소진되었습니다!");
                OnEmptyGunAttempt?.Invoke();
            }
            return;
        }

        lastGunTime = Time.time;
        currentAmmo--;
        OnAmmoChanged?.Invoke(currentAmmo, reserveMagazines);
        OnAttack?.Invoke();

        Debug.Log($"🔫 탕! 총기 사격! (남은 탄약: {currentAmmo}/{maxAmmoPerMag})");

        RaycastHit hit;
        if (Physics.Raycast(playerCamera.position, playerCamera.forward, out hit, gunRange))
        {
            Debug.Log($"🔫 총알 명중: {hit.collider.name}");
        }

        MakeNoise(transform.position, gunNoiseRadius);
    }

    // 📦 ThrowableItem에서 주운 바로 그 프리팹을 전달받음!
    public void PickupThrowable(GameObject prefab)
    {
        hasThrowable = true;
        currentThrowablePrefab = prefab; // 주운 동적 프리팹 저장
        OnThrowableStateChanged?.Invoke(true);
        Debug.Log($"📦 [상호작용] {prefab.name} 획득! (3번 키로 장착 가능)");
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

        // 🎯 주웠던 바로 그 프리팹을 던지기 지점에 소환해서 날림!
        GameObject thrownObj = Instantiate(currentThrowablePrefab, throwPoint.position, throwPoint.rotation);

        // 투척물에 Rigidbody가 없으면 자동으로 붙여줘서 물리 적용!
        Rigidbody rb = thrownObj.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = thrownObj.AddComponent<Rigidbody>();
        }

        rb.AddForce(playerCamera.forward * throwForce, ForceMode.Impulse);

        Debug.Log($"🧱 {currentThrowablePrefab.name} 투척 완료!");

        currentThrowablePrefab = null; // 초기화
        SwitchWeapon(previousWeapon);  // 이전 무기로 스왑
    }

    public void AddMagazine(int count = 1)
    {
        reserveMagazines += count;
        Debug.Log($"🔋 탄창 획득! (+{count}) 현재 예비 탄창: {reserveMagazines}");
        OnAmmoChanged?.Invoke(currentAmmo, reserveMagazines);
    }

    private void MakeNoise(Vector3 noisePosition, float radius)
    {
        Debug.Log($"🔊 [소음 발생!] 위치: {noisePosition}, 반경: {radius}m");
    }
}