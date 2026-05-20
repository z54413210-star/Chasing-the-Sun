using UnityEngine;

[DisallowMultipleComponent]
public class Level3DeathBoundary : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        TryKill(other.GetComponentInParent<Level3PlayerAvatar>());
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryKill(collision.collider.GetComponentInParent<Level3PlayerAvatar>());
    }

    private static void TryKill(Level3PlayerAvatar avatar)
    {
        if (avatar != null)
        {
            avatar.Kill();
        }
    }
}
