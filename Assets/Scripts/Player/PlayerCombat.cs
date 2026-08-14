using System;
using UnityEngine;

/// <summary>
/// [전투 및 상호작용 전담 모듈] 근접 단검 공격, 백스탭, E키 상호작용, 슬롯 전환(1:단검, 2:투척물) 및 투척
/// 🎯 [상호작용 보강] 조준 대상 실시간 감지 & E키 습득 UI 팝업 이벤트 지원!
/// </summary>
public class PlayerCombat : MonoBehaviour
{
    public enum WeaponType { Dagger, Throwable }

    [Header("Script References")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private PlayerEquipmentManager equipmentManager;

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
    [SerializeField] private float throwUpwardForce = 2.5f;
    [SerializeField] private float pickupRange = 2.5f;

    [Header("Weapon Motion Reference")]
    [SerializeField] private WeaponAttack weaponAttack;

    [Header("🔊 Combat Audio Settings")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip daggerSwingSFX;
    [SerializeField] private AudioClip daggerHitSFX;
    [SerializeField] private AudioClip daggerBackstabSFX;
    [Range(0f, 1f)][SerializeField] private float attackAudioVolume = 1.0f;

    private GameObject currentThrowablePrefab;
    private bool hasThrowable = false;
    private float lastDaggerTime = -999f;

    // 🎯 [신규] 현재 바라보고 있는 상호작용 대상 캐싱
    private IInteractable currentHoveredInteractable = null;

    public event Action<WeaponType> OnWeaponChanged;
    public event Action OnAttack;
    public event Action OnThrow;
    public event Action<bool> OnThrowableStateChanged;

    // 🎯 [신규] UI 연동용 이벤트 (상호작용 가능 여부, 표시할 안내 텍스트)
    public event Action<bool, string> OnInteractableHovered;

    public bool HasThrowable => hasThrowable;
    public WeaponType CurrentWeapon => currentWeapon;

    private void Awake()
    {
        if (playerController == null)
            playerController = GetComponent<PlayerController>();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    private void Start()
    {
        if (playerCamera == null)
            playerCamera = Camera.main.transform;

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
        HandleInteractionDetection(); // 🎯 매 프레임 감지 & E키 입력 처리
    }

    private void HandleWeaponSwitch()
    {
        if (equipmentManager != null && equipmentManager.IsSwitching) return;

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

    public bool IsStrictBackstabTarget(EnemyAI enemyAI, EnemyHealth enemyHealth)
    {
        if (enemyHealth == null || enemyHealth.IsDead) return false;

        if (enemyAI != null && enemyAI.IsAlerted)
        {
            return false;
        }

        Transform enemyTransform = enemyAI != null ? enemyAI.transform : enemyHealth.transform;

        Vector3 enemyForward = enemyTransform.forward;
        Vector3 playerForward = playerCamera.forward;
        float viewAngle = Vector3.Angle(enemyForward, playerForward);

        Vector3 dirToPlayer = (transform.position - enemyTransform.position).normalized;
        dirToPlayer.y = 0f;
        Vector3 enemyBack = -enemyForward;
        enemyBack.y = 0f;
        float positionAngle = Vector3.Angle(enemyBack, dirToPlayer);

        bool isStrictlyBehind = (viewAngle < backstabAngleThreshold) && (positionAngle < 45f);
        return isStrictlyBehind;
    }

    public bool CanAssassinateTarget()
    {
        if (currentWeapon != WeaponType.Dagger) return false;

        RaycastHit hit;
        if (Physics.Raycast(playerCamera.position, playerCamera.forward, out hit, daggerRange))
        {
            EnemyAI enemyAI = hit.transform.GetComponentInParent<EnemyAI>();
            EnemyHealth enemyHealth = hit.transform.GetComponentInParent<EnemyHealth>();

            return IsStrictBackstabTarget(enemyAI, enemyHealth);
        }

        return false;
    }

    private void HandleAttack()
    {
        if (Input.GetMouseButtonDown(0))
        {
            bool canAssassinate = CanAssassinateTarget();

            if (playerController != null && playerController.IsCrouching)
            {
                if (canAssassinate)
                {
                    Debug.Log("🗡️ [PlayerCombat] 앉은 상태에서 암살 개시! 일어서며 백스탭을 실행합니다!");
                    playerController.ForceStandUp();
                    if (weaponAttack != null) weaponAttack.SwingWeapon();
                    PerformDaggerAttack();
                    return;
                }

                Debug.Log("🤫 [PlayerCombat] 앉아있는 상태에서는 일반 공격이나 투척을 할 수 없습니다!");
                return;
            }

            if (currentWeapon == WeaponType.Dagger)
            {
                if (Time.time >= lastDaggerTime + daggerCooldown)
                {
                    PerformDaggerAttack();
                    if (weaponAttack != null) weaponAttack.SwingWeapon();
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

        if (audioSource != null && daggerSwingSFX != null)
        {
            audioSource.PlayOneShot(daggerSwingSFX, attackAudioVolume);
        }

        RaycastHit hit;
        if (Physics.Raycast(playerCamera.position, playerCamera.forward, out hit, daggerRange))
        {
            EnemyAI enemyAI = hit.transform.GetComponentInParent<EnemyAI>();
            EnemyHealth enemyHealth = hit.transform.GetComponentInParent<EnemyHealth>();

            bool isBackstab = IsStrictBackstabTarget(enemyAI, enemyHealth);
            float damage = isBackstab ? daggerBackstabDamage : daggerDamage;

            Debug.Log($"🗡️ 단검 명중! ({hit.collider.name}) | 백스탭: {isBackstab} | 데미지: {damage}");

            if (enemyHealth != null && !enemyHealth.IsDead)
            {
                enemyHealth.TakeDamage(damage);

                if (audioSource != null)
                {
                    if (isBackstab && daggerBackstabSFX != null)
                    {
                        audioSource.PlayOneShot(daggerBackstabSFX, attackAudioVolume);
                    }
                    else if (daggerHitSFX != null)
                    {
                        audioSource.PlayOneShot(daggerHitSFX, attackAudioVolume);
                    }
                }
            }
        }
    }

    /// <summary>
    /// 🎯 [실시간 상호작용 감지 및 E키 입력 처리]
    /// </summary>
    private void HandleInteractionDetection()
    {
        RaycastHit hit;
        IInteractable interactable = null;

        // 1. 카메라 정면 레이캐스트
        if (Physics.Raycast(playerCamera.position, playerCamera.forward, out hit, pickupRange))
        {
            interactable = hit.collider.GetComponent<IInteractable>();
        }

        // 2. 조준 대상 변경 감지 & UI 텍스트 전송
        if (interactable != currentHoveredInteractable)
        {
            currentHoveredInteractable = interactable;

            if (currentHoveredInteractable != null)
            {
                // 🎯 [수정] NEMESYS 폰트 스타일에 맞춘 깔끔한 영문 대문자 문구!
                string promptText = hasThrowable ? "SLOT FULL (1/1)" : "PRESS [E] TO PICK UP";
                OnInteractableHovered?.Invoke(true, promptText);
            }
            else
            {
                OnInteractableHovered?.Invoke(false, string.Empty);
            }
        }

        // 3. E키 입력 시 상호작용 실행
        if (Input.GetKeyDown(KeyCode.E) && currentHoveredInteractable != null)
        {
            currentHoveredInteractable.Interact(gameObject);

            currentHoveredInteractable = null;
            OnInteractableHovered?.Invoke(false, string.Empty);
        }
    }

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

        SwitchWeapon(WeaponType.Throwable);
        Debug.Log($"📦 [상호작용] {prefab.name} 획득 완료! (2번 슬롯 장착됨)");
        return true;
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

        Vector3 throwDirection = playerCamera.forward * throwForce + Vector3.up * throwUpwardForce;
        rb.AddForce(throwDirection, ForceMode.Impulse);

        Debug.Log($"💥 {currentThrowablePrefab.name} 투척 완료!");

        currentThrowablePrefab = null;
        SwitchWeapon(WeaponType.Dagger);
    }
}