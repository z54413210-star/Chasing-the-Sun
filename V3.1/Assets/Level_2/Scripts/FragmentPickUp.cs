using UnityEngine;

public class FragmentPickup : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("拿到碎片");

            GameState.Instance.hasFragment = true;

            Destroy(gameObject); // 消失
        }
    }
}