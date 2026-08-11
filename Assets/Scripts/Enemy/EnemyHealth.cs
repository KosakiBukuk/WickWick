using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// [4단계 수정본] 체력 관리, 피격 시 Alerted 발각 전환, 사망 처리 전담 (탄약 드랍 제거)
/// </summary>
public class EnemyHealth : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth = 100f;

    private EnemyAI enemyAI;
    private EnemyCombat enemyCombat;
    private FieldOfView fov;
    private NavMeshAgent agent;
    private bool isDead = false;

    public bool IsDead => isDead;
    public float CurrentHealth => currentHealth;

    private void Awake()
    {
        enemyAI = GetComponent<EnemyAI>();
        enemyCombat = GetComponent<EnemyCombat>();
        fov = GetComponent<FieldOfView>();
        agent = GetComponent<NavMeshAgent>();
    }

    private void Start()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(float damage)
    {
        if (isDead) return;

        currentHealth = Mathf.Max(0f, currentHealth - damage);
        Debug.Log($"🩸 [{gameObject.name}] 피격! 남은 체력: {currentHealth}/{maxHealth}");

        // 🎯 피격당하면 즉시 Alerted(발각) 상태로 전환!
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null && enemyAI != null && enemyAI.CurrentState != EnemyAI.State.Alerted)
        {
            enemyAI.SendMessage("SetState", EnemyAI.State.Alerted, SendMessageOptions.DontRequireReceiver);
        }

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    private void Die()
    {
        isDead = true;
        Debug.Log($"💀 [{gameObject.name}] 처치 완료!");

        // 모든 AI 및 센서, 컴포넌트 기능 완전 정지
        if (enemyAI != null) enemyAI.enabled = false;
        if (enemyCombat != null) enemyCombat.enabled = false;
        if (fov != null) fov.enabled = false;
        if (agent != null) agent.enabled = false;
        ScoreManager.Instance?.AddKill();

        Destroy(gameObject, 0.3f);
    }
}