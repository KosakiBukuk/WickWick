using UnityEngine;

/// <summary>
/// 맵에서 E키로 상호작용할 수 있는 모든 오브젝트가 구현해야 하는 인터페이스
/// </summary>
public interface IInteractable
{
    // 상호작용한 주체(플레이어)를 넘겨받음
    void Interact(GameObject interactor);
}