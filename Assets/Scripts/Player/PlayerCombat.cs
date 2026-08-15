using System;
using UnityEngine;

/// <summary>
/// 전투 및 상호작용 전담 모듈.
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
    [SerializeField] private float daggerRange = 2.2f; // 살짝 여유있게 보정!
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

    private IInteractable currentHoveredInteractable = null;
    private bool isAssassinateAvailable = false; // 🎯 현재 암살 가능 상태 캐싱

    public event Action<WeaponType> OnWeaponChanged;
    public event Action OnAttack;
    public event Action OnThrow;
    public event Action<bool> OnThrowableStateChanged;

    // 🎯 UI 연동 이벤트
    public event Action<bool, string> OnInteractableHovered; // E키 습득 안내
    public event Action<bool> OnAssassinateHovered;           // 🌟 암살 가능 UI 팝업 이벤트!

    public bool HasThrowable => hasThrowable;
    public WeaponType CurrentWeapon => currentWeapon;
    public bool IsAssassinateAvailable => isAssassinateAvailable;

    private void Awake()
    {
        if (playerController == null)
            playerController = GetComponent<PlayerController>();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
    }

    private void Start()
    {
        if (playerCamera == null && Camera.main != null)
            playerCamera = Camera.main.transform;

        if (throwPoint == null && playerCamera != null)
        {
            GameObject autoPoint = new GameObject("AutoThrowPoint");
            autoPoint.transform.SetParent(playerCamera);
            autoPoint.transform.localPosition = new Vector3(0.2f, -0.2f, 0.6f);
            autoPoint.transform.localRotation = Quaternion.identity;
            throwPoint = autoPoint.transform;
        }
    }

    private void Update()
    {
        HandleWeaponSwitch();
        HandleAttack();
        HandleInteractionDetection(); // E키 상호작용
        HandleAssassinationDetection(); // 🌟 [핵심 추가] 실시간 암살 가능 여부 감지 & UI 갱신!
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

    /// <summary>
    /// 🎯 [실시간 암살 감지] 매 프레임 적 뒤에 있는지 체크하여 UI 이벤트를 발송합니다.
    /// </summary>
    private void HandleAssassinationDetection()
    {
        bool canAssassinateNow = CanAssassinateTarget();

        if (canAssassinateNow != isAssassinateAvailable)
        {
            isAssassinateAvailable = canAssassinateNow;
            OnAssassinateHovered?.Invoke(isAssassinateAvailable); // 🌟 UI 스크립트로 상태 전송!
        }
    }

    public bool IsStrictBackstabTarget(EnemyAI enemyAI, EnemyHealth enemyHealth)
    {
        if (enemyHealth == null || enemyHealth.IsDead) return false;
        if (enemyAI != null && enemyAI.IsAlerted) return false;

        Transform enemyTransform = enemyAI != null ? enemyAI.transform : enemyHealth.transform;

        Vector3 enemyForward = enemyTransform.forward;
        Vector3 playerForward = playerCamera != null ? playerCamera.forward : transform.forward;
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
        if (currentWeapon != WeaponType.Dagger || playerCamera == null) return false;

        RaycastHit hit;
        // 🌟 트리거 콜라이더를 무시하고 실제 몸체 콜라이더만 검사!
        if (Physics.Raycast(playerCamera.position, playerCamera.forward, out hit, daggerRange, ~0, QueryTriggerInteraction.Ignore))
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
        if (Physics.Raycast(playerCamera.position, playerCamera.forward, out hit, daggerRange, ~0, QueryTriggerInteraction.Ignore))
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

    private void HandleInteractionDetection()
    {
        if (playerCamera == null) return;

        RaycastHit hit;
        IInteractable interactable = null;

        if (Physics.Raycast(playerCamera.position, playerCamera.forward, out hit, pickupRange, ~0, QueryTriggerInteraction.Ignore))
        {
            interactable = hit.collider.GetComponent<IInteractable>();
        }

        if (interactable != currentHoveredInteractable)
        {
            currentHoveredInteractable = interactable;

            if (currentHoveredInteractable != null)
            {
                string promptText = hasThrowable ? "SLOT FULL (1/1)" : "PRESS [E] TO PICK UP";
                OnInteractableHovered?.Invoke(true, promptText);
            }
            else
            {
                OnInteractableHovered?.Invoke(false, string.Empty);
            }
        }

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