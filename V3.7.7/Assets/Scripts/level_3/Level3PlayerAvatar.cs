using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public class Level3PlayerAvatar : MonoBehaviour
{
    private static readonly bool EnableGhostRespawnDebug = true;
    private const string GhostDebugPrefix = "[L3-GhostDebug][Avatar]";

    [SerializeField] private Level3Side side = Level3Side.Left;
    [SerializeField] private PlayerOneLevel3Controller2D level3PlayerOneController;
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
        LogAvatarEvent("Kill() forward to life");
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
        CacheReferences();
        LogAvatarEvent("HandleDeathStarted begin");
        if (side == Level3Side.Right && playerTwoController != null && playerTwoController.isActiveAndEnabled)
        {
            playerTwoController.HandleDeathStarted();
        }
        else if (level3PlayerOneController != null && level3PlayerOneController.isActiveAndEnabled)
        {
            level3PlayerOneController.HandleDeathStarted();
        }
        else if (playerOneController != null && playerOneController.isActiveAndEnabled)
        {
            playerOneController.HandleDeathStarted();
        }
        else if (playerTwoController != null && playerTwoController.isActiveAndEnabled)
        {
            playerTwoController.HandleDeathStarted();
        }
        LogAvatarEvent("HandleDeathStarted end");
    }

    public void HandleDeathFinished()
    {
        CacheReferences();
        LogAvatarEvent("HandleDeathFinished begin");
        if (side == Level3Side.Right && playerTwoController != null && playerTwoController.isActiveAndEnabled)
        {
            playerTwoController.HandleDeathFinished();
        }
        else if (level3PlayerOneController != null && level3PlayerOneController.isActiveAndEnabled)
        {
            level3PlayerOneController.HandleDeathFinished();
        }
        else if (playerOneController != null && playerOneController.isActiveAndEnabled)
        {
            playerOneController.HandleDeathFinished();
        }
        else if (playerTwoController != null && playerTwoController.isActiveAndEnabled)
        {
            playerTwoController.HandleDeathFinished();
        }

        RestorePhysicsParticipation();
        LogAvatarEvent("HandleDeathFinished end");
    }

    public void RespawnAt(Vector3 worldPosition)
    {
        CacheReferences();
        LogAvatarEvent($"RespawnAt begin target={FormatVector3(worldPosition)}");
        if (side == Level3Side.Right && playerTwoController != null && playerTwoController.isActiveAndEnabled)
        {
            playerTwoController.RespawnAt(worldPosition);
        }
        else if (level3PlayerOneController != null && level3PlayerOneController.isActiveAndEnabled)
        {
            level3PlayerOneController.RespawnAt(worldPosition);
        }
        else if (playerOneController != null && playerOneController.isActiveAndEnabled)
        {
            playerOneController.RespawnAt(worldPosition);
        }
        else if (playerTwoController != null && playerTwoController.isActiveAndEnabled)
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

        RestorePhysicsParticipation();

        // Apply one final hard snap so respawn always resolves at the requested checkpoint
        // even if an earlier controller path left stale transient movement state behind.
        transform.position = worldPosition;
        if (body != null)
        {
            body.position = worldPosition;
            body.velocity = Vector2.zero;
            body.angularVelocity = 0f;
        }

        LogAvatarEvent("RespawnAt end");
    }

    public void SetSide(Level3Side newSide)
    {
        side = newSide;
    }

    private void CacheReferences()
    {
        if (level3PlayerOneController == null)
        {
            level3PlayerOneController = GetComponent<PlayerOneLevel3Controller2D>();
        }

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

    private void RestorePhysicsParticipation()
    {
        CacheReferences();

        if (bodyCollider != null)
        {
            bodyCollider.enabled = true;
        }

        if (body != null)
        {
            body.simulated = true;
            body.WakeUp();
        }
    }

    private void LogAvatarEvent(string message)
    {
        if (!EnableGhostRespawnDebug)
        {
            return;
        }

        Level3GhostDebugFileLogger.Log($"{GhostDebugPrefix} {message} | {BuildAvatarState()}", this);
    }

    private string BuildAvatarState()
    {
        var bodySimulated = body != null ? body.simulated.ToString() : "null";
        var colliderEnabled = bodyCollider != null ? bodyCollider.enabled.ToString() : "null";
        var boundsText = bodyCollider != null ? FormatBounds(bodyCollider.bounds) : "null";
        var position = transform.position;

        return
            $"side={side} name={name} instanceId={GetInstanceID()} frame={Time.frameCount} " +
            $"time={Time.realtimeSinceStartup:0.###} position={FormatVector3(position)} " +
            $"bodySimulated={bodySimulated} colliderEnabled={colliderEnabled} bounds={boundsText}";
    }

    private static string FormatBounds(Bounds bounds)
    {
        return $"center={FormatVector3(bounds.center)} min={FormatVector3(bounds.min)} max={FormatVector3(bounds.max)}";
    }

    private static string FormatVector3(Vector3 value)
    {
        return $"({value.x:0.###},{value.y:0.###},{value.z:0.###})";
    }
}
