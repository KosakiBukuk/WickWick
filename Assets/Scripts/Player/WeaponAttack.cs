using System.Collections;
using UnityEngine;

/// <summary>
/// 1인칭 대각선 단검 베기 연출 모듈.
/// 칼을 오른쪽 위에서 왼쪽 아래로 큰 궤적을 그리며 휘두릅니다.
/// </summary>
public class WeaponAttack : MonoBehaviour
{
    [Header("⚔️ Slash Motion Settings")]
    [Tooltip("베기 시작 위치 (오른쪽 위 살짝 뒤로 당기는 준비 자세)")]
    [SerializeField] private Vector3 readyOffset = new Vector3(0.1f, 0.08f, -0.05f);

    [Tooltip("베기 도달 위치 (화면 왼쪽 아래로 시원하게 휘두름!)")]
    [SerializeField] private Vector3 slashOffset = new Vector3(-0.45f, -0.25f, 0.35f);

    [Tooltip("베기 회전 각도 (칼날이 대각선으로 크게 쓸어넘어감)")]
    [SerializeField] private Vector3 slashRotation = new Vector3(30f, -75f, 55f);

    [Header("⏱️ Speed Settings")]
    [Tooltip("휘두르는 속도 (숫자가 클수록 빠르게 벱니다)")]
    [SerializeField] private float slashSpeed = 22f;

    [Tooltip("복귀 속도 (제자리로 돌아오는 속도)")]
    [SerializeField] private float returnSpeed = 10f;

    private Vector3 startLocalPos;
    private Quaternion startLocalRot;
    private bool isAttacking = false;

    public bool IsAttacking => isAttacking;

    private void Awake()
    {
        // 🎯 시작할 때 손에 든 기본 위치와 회전값 저장!
        startLocalPos = transform.localPosition;
        startLocalRot = transform.localRotation;
    }

    /// <summary>
    /// 🎯 외부(PlayerCombat)에서 호출하는 공격 함수!
    /// </summary>
    public void SwingWeapon()
    {
        if (!isAttacking)
        {
            StartCoroutine(SlashRoutine());
        }
    }

    private IEnumerator SlashRoutine()
    {
        isAttacking = true;

        Vector3 startPos = startLocalPos;
        Vector3 readyPos = startLocalPos + readyOffset;
        Vector3 endSlashPos = startLocalPos + slashOffset;

        Quaternion startRot = startLocalRot;
        Quaternion endSlashRot = startLocalRot * Quaternion.Euler(slashRotation);

        // 1. 화면을 대각선으로 베는 모션
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * slashSpeed;

            // 위치: 오른쪽 위 준비 위치에서 왼쪽 아래로 시원하게 쓸어내림!
            transform.localPosition = Vector3.Lerp(readyPos, endSlashPos, t);
            transform.localRotation = Quaternion.Slerp(startRot, endSlashRot, t);
            yield return null;
        }

        // 2. 원래 대기 자세로 부드럽게 복귀
        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime * returnSpeed;
            transform.localPosition = Vector3.Lerp(endSlashPos, startPos, t);
            transform.localRotation = Quaternion.Slerp(endSlashRot, startRot, t);
            yield return null;
        }

        // 정확한 위치 복원
        transform.localPosition = startPos;
        transform.localRotation = startRot;
        isAttacking = false;
    }
}