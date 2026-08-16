using UnityEngine;

/// <summary>
/// 인게임 스폰 및 튜토리얼 스킵 처리 핸들러
/// MainMenuController의 선택에 따라 플레이어 스폰 위치를 결정합니다.
/// </summary>
public class PlayerSpawnHandler : MonoBehaviour
{
    [Header("👤 Player Reference")]
    [Tooltip("씬 내의 플레이어 오브젝트 (비워둘 시 태그로 자동 탐색)")]
    [SerializeField] private GameObject playerObject;

    [Header("📍 Spawn Points")]
    [Tooltip("🎯 튜토리얼 스킵 시 플레이어가 스폰될 Empty Object (SkipTuto)")]
    [SerializeField] private Transform skipTutoSpawnPoint;

    private void Start()
    {
        // 1. 플레이어 오브젝트 캐싱
        if (playerObject == null)
        {
            playerObject = GameObject.FindGameObjectWithTag("Player");
        }

        if (playerObject == null)
        {
            Debug.LogError("⚠️ [PlayerSpawnHandler] Player 오브젝트를 찾을 수 없습니다!");
            return;
        }

        // 2. 튜토리얼 스킵 여부 확인 및 텔레포트 실행
        if (MainMenuController.IsTutorialSkipped && skipTutoSpawnPoint != null)
        {
            TeleportPlayerToSkipPoint();
        }
        else
        {
            Debug.Log("🎮 [PlayerSpawnHandler] 일반 튜토리얼 구역에서 시작합니다.");
        }
    }

    /// <summary>
    /// 플레이어를 SkipTuto 위치로 이동시키고 구역을 초기화하는 함수
    /// </summary>
    private void TeleportPlayerToSkipPoint()
    {
        // CharacterController가 켜져 있으면 transform 이동이 무시되므로 잠시 꺼둔다
        CharacterController cc = playerObject.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        // 위치 & 회전값 덮어쓰기
        playerObject.transform.position = skipTutoSpawnPoint.position;
        playerObject.transform.rotation = skipTutoSpawnPoint.rotation;

        if (cc != null) cc.enabled = true;

        // 구역 번호 1로 갱신
        PlayerController pc = playerObject.GetComponent<PlayerController>();
        if (pc != null)
        {
            pc.SetCurrentZone(1);
        }

        Debug.Log($"⚡ [PlayerSpawnHandler] 플레이어를 '{skipTutoSpawnPoint.name}' 위치로 텔레포트 완료!");
    }
}