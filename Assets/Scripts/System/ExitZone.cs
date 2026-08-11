using UnityEngine;

/// <summary>
/// 🎯 [최종 탈출 트리거 모듈]
/// 플레이어가 영역 진입 시 최종 점수 산출, 게임 일시정지, 마우스 잠금 해제 및 결과 UI 출력!
/// </summary>
[RequireComponent(typeof(Collider))]
public class ExitZone : MonoBehaviour
{
    [Header("UI Reference")]
    [Tooltip("결과 화면 캔버스(GameResultUI) 스크립트를 연결해 주세요.")]
    [SerializeField] private GameResultUI resultUI;

    [Header("Player Health Settings (임시/연동용)")]
    [SerializeField] private float playerCurrentHP = 100f;
    [SerializeField] private float playerMaxHP = 100f;

    private bool isCleared = false;

    private void OnTriggerEnter(Collider other)
    {
        // 이미 클리어했거나, 플레이어가 아니면 무시
        if (isCleared || !other.CompareTag("Player")) return;

        isCleared = true;
        Debug.Log("🎉 [ExitZone] 플레이어 최종 탈출 성공!!");

        // 1. 점수 집계 연산
        ScoreResult result = default;
        if (ScoreManager.Instance != null)
        {
            result = ScoreManager.Instance.CalculateFinalScore(playerCurrentHP, playerMaxHP);
        }

        // 2. 시간 정지 및 마우스 커서 해제 (UI 클릭용!)
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // 3. UI 창 띄우기
        if (resultUI != null)
        {
            resultUI.ShowResultUI(result);
        }
        else
        {
            Debug.LogWarning("[ExitZone] ResultUI가 연결되어 있지 않습니다!");
        }
    }
}