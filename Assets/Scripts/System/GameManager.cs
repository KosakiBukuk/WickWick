using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    /// <summary>
    /// Zone 0 튜토리얼에서 사망 시 호출하는 씬 재시작
    /// </summary>
    public void RestartTutorialScene()
    {
        Debug.Log("🔄 [GameManager] 튜토리얼 씬을 깨끗하게 재시작합니다!");

        // 1. 멈춰있던 시간과 마우스 커서 상태 정상화
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // 2. 현재 씬 다시 로드!
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}