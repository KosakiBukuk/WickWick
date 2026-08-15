using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 🎯 [인게임 HUD & 피격 효과 연출 매니저]
/// HP 텍스트("HP 100 / 100"), 무기/투척물 UI 아이콘 ON/OFF 스위칭, 피격 비네팅 연출을 전담합니다.
/// </summary>
public class InGameHUDManager : MonoBehaviour
{
    [Header("Script References")]
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private PlayerCombat playerCombat;

    [Header("🩸 HP UI Elements")]
    [Tooltip("체력을 숫자로 명확하게 보여줄 TMP 텍스트 (예: HP 100 / 100)")]
    [SerializeField] private TMP_Text hpText;

    [Tooltip("(선택사항) 기본 슬라이더가 있다면 넣고, 없으면 비워두어도 됩니다.")]
    [SerializeField] private Slider hpSlider;

    [Header("💥 Damage Screen FX")]
    [Tooltip("피격 시 화면 테두리에 뜰 비네팅 UI Image")]
    [SerializeField] private Image damageOverlayImage;
    [SerializeField] private float flashDuration = 0.45f;
    [Range(0f, 1f)][SerializeField] private float maxAlpha = 0.75f;

    [Header("🗡️ Weapon Icon UI (Individual GameObjects)")]
    [Tooltip("단검 아이콘 UI 오브젝트 (단검 소지/들고 있을 때 활성화)")]
    [SerializeField] private GameObject daggerIconUI;

    [Tooltip("던질 오브젝트 아이콘 UI 오브젝트 (투척물 소지/들고 있을 때 활성화)")]
    [SerializeField] private GameObject throwableIconUI;

    private Coroutine damageFlashCoroutine;

    private void Awake()
    {
        if (playerHealth == null) playerHealth = FindObjectOfType<PlayerHealth>();
        if (playerCombat == null) playerCombat = FindObjectOfType<PlayerCombat>();
    }

    private void OnEnable()
    {
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged += UpdateHPUI;
            playerHealth.OnTakeDamage += TriggerDamageFlash;
        }

        if (playerCombat != null)
        {
            playerCombat.OnWeaponChanged += UpdateWeaponIcon;
        }
    }

    private void OnDisable()
    {
        if (playerHealth != null)
        {
            playerHealth.OnHealthChanged -= UpdateHPUI;
            playerHealth.OnTakeDamage -= TriggerDamageFlash;
        }

        if (playerCombat != null)
        {
            playerCombat.OnWeaponChanged -= UpdateWeaponIcon;
        }
    }

    private void Start()
    {
        SetupVignetteImage();

        if (playerHealth != null) UpdateHPUI(playerHealth.CurrentHealth, playerHealth.MaxHealth);
        if (playerCombat != null) UpdateWeaponIcon(playerCombat.CurrentWeapon);
    }

    private void SetupVignetteImage()
    {
        if (damageOverlayImage == null) return;

        damageOverlayImage.raycastTarget = false;

        if (damageOverlayImage.sprite == null)
        {
            int texSize = 256;
            Texture2D tex = new Texture2D(texSize, texSize, TextureFormat.RGBA32, false);
            Vector2 center = new Vector2(texSize / 2f, texSize / 2f);
            float maxRadius = texSize / 2f;

            for (int y = 0; y < texSize; y++)
            {
                for (int x = 0; x < texSize; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    float normalizedDist = Mathf.Clamp01(dist / maxRadius);
                    float alpha = Mathf.SmoothStep(0f, 1f, Mathf.Pow(normalizedDist, 2.5f));

                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();

            Sprite vignetteSprite = Sprite.Create(tex, new Rect(0, 0, texSize, texSize), new Vector2(0.5f, 0.5f));
            damageOverlayImage.sprite = vignetteSprite;
        }

        Color initialColor = Color.red;
        initialColor.a = 0f;
        damageOverlayImage.color = initialColor;
    }

    private void UpdateHPUI(float currentHP, float maxHP)
    {
        if (hpText != null)
        {
            hpText.text = $"HP {Mathf.CeilToInt(currentHP)} / {Mathf.CeilToInt(maxHP)}";
        }

        if (hpSlider != null)
        {
            hpSlider.value = currentHP / maxHP;
        }
    }

    private void TriggerDamageFlash()
    {
        if (damageOverlayImage == null) return;

        if (damageFlashCoroutine != null)
        {
            StopCoroutine(damageFlashCoroutine);
        }
        damageFlashCoroutine = StartCoroutine(DamageFlashRoutine());
    }

    private IEnumerator DamageFlashRoutine()
    {
        Color c = Color.red;

        float t = 0f;
        while (t < 0.06f)
        {
            t += Time.deltaTime;
            c.a = Mathf.Lerp(0f, maxAlpha, t / 0.06f);
            damageOverlayImage.color = c;
            yield return null;
        }

        t = 0f;
        while (t < flashDuration)
        {
            t += Time.deltaTime;
            c.a = Mathf.Lerp(maxAlpha, 0f, t / flashDuration);
            damageOverlayImage.color = c;
            yield return null;
        }

        c.a = 0f;
        damageOverlayImage.color = c;
    }

    // 🎯 [핵심] 현재 무기 상태에 따라 각각의 UI 오브젝트를 활성화 / 비활성화!
    private void UpdateWeaponIcon(PlayerCombat.WeaponType weaponType)
    {
        if (weaponType == PlayerCombat.WeaponType.Dagger)
        {
            if (daggerIconUI != null) daggerIconUI.SetActive(true);
            if (throwableIconUI != null) throwableIconUI.SetActive(false);
        }
        else if (weaponType == PlayerCombat.WeaponType.Throwable)
        {
            if (daggerIconUI != null) daggerIconUI.SetActive(false);
            if (throwableIconUI != null) throwableIconUI.SetActive(true);
        }
    }
}