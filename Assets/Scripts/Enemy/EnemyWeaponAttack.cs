using System.Collections;
using UnityEngine;

/// <summary>
/// 🎯 [적 칼 휘두르기 공격 모션 모듈]
/// 적이 플레이어를 공격할 때 손에 든 칼을 획-! 베어 가르고 제자리로 돌아옵니다.
/// </summary>
public class EnemyWeaponAttack : MonoBehaviour
{
    [Header("⚔️ Enemy Slash Motion Settings")]
    [Tooltip("휘두를 때 칼이 나아갈 위치 오프셋")]
    [SerializeField] private Vector3 slashOffset = new Vector3(-0.5f, -0.1f, 0.3f);

    [Tooltip("휘두를 때 칼 회전 각도")]
    [SerializeField] private Vector3 slashRotation = new Vector3(20f, -80f, 45f);

    [Header("⏱️ Speed Settings")]
    [Tooltip("적의 칼 휘두르기 속도")]
    [SerializeField] private float slashSpeed = 18f;

    [Tooltip("원위치 복귀 속도")]
    [SerializeField] private float returnSpeed = 10f;

    private Vector3 startLocalPos;
    private Quaternion startLocalRot;
    private bool isAttacking = false;

    public bool IsAttacking => isAttacking;

    private void Awake()
    {
        // 시작할 때 적 손에 들린 칼의 기본 위치/회전 저장!
        startLocalPos = transform.localPosition;
        startLocalRot = transform.localRotation;
    }

    /// <summary>
    /// 🎯 적 AI에서 호출하는 칼 휘두르기 함수!
    /// </summary>
    public void SwingKnife()
    {
        if (!isAttacking)
        {
            StartCoroutine(SlashRoutine());
        }
    }

    private IEnumerator SlashRoutine()
    {
        isAttacking = true;

        Vector3 targetPos = startLocalPos + slashOffset;
        Quaternion targetRot = startLocalRot * Quaternion.Euler(slashRotation);

        // 1. ⚡ 칼을 획-! 하고 왼쪽 아래로 베어 가르기!
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * slashSpeed;
            transform.localPosition = Vector3.Lerp(startLocalPos, targetPos, t);
            transform.localRotation = Quaternion.Slerp(startLocalRot, targetRot, t);
            yield return null;
        }

        // 2. 🌸 대기 자세로 복귀
        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * returnSpeed;
            transform.localPosition = Vector3.Lerp(targetPos, startLocalPos, t);
            transform.localRotation = Quaternion.Slerp(targetRot, startLocalRot, t);
            yield return null;
        }

        transform.localPosition = startLocalPos;
        transform.localRotation = startLocalRot;
        isAttacking = false;
    }
}