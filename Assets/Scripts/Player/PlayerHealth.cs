using System;
using UnityEngine;

/// <summary>
/// 🎯 [플레이어 체력 및 피격 전담 모듈]
/// 체력 관리, 사망 처리, 피격 사운드 출력 및 UI 연동 이벤트를 발생시킵니다.
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

    // 🎯 UI 및 연출 스크립트에서 구독할 이벤트들!
    public event Action<float, float> OnHealthChanged; // (currentHealth, maxHealth)
    public event Action OnTakeDamage;                   // 피격 순간 이벤트
    public event Action OnDie;                          // 사망 순간 이벤트

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
        Debug.Log($"🩸 [Player] 피격! 남은 체력: {currentHealth}/{maxHealth}");

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
        Debug.Log("💀 [Player] 플레이어가 사망했습니다! (GAME OVER)");

        OnDie?.Invoke();

        // 플레이어 이동 제어 비활성화
        PlayerController controller = GetComponent<PlayerController>();
        if (controller != null) controller.enabled = false;
    }
}