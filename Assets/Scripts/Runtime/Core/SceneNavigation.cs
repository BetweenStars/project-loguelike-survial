using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneNavigation : MonoBehaviour
{
    [SerializeField] private string gameScene = "GameScene";
    [SerializeField] private string titleScene = "TitleScene";
    public void StartGame()
    {
        Load(gameScene);
    }

    public void ReturnToTitle()
    {
        Load(titleScene);
    }

    public static void Load(string scene)
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (!Application.CanStreamedLevelBeLoaded(scene))
        {
            Debug.LogError($"Add scene '{scene}' to Build Profiles / Scene List.");
            return;
        }
        SceneManager.LoadScene(scene);
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}
