using UnityEngine;
using TMPro; // TextMeshPro 사용 시

public class InteractionHUD : MonoBehaviour
{
    [Header("🎯 UI References")]
    [Tooltip("상호작용 안내 부모 패널 (없으면 이 오브젝트 자체)")]
    [SerializeField] private GameObject promptPanel;

    [Tooltip("안내 문구를 띄울 텍스트 (TMP)")]
    [SerializeField] private TextMeshProUGUI promptText;

    [Header("🎯 Player Reference")]
    [SerializeField] private PlayerCombat playerCombat;

    private void Awake()
    {
        if (promptPanel == null) promptPanel = gameObject;
        if (promptText == null) promptText = GetComponentInChildren<TextMeshProUGUI>();
    }

    private void Start()
    {
        if (playerCombat == null)
        {
            playerCombat = FindFirstObjectByType<PlayerCombat>();
        }

        if (playerCombat != null)
        {
            // 이벤트 구독
            playerCombat.OnInteractableHovered += UpdateInteractionUI;
        }

        // 시작할 때는 숨김
        HidePrompt();
    }

    private void OnDestroy()
    {
        if (playerCombat != null)
        {
            playerCombat.OnInteractableHovered -= UpdateInteractionUI;
        }
    }

    private void UpdateInteractionUI(bool isVisible, string message)
    {
        if (isVisible)
        {
            if (promptText != null) promptText.text = message;
            if (promptPanel != null) promptPanel.SetActive(true);
        }
        else
        {
            HidePrompt();
        }
    }

    private void HidePrompt()
    {
        if (promptPanel != null) promptPanel.SetActive(false);
    }
}