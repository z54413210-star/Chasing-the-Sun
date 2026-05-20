using System;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class LevelSelectMenuController : MonoBehaviour
{
    [Header("Level Scenes")]
    [SerializeField] private string level1SceneName = "Level_1";
    [SerializeField] private string level2SceneName = "Level_2";
    [SerializeField] private string level3SceneName = "Level3";

    private bool _isLoading;

    public void LoadLevel1()
    {
        TryLoadScene(level1SceneName);
    }

    public void LoadLevel2()
    {
        TryLoadScene(level2SceneName);
    }

    public void LoadLevel3()
    {
        TryLoadScene(level3SceneName);
    }

    private void TryLoadScene(string sceneName)
    {
        if (_isLoading)
        {
            return;
        }

        string safeSceneName = string.IsNullOrWhiteSpace(sceneName) ? "<empty>" : sceneName;

        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError($"{nameof(LevelSelectMenuController)} cannot load scene '{safeSceneName}' because the scene name is empty.", this);
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"{nameof(LevelSelectMenuController)} cannot load scene '{sceneName}'. Make sure it exists and is added to Build Settings.", this);
            return;
        }

        _isLoading = true;

        try
        {
            SceneManager.LoadScene(sceneName);
        }
        catch (Exception exception)
        {
            _isLoading = false;
            Debug.LogError($"{nameof(LevelSelectMenuController)} failed to load scene '{sceneName}'. {exception.Message}", this);
        }
    }
}
