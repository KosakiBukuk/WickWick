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

    private CharacterController controller;
    private Vector3 velocity;
    private float xRotation = 0f;

    private bool isCrouching = false;
    private bool isSprinting = false;

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
            Vector3 initialCamPos = playerCamera.localPosition;
            initialCamPos.y = standingCameraY;
            playerCamera.localPosition = initialCamPos;
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

        if (isCrouching) isCrouching = crouchKey;
        else if (isSprinting) isSprinting = sprintKey;
        else
        {
            if (crouchKey) isCrouching = true;
            else if (sprintKey) isSprinting = true;
        }
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
}