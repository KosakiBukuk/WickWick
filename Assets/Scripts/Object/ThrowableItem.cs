using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 인스펙터 세팅 없이도 자기 자신의 원본 프리팹을 자동으로 찾아내는 스마트 투척 스크립트
/// </summary>
public class ThrowableItem : MonoBehaviour, IInteractable
{
    [Header("Throwable Settings (비워두면 자동 감지!)")]
    [SerializeField] private GameObject throwablePrefab;

    public void Interact(GameObject interactor)
    {
        PlayerCombat combat = interactor.GetComponent<PlayerCombat>();

        if (combat != null)
        {
            if (combat.HasThrowable)
            {
                Debug.Log("⚠️ 이미 투척 오브젝트를 소지하고 있습니다!");
                return;
            }

            // 🎯 1. 인스펙터가 비어있다면, 현재 씬 물체의 '원본 프리팹 파일'을 자동 추적!
            GameObject prefabToPass = throwablePrefab;

            if (prefabToPass == null)
            {
#if UNITY_EDITOR
                // 에디터 상에서 씬 오즈젝트의 원본 프리팹 자산을 자동으로 찾아옴!
                prefabToPass = PrefabUtility.GetCorrespondingObjectFromSource(gameObject);
#endif
            }

            // 🎯 2. 그래도 프리팹을 못 찾으면 자기 자신을 비활성화(Hide)해서 안전하게 전달
            if (prefabToPass == null)
            {
                gameObject.SetActive(false);
                prefabToPass = gameObject;
            }
            else
            {
                // 원본 프리팹을 성공적으로 찾았다면 씬의 물체는 제거!
                Destroy(gameObject);
            }

            combat.PickupThrowable(prefabToPass);
        }
    }
}