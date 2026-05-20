using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class PatrolMonster2D : MonoBehaviour
{
    [Header("Patrol")]
    [SerializeField] private Vector2 patrolDirection = Vector2.right;
    [SerializeField] private float patrolDistance = 4f;
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private bool flipSpriteOnTurn = true;

    [Header("References (Optional)")]
    [SerializeField] private Rigidbody2D body;

    private Vector3 _startPosition;
    private Vector2 _normalizedDirection = Vector2.right;
    private int _directionSign = 1;

    private void Awake()
    {
        CacheReferences();
        _startPosition = transform.position;
        _normalizedDirection = patrolDirection.sqrMagnitude > 0.0001f ? patrolDirection.normalized : Vector2.right;
    }

    private void FixedUpdate()
    {
        if (moveSpeed <= 0f || patrolDistance <= 0f)
        {
            return;
        }

        var delta = _normalizedDirection * (_directionSign * moveSpeed * Time.fixedDeltaTime);
        var nextPosition = transform.position + (Vector3)delta;

        if (body != null)
        {
            body.MovePosition(nextPosition);
        }
        else
        {
            transform.position = nextPosition;
        }

        var traveled = Vector2.Dot((Vector2)(transform.position - _startPosition), _normalizedDirection);
        if (_directionSign > 0 && traveled >= patrolDistance)
        {
            TurnAround();
        }
        else if (_directionSign < 0 && traveled <= 0f)
        {
            TurnAround();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryKill(other.GetComponentInParent<PlayerController2D>());
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryKill(collision.collider.GetComponentInParent<PlayerController2D>());
    }

    private void TryKill(PlayerController2D player)
    {
        if (player == null) return;

        var respawnManager = RespawnManager.Instance;
        if (respawnManager != null)
        {
            // Reuse the same death/respawn pipeline as HazardZone.
            respawnManager.KillPlayer(player);
        }
        else
        {
            // Fallback behavior copied from HazardZone.
            player.HandleDeathStarted();
            player.RespawnAt(player.InitialSpawnPosition);
            player.HandleDeathFinished();
        }
    }

    private void TurnAround()
    {
        _directionSign *= -1;

        if (!flipSpriteOnTurn) return;

        var scale = transform.localScale;
        scale.x = Mathf.Abs(scale.x) * _directionSign;
        transform.localScale = scale;
    }

    private void CacheReferences()
    {
        if (body == null)
        {
            body = GetComponent<Rigidbody2D>();
        }
    }

    private void OnValidate()
    {
        CacheReferences();
        if (patrolDirection.sqrMagnitude <= 0.0001f)
        {
            patrolDirection = Vector2.right;
        }
    }

    private void Reset()
    {
        CacheReferences();

        var col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.isTrigger = true;
        }

        if (body != null)
        {
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }
    }

    private void OnDrawGizmosSelected()
    {
        var dir = patrolDirection.sqrMagnitude > 0.0001f ? patrolDirection.normalized : Vector2.right;
        var start = Application.isPlaying ? _startPosition : transform.position;
        var end = start + (Vector3)(dir * Mathf.Max(0f, patrolDistance));

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(start, end);
        Gizmos.DrawSphere(start, 0.08f);
        Gizmos.DrawSphere(end, 0.08f);
    }
}