using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class EscapeQuitHandler : MonoBehaviour
{
    private static EscapeQuitHandler _instance;
    private const string SettingsResourcePath = "Gameplay/ExitHotkeySettings";

    [Header("Return To Title")]
    [SerializeField] private bool enableReturnToSceneKey = true;
    [SerializeField] private KeyCode returnToSceneKey = KeyCode.Alpha0;
    [SerializeField] private string returnSceneName = "MainMenu";

    [Header("Application Quit")]
    [SerializeField] private bool enableQuitKey = true;
    [SerializeField] private KeyCode quitKey = KeyCode.Escape;
    [SerializeField] private bool useQuitKeyToReturnSceneInWebGL;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureInstance()
    {
        if (_instance != null)
        {
            return;
        }

        var root = new GameObject("EscapeQuitHandler");
        _instance = root.AddComponent<EscapeQuitHandler>();
        DontDestroyOnLoad(root);
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        ApplySettings(Resources.Load<ExitHotkeySettings>(SettingsResourcePath));
    }

    private void Update()
    {
        if (enableReturnToSceneKey && returnToSceneKey != KeyCode.None && Input.GetKeyDown(returnToSceneKey))
        {
            LoadReturnScene();
            return;
        }

        if (enableQuitKey && quitKey != KeyCode.None && Input.GetKeyDown(quitKey))
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (useQuitKeyToReturnSceneInWebGL)
            {
                LoadReturnScene();
                return;
            }
#endif
            QuitGame();
        }
    }

    private void ApplySettings(ExitHotkeySettings settings)
    {
        if (settings == null)
        {
            return;
        }

        enableReturnToSceneKey = settings.EnableReturnToSceneKey;
        returnToSceneKey = settings.ReturnToSceneKey;
        returnSceneName = settings.ReturnSceneName;
        enableQuitKey = settings.EnableQuitKey;
        quitKey = settings.QuitKey;
        useQuitKeyToReturnSceneInWebGL = settings.UseQuitKeyToReturnSceneInWebGL;
    }

    private void LoadReturnScene()
    {
        if (string.IsNullOrWhiteSpace(returnSceneName))
        {
            Debug.LogWarning(nameof(EscapeQuitHandler) + " cannot return to a scene because returnSceneName is empty.", this);
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(returnSceneName))
        {
            Debug.LogWarning(
                nameof(EscapeQuitHandler) + " cannot return to scene '" + returnSceneName +
                "'. Make sure it exists and is added to Build Settings.",
                this);
            return;
        }

        var activeScene = SceneManager.GetActiveScene();
        if (activeScene.IsValid() && activeScene.name == returnSceneName)
        {
            return;
        }

        SceneManager.LoadScene(returnSceneName);
    }

    private static void QuitGame()
    {
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
