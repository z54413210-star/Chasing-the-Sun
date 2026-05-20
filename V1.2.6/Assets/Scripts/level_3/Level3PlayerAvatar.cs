using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public class Level3PlayerAvatar : MonoBehaviour
{
    [SerializeField] private Level3Side side = Level3Side.Left;
    [SerializeField] private PlayerController2D playerOneController;
    [SerializeField] private PlayerTwoController2D playerTwoController;
    [SerializeField] private Rigidbody2D body;
    [SerializeField] private Collider2D bodyCollider;
    [SerializeField] private Level3PlayerLife life;

    public Level3Side Side => side;
    public Rigidbody2D Body => body;
    public Collider2D BodyCollider => bodyCollider;
    public Level3PlayerLife Life => life;

    public Vector3 FootPosition
    {
        get
        {
            CacheReferences();
            if (bodyCollider == null)
            {
                return transform.position;
            }

            var bounds = bodyCollider.bounds;
            return new Vector3(bounds.center.x, bounds.min.y, transform.position.z);
        }
    }

    private void Awake()
    {
        CacheReferences();
    }

    private void OnValidate()
    {
        CacheReferences();
    }

    public void Kill()
    {
        CacheReferences();
        if (life != null)
        {
            life.Kill();
        }
    }

    public void SetCheckpoint(Level3Checkpoint checkpoint)
    {
        CacheReferences();
        if (life != null)
        {
            life.SetCheckpoint(checkpoint);
        }
    }

    public void HandleDeathStarted()
    {
        if (playerOneController != null)
        {
            playerOneController.HandleDeathStarted();
        }
        else if (playerTwoController != null)
        {
            playerTwoController.HandleDeathStarted();
        }
    }

    public void HandleDeathFinished()
    {
        if (playerOneController != null)
        {
            playerOneController.HandleDeathFinished();
        }
        else if (playerTwoController != null)
        {
            playerTwoController.HandleDeathFinished();
        }
    }

    public void RespawnAt(Vector3 worldPosition)
    {
        if (playerOneController != null)
        {
            playerOneController.RespawnAt(worldPosition);
        }
        else if (playerTwoController != null)
        {
            playerTwoController.RespawnAt(worldPosition);
        }
        else
        {
            transform.position = worldPosition;
            if (body != null)
            {
                body.position = worldPosition;
                body.velocity = Vector2.zero;
                body.angularVelocity = 0f;
            }
        }
    }

    public void SetSide(Level3Side newSide)
    {
        side = newSide;
    }

    private void CacheReferences()
    {
        if (playerOneController == null)
        {
            playerOneController = GetComponent<PlayerController2D>();
        }

        if (playerTwoController == null)
        {
            playerTwoController = GetComponent<PlayerTwoController2D>();
        }

        if (body == null)
        {
            body = GetComponent<Rigidbody2D>();
        }

        if (bodyCollider == null)
        {
            bodyCollider = GetComponent<Collider2D>();
        }

        if (life == null)
        {
            life = GetComponent<Level3PlayerLife>();
        }
    }
}
