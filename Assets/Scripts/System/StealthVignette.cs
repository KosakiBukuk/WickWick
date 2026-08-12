using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal; // URP Post-Processing 연동

/// <summary>
/// 🎯 [앉기 은신 화면 연출 모듈]
/// PlayerController의 IsCrouching 상태를 감지하여
/// Post-Processing Vignette(비네팅) 강도를 부드럽게 Lerp 연출합니다.
/// </summary>
public class StealthVignette : MonoBehaviour
{
    [Header("Script References")]
    [Tooltip("플레이어의 PlayerController (비워두면 자동 탐색)")]
    [SerializeField] private PlayerController playerController;

    [Header("Volume Reference")]
    [Tooltip("씬의 Global Volume 오브젝트를 드래그해 넣으세요.")]
    [SerializeField] private Volume globalVolume;

    [Header("Vignette Stealth Settings")]
    [Tooltip("평소(서있을 때) 비네팅 강도")]
    [SerializeField] private float normalIntensity = 0.2f;

    [Tooltip("앉았을 때(은신 중) 비네팅 강도 (숫자가 커질수록 화면 테두리가 어두워집니다)")]
    [SerializeField] private float crouchIntensity = 0.55f;

    [Tooltip("화면 음영이 스스륵 조여드는 전환 속도")]
    [SerializeField] private float transitionSpeed = 6.0f;

    private Vignette vignette;

    private void Start()
    {
        // 1. PlayerController 자동 캐싱
        if (playerController == null)
            playerController = FindFirstObjectByType<PlayerController>();

        // 2. Global Volume 자동 캐싱 및 Vignette 오버라이드 가져오기
        if (globalVolume == null)
            globalVolume = FindFirstObjectByType<Volume>();

        if (globalVolume != null && globalVolume.profile != null)
        {
            if (!globalVolume.profile.TryGet(out vignette))
            {
                Debug.LogWarning("⚠️ Global Volume Profile에 Vignette 효과가 추가되어 있지 않습니다!");
            }
        }
    }

    private void Update()
    {
        if (vignette == null || playerController == null) return;

        // 🎯 플레이어가 앉아있는지(IsCrouching)에 따라 목표 어두움 강도 결정!
        float targetIntensity = playerController.IsCrouching ? crouchIntensity : normalIntensity;

        // 🎯 부드럽게 Lerp 전환하여 은신 몰입감 연출!
        vignette.intensity.value = Mathf.Lerp(vignette.intensity.value, targetIntensity, Time.deltaTime * transitionSpeed);
    }
}