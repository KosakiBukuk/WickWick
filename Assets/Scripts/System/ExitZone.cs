using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using Unity.Cinemachine; // 🎯 유니티 6 시네머신 v3 네임스페이스

/// <summary>
/// 🎯 [스플라인 컷씬, WayPoint 자동 이동, 적 일시정지 & 결과 UI 전환 모듈]
/// </summary>
[RequireComponent(typeof(Collider))]
public class ExitZone : MonoBehaviour
{
    [Header("Cinemachine Camera Settings")]
    [Tooltip("CinemachineCamera 오브젝트를 드래그해 넣으세요.")]
    [SerializeField] private CinemachineCamera exitCamera;
    [Tooltip("컷씬 연출 시 적용할 카메라 Priority 수치")]
    [SerializeField] private int activePriority = 20;
    [Tooltip("카메라가 레일을 따라 이동하는 시간 (초)")]
    [SerializeField] private float cutsceneDuration = 2.0f;

    [Header("Auto Walk Waypoint Settings")]
    [Tooltip("🎯 플레이어가 걸어갈 탈출 목표 위치 (빈 게임오브젝트를 만들어 넣어주세요!)")]
    [SerializeField] private Transform exitWaypoint;
    [Tooltip("탈출 연출 중 플레이어 이동 속도")]
    [SerializeField] private float autoWalkSpeed = 2.5f;
    [Tooltip("WayPoint를 향해 몸을 회전하는 속도")]
    [SerializeField] private float rotationSpeed = 5.0f;

    [Header("Player References (조작 잠금 및 이동용)")]
    [SerializeField] private MonoBehaviour playerController;
    [SerializeField] private MonoBehaviour playerCombat;

    [Header("UI References")]
    [Tooltip("STAGE CLEAR 문구와 [다음] 버튼이 있는 1단계 배너 UI")]
    [SerializeField] private GameObject stageClearBannerUI;

    [Tooltip("2단계 최종 점수/랭크 결과 UI (GameResultUI)")]
    [SerializeField] private GameResultUI gameResultUI;

    [Header("Player Health Settings (점수 연산용)")]
    [SerializeField] private float playerCurrentHP = 100f;
    [SerializeField] private float playerMaxHP = 100f;

    private bool isCleared = false;

    private void Awake()
    {
        if (stageClearBannerUI != null)
            stageClearBannerUI.SetActive(false);

        if (exitCamera != null)
            exitCamera.gameObject.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isCleared || !other.CompareTag("Player")) return;

        isCleared = true;
        Debug.Log("🎉 [ExitZone] 탈출 연출 시작! 적 정지 및 플레이어 Waypoint 이동 시작!");

        // 1. 플레이어 기존 조작 비활성화
        if (playerController != null) playerController.enabled = false;
        if (playerCombat != null) playerCombat.enabled = false;

        // 2. 씬 내의 모든 적 AI 및 NavMeshAgent 일시정지!
        FreezeAllEnemies();

        // 3. 탈출 카메라 활성화
        if (exitCamera != null)
        {
            exitCamera.gameObject.SetActive(true);
        }

        // 4. 마우스 커서 해제
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // 5. 스플라인 컷씬 & Waypoint 자동 이동 시작!
        StartCoroutine(PlayExitCutsceneRoutine());
    }

    /// <summary>
    /// 🎯 씬에 존재하는 모든 적의 AI와 NavMeshAgent를 정지시키는 메서드
    /// </summary>
    private void FreezeAllEnemies()
    {
        EnemyAI[] enemies = FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
        foreach (var enemy in enemies)
        {
            enemy.enabled = false;

            NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
            if (agent != null && agent.isActiveAndEnabled)
            {
                agent.isStopped = true;
                agent.velocity = Vector3.zero;
            }
        }
        Debug.Log($"🛑 [ExitZone] 총 {enemies.Length}명의 적을 완전 정지시켰습니다.");
    }

    /// <summary>
    /// 🎯 스플라인 카메라 이동 + WayPoint를 바라보며 부드럽게 걸어가는 코루틴
    /// </summary>
    private IEnumerator PlayExitCutsceneRoutine()
    {
        CharacterController cc = playerController != null ? playerController.GetComponent<CharacterController>() : null;
        Transform playerTransform = playerController != null ? playerController.transform : null;

        if (exitCamera != null)
        {
            exitCamera.Priority = activePriority;
            CinemachineSplineDolly dolly = exitCamera.GetComponent<CinemachineSplineDolly>();

            float elapsedTime = 0f;
            if (dolly != null) dolly.CameraPosition = 0f;

            while (elapsedTime < cutsceneDuration)
            {
                elapsedTime += Time.deltaTime;

                // 1. 카메라 스플라인 위치 이동 (0 -> 1)
                if (dolly != null)
                {
                    dolly.CameraPosition = Mathf.Clamp01(elapsedTime / cutsceneDuration);
                }

                // 2. 🎯 Waypoint 지점을 향해 회전하고 걸어가기!
                if (exitWaypoint != null && playerTransform != null)
                {
                    // 방향 계산 (Y축 높이는 고정해서 눕지 않게 방지!)
                    Vector3 targetDir = exitWaypoint.position - playerTransform.position;
                    targetDir.y = 0f;

                    if (targetDir.sqrMagnitude > 0.01f)
                    {
                        // 부드럽게 목표 방향을 향해 회전!
                        Quaternion targetRotation = Quaternion.LookRotation(targetDir);
                        playerTransform.rotation = Quaternion.Slerp(playerTransform.rotation, targetRotation, Time.deltaTime * rotationSpeed);

                        // 목표 지점으로 이동!
                        Vector3 moveDelta = targetDir.normalized * autoWalkSpeed * Time.deltaTime;

                        if (cc != null && cc.enabled)
                        {
                            cc.Move(moveDelta);
                        }
                        else
                        {
                            playerTransform.position = Vector3.MoveTowards(playerTransform.position, exitWaypoint.position, autoWalkSpeed * Time.deltaTime);
                        }
                    }
                }

                yield return null;
            }

            if (dolly != null) dolly.CameraPosition = 1f;
        }

        // 컷씬 완료 후 STAGE CLEAR UI 팝업!
        if (stageClearBannerUI != null)
        {
            stageClearBannerUI.SetActive(true);
        }
    }

    /// <summary>
    /// UI의 [다음] 버튼 OnClick() 이벤트에 연결
    /// </summary>
    public void OnClickNextButton()
    {
        Debug.Log("➡️ [ExitZone] [다음] 버튼 클릭! 최종 점수 집계 화면으로 이동!");

        if (stageClearBannerUI != null)
        {
            stageClearBannerUI.SetActive(false);
        }

        ScoreResult result = default;
        if (ScoreManager.Instance != null)
        {
            result = ScoreManager.Instance.CalculateFinalScore(playerCurrentHP, playerMaxHP);
        }

        Time.timeScale = 0f;

        if (gameResultUI != null)
        {
            gameResultUI.ShowResultUI(result);
        }
    }
}