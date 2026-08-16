using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 🎯 [플레이어 체력 및 피격/구역별 사망 전담 모듈]
/// 체력 관리, 피격 사운드, 구역별(Zone 0 vs Zone 1+) 분기 사망 처리를 담당합니다.
/// </summary>
public class PlayerHealth : MonoBehaviour
{
    [Header("🩸 Health Settings")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth = 100f;

    [Header("🔊 Damage Audio Settings")]
    [Tooltip("피격 시 재생될 억-! 하는 효과음")]
    [SerializeField] private AudioClip damageSFX;
    [Range(0f, 1f)][SerializeField] private float damageSFXVolume = 1.0f;

    private AudioSource audioSource;
    private bool isDead = false;
    private PlayerController playerController;

    // 🎯 UI 및 연출 스크립트에서 구독할 이벤트들!
    public event Action<float, float> OnHealthChanged; // (currentHealth, maxHealth)
    public event Action OnTakeDamage;                   // 피격 순간 이벤트
    public event Action OnDie;                          // 정식 사망(Zone 1 이상) 순간 이벤트

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsDead => isDead;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        // 🎯 플레이어 컨트롤러 컴포넌트 캐싱 (Zone 감지용)
        playerController = GetComponent<PlayerController>();
    }

    private void Start()
    {
        currentHealth = maxHealth;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void TakeDamage(float damage)
    {
        if (isDead) return;

        currentHealth = Mathf.Max(0f, currentHealth - damage);

        // 1. 🔊 피격 효과음 1회 출력!
        if (audioSource != null && damageSFX != null)
        {
            audioSource.PlayOneShot(damageSFX, damageSFXVolume);
        }

        // 2. 📡 UI 매니저로 이벤트 전파!
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        OnTakeDamage?.Invoke();

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    private void Die()
    {
        isDead = true;

        // 🎯 [핵심] 현재 위치가 Zone 0 (튜토리얼 구역)인지 체크!
        if (playerController != null && playerController.CurrentZone == 0)
        {

            if (GameManager.Instance != null)
            {
                GameManager.Instance.RestartTutorialScene();
            }
            else
            {
                // GameManager가 씬에 없을 때를 대비한 안전 장치 (Fallback)
                Time.timeScale = 1f;
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }
        }
        else
        {
            // 🎯 Zone 1 이후부터는 정식 Game Over 플로우 실행!
            OnDie?.Invoke();

            // 플레이어 조작 비활성화
            if (playerController != null)
            {
                playerController.enabled = false;
            }
        }
    }

    /// <summary>
    /// 🎯 (선택) 씬 리로드가 아닌 인게임 수동 부활/체력 복구 시 사용하는 메서드
    /// </summary>
    public void ResetHealth()
    {
        isDead = false;
        currentHealth = maxHealth;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (playerController != null)
        {
            playerController.enabled = true;
        }
    }
}