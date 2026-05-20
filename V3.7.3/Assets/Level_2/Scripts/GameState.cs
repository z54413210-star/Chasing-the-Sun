using UnityEngine;

public class GameState : MonoBehaviour
{   public bool hasFragment = false;
//判断fragment是否拾取
    public static GameState Instance;
    

    // 🌊 泉眼是否被堵住
    public bool isWaterBlocked = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 跨场景保留
        }
        else
        {
            Destroy(gameObject);
        }
    }
}