using UnityEngine;

[CreateAssetMenu(menuName = "Gameplay/Exit Hotkey Settings", fileName = "ExitHotkeySettings")]
public class ExitHotkeySettings : ScriptableObject
{
    [Header("Return To Title")]
    [SerializeField] private bool enableReturnToSceneKey = true;
    [SerializeField] private KeyCode returnToSceneKey = KeyCode.Alpha0;
    [SerializeField] private string returnSceneName = "MainMenu";

    [Header("Application Quit")]
    [SerializeField] private bool enableQuitKey = true;
    [SerializeField] private KeyCode quitKey = KeyCode.Escape;
    [SerializeField] private bool useQuitKeyToReturnSceneInWebGL;

    public bool EnableReturnToSceneKey => enableReturnToSceneKey;
    public KeyCode ReturnToSceneKey => returnToSceneKey;
    public string ReturnSceneName => returnSceneName;
    public bool EnableQuitKey => enableQuitKey;
    public KeyCode QuitKey => quitKey;
    public bool UseQuitKeyToReturnSceneInWebGL => useQuitKeyToReturnSceneInWebGL;
}
