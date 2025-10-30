using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneResetKeyPuzzle : MonoBehaviour
{
    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        KeyFloating.ResetBossDrops();
    }
}
