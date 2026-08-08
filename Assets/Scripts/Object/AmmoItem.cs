using UnityEngine;

public class AmmoItem : MonoBehaviour, IInteractable
{
    [SerializeField] private int ammoCount = 1; // 획득할 예비 탄창 수

    public void Interact(GameObject interactor)
    {
        PlayerCombat combat = interactor.GetComponent<PlayerCombat>();

        if (combat != null)
        {
            combat.AddMagazine(ammoCount);
            Destroy(gameObject); // 맵에서 제거
        }
    }
}