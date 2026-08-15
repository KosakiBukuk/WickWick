using UnityEngine;

/// <summary>
/// 1인칭 카메라 헤드밥 모듈.
/// Main Camera의 localPosition(0,0,0)을 기준점으로 삼아 LateUpdate에서 부드럽게 흔듭니다.
/// </summary>
public class CameraBobbing : MonoBehaviour
{
    [Header("Script References")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private CharacterController characterController;

    [Header("Walking Head Bob (걷기)")]
    [Tooltip("걸을 때 흔들림 속도 (빈도)")]
    [SerializeField] private float walkBobSpeed = 11.0f;
    [Tooltip("걸을 때 흔들림 크기 (강도)")]
    [SerializeField] private float walkBobAmount = 0.035f;

    [Header("Running Head Bob (달리기)")]
    [Tooltip("뛸 때 흔들림 속도 (빈도)")]
    [SerializeField] private float runBobSpeed = 16.0f;
    [Tooltip("뛸 때 흔들림 크기 (강도)")]
    [SerializeField] private float runBobAmount = 0.075f;

    [Header("Crouch Head Bob (앉아서 기어가기)")]
    [Tooltip("기어갈 때 흔들림 속도")]
    [SerializeField] private float crouchBobSpeed = 7.0f;
    [Tooltip("기어갈 때 흔들림 크기")]
    [SerializeField] private float crouchBobAmount = 0.012f;

    [Header("Smoothness Settings")]
    [Tooltip("흔들림 보정 보간 속도 (높을수록 반응이 빠르고, 낮을수록 부드러움)")]
    [SerializeField] private float smoothSpeed = 10.0f;

    private float timer = 0f;
    private Vector3 targetBobPosition = Vector3.zero;

    private void Start()
    {
        if (playerController == null) playerController = GetComponentInParent<PlayerController>();
        if (characterController == null) characterController = GetComponentInParent<CharacterController>();

        // Main Camera의 로컬 위치 기준점을 (0,0,0)으로 보정
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
    }

    // Update가 아닌 LateUpdate에서 연산해야 CharacterController 이동 후 미세 떨림(Jitter)이 사라짐
    private void LateUpdate()
    {
        HandleHeadBob();
    }

    private void HandleHeadBob()
    {
        if (playerController == null || characterController == null) return;

        // 실제 수평 이동 속도 체크
        Vector3 horizontalVelocity = new Vector3(characterController.velocity.x, 0f, characterController.velocity.z);
        bool isMoving = horizontalVelocity.sqrMagnitude > 0.1f && characterController.isGrounded;

        if (isMoving)
        {
            float speed = walkBobSpeed;
            float amount = walkBobAmount;

            if (playerController.IsCrouching)
            {
                speed = crouchBobSpeed;
                amount = crouchBobAmount;
            }
            else if (playerController.IsSprinting)
            {
                speed = runBobSpeed;
                amount = runBobAmount;
            }

            timer += Time.deltaTime * speed;

            // 삼각함수로 상하좌우 8자 파동 연산
            float newY = Mathf.Sin(timer) * amount;
            float newX = Mathf.Cos(timer * 0.5f) * amount;

            targetBobPosition = new Vector3(newX, newY, 0f);
        }
        else
        {
            // 멈추면 흔들림 목표 지점을 (0,0,0)으로 초기화
            timer = 0f;
            targetBobPosition = Vector3.zero;
        }

        // Vector3.Lerp로 매 프레임 위치를 부드럽게 보정
        transform.localPosition = Vector3.Lerp(transform.localPosition, targetBobPosition, Time.deltaTime * smoothSpeed);
    }
}