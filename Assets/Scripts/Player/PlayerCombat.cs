using System;
using UnityEngine;

/// <summary>
/// [전투 및 상호작용 전담 모듈] 근접 단검 공격, 백스탭, E키 상호작용, 슬롯 전환(1:단검, 2:투척물) 및 투척
/// 🎯 [암살 보강] 적 Alerted 상태 체크 + 완벽한 등 뒤(시선 & 위치) 조건 엄격 검증!
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

    // ========================================================================
    // 🔊 [Combat Audio Settings]
    // ========================================================================
    [Header("🔊 Combat Audio Settings")]
    [Tooltip("공격 사운드가 출력될 AudioSource (비워두면 자동 찾기)")]
    [SerializeField] private AudioSource audioSource;

    [Space(5)]
    [Tooltip("단검 휘두르기(허공 가르기) SFX")]
    [SerializeField] private AudioClip daggerSwingSFX;

    [Tooltip("적 일반 타격 SFX")]
    [SerializeField] private AudioClip daggerHitSFX;

    [Tooltip("뒤에서 암살(백스탭) 시 시원하게 터지는 SFX")]
    [SerializeField] private AudioClip daggerBackstabSFX;

    [Range(0f, 1f)][SerializeField] private float attackAudioVolume = 1.0f;

    private GameObject currentThrowablePrefab;
    private bool hasThrowable = false;
    private float lastDaggerTime = -999f;

    public event Action<WeaponType> OnWeaponChanged;
    public event Action OnAttack;
    public event Action OnThrow;
    public event Action<bool> OnThrowableStateChanged;

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
        HandleInteraction();
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
    /// 🎯 [암살/백스탭 성립 엄격 검증 메서드]
    /// 1. 적 사망 여부 체크
    /// 2. 적의 Alerted(경계/발각) 상태 체크 -> Alerted면 암살 불가!
    /// 3. 적 시선 방향 vs 플레이어 바라보는 방향 비교 (angle < backstabAngleThreshold)
    /// 4. 적 등 뒤 위치 각도 비교 (positionAngle < 45도) -> 확실한 등 뒤인지 확인
    /// </summary>
    public bool IsStrictBackstabTarget(EnemyAI enemyAI, EnemyHealth enemyHealth)
    {
        if (enemyHealth == null || enemyHealth.IsDead) return false;

        // 🛑 조건 1: 적이 경계/발각(Alerted) 상태라면 암살 불가능!
        if (enemyAI != null && enemyAI.IsAlerted)
        {
            return false;
        }

        Transform enemyTransform = enemyAI != null ? enemyAI.transform : enemyHealth.transform;

        // 🎯 조건 2: 시선 방향 비교 (적 등 뒤를 바라보고 있는지)
        Vector3 enemyForward = enemyTransform.forward;
        Vector3 playerForward = playerCamera.forward;
        float viewAngle = Vector3.Angle(enemyForward, playerForward);

        // 🎯 조건 3: 실제 플레이어 위치가 적의 Z축 뒤쪽(후방 범위)에 존재하는지 판정
        Vector3 dirToPlayer = (transform.position - enemyTransform.position).normalized;
        dirToPlayer.y = 0f; // 수평 위치 비교용
        Vector3 enemyBack = -enemyForward;
        enemyBack.y = 0f;
        float positionAngle = Vector3.Angle(enemyBack, dirToPlayer);

        // 시선 방향 < Threshold(60도) AND 위치 방향 < 45도 조건 동시 만족 필요!
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

        // 🔊 1. 휘두르기 소리 (허공 가르기 SFX)
        if (audioSource != null && daggerSwingSFX != null)
        {
            audioSource.PlayOneShot(daggerSwingSFX, attackAudioVolume);
        }

        RaycastHit hit;
        if (Physics.Raycast(playerCamera.position, playerCamera.forward, out hit, daggerRange))
        {
            EnemyAI enemyAI = hit.transform.GetComponentInParent<EnemyAI>();
            EnemyHealth enemyHealth = hit.transform.GetComponentInParent<EnemyHealth>();

            // 🎯 강화된 백스탭 조건을 통해 암살/일반 공격 분기
            bool isBackstab = IsStrictBackstabTarget(enemyAI, enemyHealth);
            float damage = isBackstab ? daggerBackstabDamage : daggerDamage;

            Debug.Log($"🗡️ 단검 명중! ({hit.collider.name}) | 백스탭: {isBackstab} | 데미지: {damage}");

            if (enemyHealth != null && !enemyHealth.IsDead)
            {
                enemyHealth.TakeDamage(damage);

                // 🔊 2. 적 명중 SFX (백스탭/일반 타격 구분)
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