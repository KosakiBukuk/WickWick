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
    [Header("Death Shatter Prefab")]
    [Tooltip("적이 죽을 때 터질 파편 프리팹 (EnemyShatterPrefab)")]
    [SerializeField] private GameObject shatterPrefab;
    [Tooltip("사망 위치보다 앞으로 밀어낼 거리 (미터)")]
    [SerializeField] private float forwardOffset = 0.4f; // 🎯 요 숫자로 앞으로 튀어나갈 위치 조절!

    [Tooltip("사망 위치보다 위로 띄울 높이 (미터)")]
    [SerializeField] private float upwardOffset = 0.1f;  // 🎯 피격 높이 보정용
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
        // 🎯 1. 사망 위치에 큐브 파편 프리팹 짠! 하고 생성!
        if (shatterPrefab != null)
        {
            // 🎯 [핵심] 적의 바라보는 정면 방향(transform.forward)으로 forwardOffset 만큼 살짝 앞으로 보정!
            Vector3 spawnPosition = transform.position + (transform.forward * forwardOffset) + (Vector3.up * upwardOffset);

            Instantiate(shatterPrefab, spawnPosition, transform.rotation);
        }

        Destroy(gameObject);
    }
}