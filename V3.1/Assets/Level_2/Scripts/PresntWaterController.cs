using UnityEngine;

public class PresentWaterController : MonoBehaviour
{
    public GameObject water;
    public GameObject dry;

    void Start()
    {
        UpdateState();
    }

    void Update()
    {
        UpdateState(); // 实时检测（方便你按E切换）
    }

    void UpdateState()
    {
        if (GameState.Instance.isWaterBlocked)
        {
            water.SetActive(false);
            dry.SetActive(true);
        }
        else
        {
            water.SetActive(true);
            dry.SetActive(false);
        }
    }
}