using UnityEngine;

/// <summary>
/// 1인칭 플레이어 이동, 마우스 회전, 달리기(Sprint), 앉기(Crouch - 높이 및 메쉬 보정) 컨트롤러
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

    [Header("Visual Mesh (Optional)")]
    [SerializeField] private Transform visualMesh; // 바닥 뚫림 방지용 캡슐 메쉬 Transform

    private CharacterController controller;
    private Vector3 velocity;
    private float xRotation = 0f;

    private bool isCrouching = false;
    private bool isSprinting = false;

    private float standingCameraY = 1.6f;
    private float crouchingCameraY = 0.8f;

    public bool IsCrouching => isCrouching;
    public bool IsSprinting => isSprinting;

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
            standingCameraY = playerCamera.localPosition.y;
            crouchingCameraY = standingCameraY * (crouchingHeight / standingHeight);
        }
    }

    private void Update()
    {
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
        bool crouchKey = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C);
        bool sprintKey = Input.GetKey(KeyCode.LeftShift);

        if (isCrouching)
        {
            isCrouching = crouchKey;
        }
        else if (isSprinting)
        {
            isSprinting = sprintKey;
        }
        else
        {
            if (crouchKey)
            {
                isCrouching = true;
            }
            else if (sprintKey)
            {
                isSprinting = true;
            }
        }
    }

    private void HandleMovement()
    {
        // 지면 착지 및 점프 처리
        if (controller.isGrounded)
        {
            if (velocity.y < 0)
            {
                velocity.y = -2f;
            }

            if (Input.GetButtonDown("Jump") && !isCrouching)
            {
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
        }

        // 🎯 [유령 조이스틱 차단] 순수 키보드 전용 입력 감지!
        float x = 0f;
        float z = 0f;

        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) x += 1f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) x -= 1f;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) z += 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) z -= 1f;

        float currentSpeed = walkSpeed;
        if (isCrouching) currentSpeed = crouchSpeed;
        else if (isSprinting) currentSpeed = runSpeed;

        // 이동 방향 계산
        Vector3 moveInput = (transform.right * x + transform.forward * z).normalized;
        Vector3 horizontalMove = moveInput * currentSpeed;

        // 중력 계산
        velocity.y += gravity * Time.deltaTime;

        // 수평 이동과 Y축 중력을 결합하여 이동
        Vector3 finalMove = horizontalMove + velocity;
        controller.Move(finalMove * Time.deltaTime);
    }

    private void HandleCrouchHeight()
    {
        float targetHeight = isCrouching ? crouchingHeight : standingHeight;

        // 🎯 [핵심 수정 2] 목표 높이와 실제 높이에 차이가 있을 때만 Lerp 및 center/mesh 보정 수행!
        if (Mathf.Abs(controller.height - targetHeight) > 0.001f)
        {
            controller.height = Mathf.Lerp(controller.height, targetHeight, Time.deltaTime * crouchTransitionSpeed);
            controller.center = new Vector3(0, controller.height / 2f, 0);

            // 카메라 높이 연동
            if (playerCamera != null)
            {
                float targetCamY = isCrouching ? crouchingCameraY : standingCameraY;
                Vector3 camPos = playerCamera.localPosition;
                camPos.y = Mathf.Lerp(camPos.y, targetCamY, Time.deltaTime * crouchTransitionSpeed);
                playerCamera.localPosition = camPos;
            }

            // 메쉬 위치 보정
            if (visualMesh != null)
            {
                Vector3 meshPos = visualMesh.localPosition;
                meshPos.y = controller.height / 2f;
                visualMesh.localPosition = meshPos;
            }
        }
    }

    /// <summary>
    /// 주변 적들의 FieldOfView 센서로 소음 파동을 발산하는 메서드
    /// </summary>
   /* public void EmitNoise(float radius, FieldOfView.NoiseType noiseType)
    {
        Collider[] nearbyCols = Physics.OverlapSphere(transform.position, radius);
        foreach (var col in nearbyCols)
        {
            FieldOfView fov = col.GetComponent<FieldOfView>();
            if (fov != null)
            {
                fov.ListenNoise(transform.position, radius, noiseType);
            }
        }
    } */

}