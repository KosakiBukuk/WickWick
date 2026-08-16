using UnityEngine;

/// <summary>
/// [이동 전담 모듈] 1인칭 플레이어 이동, 마우스 회전, 달리기, 앉기(Crouch - 높이 및 메쉬 보정)
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = 4.5f;
    [SerializeField] private float runSpeed = 7.5f;
    [SerializeField] private float crouchSpeed = 2.0f;
    [SerializeField] private float jumpHeight = 1.2f;
    [SerializeField] private float gravity = -19.62f;

    [Header("Look Settings")]
    [SerializeField] private float mouseSensitivity = 2.0f;
    [SerializeField] private Transform playerCamera;
    [SerializeField] private float minXAngle = -85.0f;
    [SerializeField] private float maxXAngle = 85.0f;

    [Header("Crouch Settings")]
    [SerializeField] private float standingHeight = 2.0f;
    [SerializeField] private float crouchingHeight = 1.0f;
    [SerializeField] private float crouchTransitionSpeed = 10.0f;
    [SerializeField] private float standingCameraY = 1.6f;
    [SerializeField] private float crouchingCameraY = 0.8f;

    [Header("Visual Mesh (Optional)")]
    [SerializeField] private Transform visualMesh;

    [Header("Zone Settings")]
    [Tooltip("현재 플레이어가 위치한 구역 (0 = Zone 0 튜토리얼, 1 = Zone 1...)")]
    [SerializeField] private int currentZone = 0;

    private CharacterController controller;
    private Vector3 velocity;
    private float xRotation = 0f;

    private bool isCrouching = false;
    private bool isSprinting = false;

    public bool IsCrouching => isCrouching;
    public bool IsSprinting => isSprinting;

    // 외부에서 현재 구역을 읽을 수 있게 해주는 프로퍼티
    public int CurrentZone => currentZone;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (playerCamera != null)
        {
            Vector3 initialCamPos = playerCamera.localPosition;
            initialCamPos.y = standingCameraY;
            playerCamera.localPosition = initialCamPos;
        }
    }

    private void Update()
    {
        // 튜토리얼 창이 떠서 시간이 멈춘 상태(Time.timeScale == 0)라면 조작/카메라 회전을 중단
        if (Time.timeScale == 0f) return;

        HandleMouseLook();
        HandleStateInputs();
        HandleMovement();
        HandleCrouchHeight();
    }

    private void HandleMouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, minXAngle, maxXAngle);

        if (playerCamera != null)
        {
            playerCamera.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        }

        transform.Rotate(Vector3.up * mouseX);
    }

    private void HandleStateInputs()
    {
        // 1. C키 또는 LeftControl 키를 눌렀을 때(GetKeyDown) 앉기 상태 반전
        if (Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.C))
        {
            isCrouching = !isCrouching;

            // 앉을 때는 달리기(Sprint) 자동 해제!
            if (isCrouching)
            {
                isSprinting = false;
            }
        }

        // 2. 달리기는 LeftShift를 누르는 동안만 발동 (앉아있을 땐 불가능)
        if (!isCrouching)
        {
            isSprinting = Input.GetKey(KeyCode.LeftShift);
        }
        else
        {
            isSprinting = false;
        }
    }

    /// <summary>
    /// 암살 시 강제로 앉은 자세를 풀고 일어서게 만드는 메서드
    /// </summary>
    public void ForceStandUp()
    {
        isCrouching = false;
    }

    private void HandleMovement()
    {
        if (controller.isGrounded)
        {
            if (velocity.y < 0) velocity.y = -2f;
            if (Input.GetButtonDown("Jump") && !isCrouching)
            {
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
        }

        float x = 0f;
        float z = 0f;

        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) x += 1f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) x -= 1f;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) z += 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) z -= 1f;

        float currentSpeed = walkSpeed;
        if (isCrouching) currentSpeed = crouchSpeed;
        else if (isSprinting) currentSpeed = runSpeed;

        Vector3 moveInput = (transform.right * x + transform.forward * z).normalized;
        Vector3 horizontalMove = moveInput * currentSpeed;

        velocity.y += gravity * Time.deltaTime;
        Vector3 finalMove = horizontalMove + velocity;
        controller.Move(finalMove * Time.deltaTime);
    }

    private void HandleCrouchHeight()
    {
        float targetHeight = isCrouching ? crouchingHeight : standingHeight;
        float targetCamY = isCrouching ? crouchingCameraY : standingCameraY;

        controller.height = Mathf.Lerp(controller.height, targetHeight, Time.deltaTime * crouchTransitionSpeed);
        controller.center = new Vector3(0, controller.height / 2f, 0);

        if (playerCamera != null)
        {
            Vector3 camPos = playerCamera.localPosition;
            camPos.y = Mathf.Lerp(camPos.y, targetCamY, Time.deltaTime * crouchTransitionSpeed);
            playerCamera.localPosition = camPos;
        }

        if (visualMesh != null)
        {
            float meshScaleY = controller.height / standingHeight;
            visualMesh.localScale = new Vector3(1f, meshScaleY, 1f);
            visualMesh.localPosition = new Vector3(0f, controller.height / 2f, 0f);
        }
    }

    /// <summary>
    /// 구역 전환 트리거를 밟았을 때 호출될 함수
    /// </summary>
    public void SetCurrentZone(int newZone)
    {
        currentZone = newZone;
    }
}