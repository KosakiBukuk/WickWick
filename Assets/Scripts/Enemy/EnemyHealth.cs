using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 체력 관리, 피격 시 Alerted 발각 전환, 사망 처리를 담당.
/// 암살로 즉사할 경우 동료에게 경보를 울리지 않고 조용히 사망 처리합니다.
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
    [SerializeField] private float forwardOffset = 0.4f;

    [Tooltip("사망 위치보다 위로 띄울 높이 (미터)")]
    [SerializeField] private float upwardOffset = 0.1f;

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

        // 체력이 한 방에 0 이하로 떨어지면(암살) 경보 없이 즉시 사망
        if (currentHealth <= 0f)
        {
            Die();
        }
        else
        {
            // 한 방에 죽지 않고 살아남았을 때만 Alerted로 전환하고 동료에게 전파
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null && enemyAI != null && enemyAI.CurrentState != EnemyAI.State.Alerted)
            {
                enemyAI.SendMessage("SetState", EnemyAI.State.Alerted, SendMessageOptions.DontRequireReceiver);
            }
        }
    }

    private void Die()
    {
        isDead = true;

        // 모든 AI 및 센서, 컴포넌트 기능 완전 정지
        if (enemyAI != null) enemyAI.enabled = false;
        if (enemyCombat != null) enemyCombat.enabled = false;
        if (fov != null) fov.enabled = false;
        if (agent != null) agent.enabled = false;

        ScoreManager.Instance?.AddKill();

        // 1. 사망 위치에 파편 프리팹 생성
        if (shatterPrefab != null)
        {
            Vector3 spawnPosition = transform.position + (transform.forward * forwardOffset) + (Vector3.up * upwardOffset);
            Instantiate(shatterPrefab, spawnPosition, transform.rotation);
        }

        Destroy(gameObject);
    }
}