using UnityEngine;

/// <summary>
/// 플레이어의 체력 관리, 피격 반응 및 사망 처리를 담당하는 모듈
/// </summary>
public class PlayerHealth : MonoBehaviour
{
    [Header("Player Health Settings")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth = 100f;

    private bool isDead = false;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsDead => isDead;

    private void Start()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(float damage)
    {
        if (isDead) return;

        currentHealth = Mathf.Max(0f, currentHealth - damage);
        Debug.Log($"🩸 [Player] 피격! 남은 체력: {currentHealth}/{maxHealth}");

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    private void Die()
    {
        isDead = true;
        Debug.Log("💀 [Player] 플레이어가 사망했습니다! (GAME OVER)");

        // 플레이어 이동 제어 비활성화
        PlayerController controller = GetComponent<PlayerController>();
        if (controller != null) controller.enabled = false;
    }
}