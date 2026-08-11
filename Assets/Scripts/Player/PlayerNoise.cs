using UnityEngine;

/// <summary>
/// 🎯 [PlayerController 연동 모듈]
/// 플레이어의 이동 상태(기어가기/걷기/달리기) 및 CharacterController의 속도를 감지하여
/// 발자국 주기에 따라 주변 적 AI(EnemyAI)에게 소음 파동을 발사합니다.
/// </summary>
[RequireComponent(typeof(PlayerController))]
[RequireComponent(typeof(CharacterController))]
public class PlayerNoise : MonoBehaviour
{
    [Header("Script References")]
    private PlayerController playerController;
    private CharacterController characterController;

    [Header("Noise Radius Settings")]
    [Tooltip("앉아서 기어갈 때 소음 범위 (0 = 완전 무소음)")]
    [SerializeField] private float crouchNoiseRadius = 0f;

    [Tooltip("일반 걷기 시 소음 범위 (적당한 거리)")]
    [SerializeField] private float walkNoiseRadius = 3.0f;

    [Tooltip("달리기(Sprint) 시 소음 범위 (멀리까지 소음 전달!)")]
    [SerializeField] private float runNoiseRadius = 7.0f;

    [Header("Step Interval Settings")]
    [Tooltip("걸을 때 발소리 주기 (초)")]
    [SerializeField] private float walkStepInterval = 0.45f;

    [Tooltip("뛸 때 발소리 주기 (초)")]
    [SerializeField] private float runStepInterval = 0.25f;

    [Header("Layer Settings")]
    [Tooltip("적 AI 오브젝트가 속한 Layer를 지정하세요.")]
    [SerializeField] private LayerMask enemyLayer;

    private float stepTimer = 0f;

    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
        characterController = GetComponent<CharacterController>();
    }

    private void Update()
    {
        HandleFootstepNoise();
    }

    private void HandleFootstepNoise()
    {
        // 1. 공중에 떠 있거나, 실제로 이동하지 않고 제자리에 서 있다면 소음 발생 안 함
        Vector3 horizontalVelocity = new Vector3(characterController.velocity.x, 0f, characterController.velocity.z);
        bool isMoving = horizontalVelocity.sqrMagnitude > 0.01f;

        if (!characterController.isGrounded || !isMoving)
        {
            stepTimer = 0f;
            return;
        }

        // 2. PlayerController의 상태(IsCrouching, IsSprinting) 감지
        float currentRadius = 0f;
        float currentInterval = walkStepInterval;

        // 앉은 상태일 때
        if (playerController.IsCrouching)
        {
            currentRadius = crouchNoiseRadius;
            currentInterval = walkStepInterval * 1.2f;
        }
        // 달리는 상태일 때 (Sprint)
        else if (playerController.IsSprinting)
        {
            currentRadius = runNoiseRadius;
            currentInterval = runStepInterval;
        }
        // 일반 걷기 상태일 때
        else
        {
            currentRadius = walkNoiseRadius;
            currentInterval = walkStepInterval;
        }

        // 소음 범위가 0 이하이면 (무소음 상태) 타이머 리셋 후 진행 안 함
        if (currentRadius <= 0f)
        {
            stepTimer = 0f;
            return;
        }

        // 3. 발자국 주기 타이머 계산 후 소음 파동 발사
        stepTimer += Time.deltaTime;
        if (stepTimer >= currentInterval)
        {
            stepTimer = 0f;
            EmitNoise(currentRadius);
        }
    }

    /// <summary>
    /// 주변 일정 범위 내의 적 AI들에게 소음 위치 전달
    /// </summary>
    private void EmitNoise(float radius)
    {
        Collider[] hitEnemies = Physics.OverlapSphere(transform.position, radius, enemyLayer);

        foreach (Collider col in hitEnemies)
        {
            EnemyAI enemy = col.GetComponentInParent<EnemyAI>();
            if (enemy != null)
            {
                // 적 AI의 소음 감지 함수 호출!
                enemy.OnHearNoise(transform.position);
            }
        }
    }

    // 🎯 씬 뷰(Scene View)에서 플레이어가 달리고/걸을 때 소음 범위를 시각적으로 기즈모로 확인!
    private void OnDrawGizmosSelected()
    {
        if (playerController == null || characterController == null) return;

        Vector3 horizontalVelocity = new Vector3(characterController.velocity.x, 0f, characterController.velocity.z);
        if (!characterController.isGrounded || horizontalVelocity.sqrMagnitude <= 0.01f) return;

        if (playerController.IsSprinting)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, runNoiseRadius);
        }
        else if (!playerController.IsCrouching)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, walkNoiseRadius);
        }
    }
}