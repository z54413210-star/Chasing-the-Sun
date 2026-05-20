using UnityEngine;

public class WallController : MonoBehaviour
{
    void Update()
    {
        if (GameState.Instance.hasFragment)
        {
            gameObject.SetActive(false);
        }
    }
}