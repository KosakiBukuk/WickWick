using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 🎯 [탈출 시네머신, Waypoint 자동 이동, 적 정지 & 결과창 호출 전담 모듈]
/// </summary>
[RequireComponent(typeof(Collider))]
public class ExitZone : MonoBehaviour
{
    [Header("🎬 Cinemachine Camera Settings")]
    [Tooltip("탈출 연출을 담당할 CinemachineCamera 오브젝트")]
    [SerializeField] private CinemachineCamera exitCamera;
    [Tooltip("컷씬 연출 시 적용할 카메라 Priority 수치")]
    [SerializeField] private int activePriority = 20;
    [Tooltip("카메라가 레일을 따라 이동하는 시간 (초)")]
    [SerializeField] private float cutsceneDuration = 2.5f;

    [Header("🚶 Auto Walk Waypoint Settings")]
    [Tooltip("🎯 플레이어가 걸어갈 탈출 목표 위치 (빈 게임오브젝트)")]
    [SerializeField] private Transform exitWaypoint;
    [Tooltip("탈출 연출 중 플레이어 이동 속도")]
    [SerializeField] private float autoWalkSpeed = 2.5f;
    [Tooltip("WayPoint를 향해 몸을 회전하는 속도")]
    [SerializeField] private float rotationSpeed = 5.0f;

    [Header("👤 Player References")]
    [Tooltip("플레이어 조작 스크립트 (비워둘 시 자동 감지)")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private PlayerCombat playerCombat;
    [Tooltip("🎯 탈출 연출 시 숨길 플레이어 손/무기 VisualMesh")]
    [SerializeField] private GameObject playerWeaponMesh;

    [Header("🖥️ UI References")]
    [Tooltip("🎯 HP 텍스트, 무기 아이콘 등이 포함된 메인 인게임 HUD Canvas")]
    [SerializeField] private GameObject inGameHUDCanvas;

    [Header("⚙️ Clear Options")]
    [Tooltip("체크 시 탈출 연출 진입 즉시 씬의 모든 적을 비활성화/숨김 처리합니다.")]
    [SerializeField] private bool hideEnemiesOnClear = true;

    private bool isCleared = false;

    private void Awake()
    {
        Collider col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;

        if (exitCamera != null) exitCamera.gameObject.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isCleared || !other.CompareTag("Player")) return;

        isCleared = true;
        Debug.Log("🎉 [ExitZone] 탈출 구역 진입! 컷씬 연출 및 인게임 HUD 정리를 시작합니다.");

        // 1. 플레이어 컴포넌트 자동 캐싱
        if (playerController == null) playerController = other.GetComponent<PlayerController>();
        if (playerCombat == null) playerCombat = other.GetComponent<PlayerCombat>();

        // 2. 인게임 HUD 및 플레이어 무기 숨기기
        CleanUpInGameVisuals();

        // 3. 플레이어 조작 비활성화
        if (playerController != null) playerController.enabled = false;
        if (playerCombat != null) playerCombat.enabled = false;

        // 4. 씬 내 모든 적 AI 정지
        FreezeAllEnemies();

        // 5. 탈출 카메라 활성화 및 마우스 커서 해제
        if (exitCamera != null) exitCamera.gameObject.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // 6. 스플라인 컷씬 & Waypoint 자동 이동 시작!
        StartCoroutine(PlayExitCutsceneRoutine());
    }

    private void CleanUpInGameVisuals()
    {
        if (inGameHUDCanvas != null)
        {
            inGameHUDCanvas.SetActive(false);
            Debug.Log("🧹 [ExitZone] InGameHUDCanvas 비활성화 완료!");
        }

        if (playerWeaponMesh != null)
        {
            playerWeaponMesh.SetActive(false);
            Debug.Log("🧹 [ExitZone] PlayerWeaponMesh 숨김 처리 완료!");
        }
    }

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

            if (hideEnemiesOnClear)
            {
                enemy.gameObject.SetActive(false);
            }
        }
        Debug.Log($"🛑 [ExitZone] 적 {enemies.Length}명 정지 완료!");
    }

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

                if (dolly != null)
                {
                    dolly.CameraPosition = Mathf.Clamp01(elapsedTime / cutsceneDuration);
                }

                if (exitWaypoint != null && playerTransform != null)
                {
                    Vector3 targetDir = exitWaypoint.position - playerTransform.position;
                    targetDir.y = 0f;

                    if (targetDir.sqrMagnitude > 0.01f)
                    {
                        Quaternion targetRotation = Quaternion.LookRotation(targetDir);
                        playerTransform.rotation = Quaternion.Slerp(playerTransform.rotation, targetRotation, Time.deltaTime * rotationSpeed);

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

        // 🌟 컷씬 종료 직후 0.3초 대기 후 곧바로 최종 점수 결과 화면 오픈!
        yield return new WaitForSeconds(0.3f);
        OpenFinalResultScreen();
    }

    /// <summary>
    /// 🎯 ResultScreenUI 싱글톤을 통한 결과창 오픈
    /// </summary>
    private void OpenFinalResultScreen()
    {
        if (ResultScreenUI.Instance != null)
        {
            Debug.Log("🏆 [ExitZone] ResultScreenUI.Instance.OpenResultScreen() 호출!");
            ResultScreenUI.Instance.OpenResultScreen();
        }
        else
        {
            Debug.LogError("⚠️ [ExitZone] 씬에서 ResultScreenUI 인스턴스를 찾을 수 없습니다!");
        }
    }
}