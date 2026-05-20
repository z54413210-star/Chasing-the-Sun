using UnityEngine;

[DisallowMultipleComponent]
public class Level3Checkpoint : MonoBehaviour
{
    [SerializeField] private Level3Side side = Level3Side.Left;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Vector2 spawnOffset = new Vector2(0f, 1.1f);

    private void Reset()
    {
        var collider2D = GetComponent<Collider2D>();
        if (collider2D != null)
        {
            collider2D.isTrigger = true;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        var avatar = other.GetComponentInParent<Level3PlayerAvatar>();
        if (avatar == null || !AcceptsSide(avatar.Side))
        {
            return;
        }

        avatar.SetCheckpoint(this);
    }

    public bool AcceptsSide(Level3Side avatarSide)
    {
        return avatarSide == side;
    }

    public Vector3 GetSpawnPosition()
    {
        if (spawnPoint != null)
        {
            return spawnPoint.position;
        }

        return transform.position + (Vector3)spawnOffset;
    }
}
