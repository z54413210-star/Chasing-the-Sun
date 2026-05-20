using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class MirrorGhostPairController : MonoBehaviour
{
    private static readonly bool EnableGhostRespawnDebug = true;
    private const string GhostDebugPrefix = "[L3-GhostDebug][Ghost]";

    [Header("Mirror")]
    [SerializeField] private float mirrorCenterX;

    [Header("Ghosts")]
    [SerializeField] private Transform leftGhost;
    [SerializeField] private Transform rightGhost;
    [SerializeField] private BoxCollider2D leftGhostCollider;
    [SerializeField] private BoxCollider2D rightGhostCollider;
    [SerializeField] private SpriteRenderer leftGhostRenderer;
    [SerializeField] private SpriteRenderer rightGhostRenderer;

    [Header("Sensors")]
    [SerializeField] private GhostPlatformSensor leftSensor;
    [SerializeField] private GhostPlatformSensor rightSensor;
    [SerializeField] private bool useSensorBoundsForMovement = true;

    [Header("Bounds")]
    [SerializeField] private float leftMinX = -4f;
    [SerializeField] private float leftMaxX = -1f;
    [SerializeField] private float rightMinX = 1f;
    [SerializeField] private float rightMaxX = 4f;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private bool useBlockingDetection = false;

    [Header("Contact Kill")]
    [SerializeField] private Vector2 visualKillBoundsInset = new Vector2(0.1f, 0.1f);

    private readonly Collider2D[] _ghostOverlapResults = new Collider2D[16];
    private readonly Dictionary<string, int> _lastKillCheckLoggedFrame = new Dictionary<string, int>();
    private Level3Side _lastLeadingSide = Level3Side.Left;
    private int _lastLoggedLeftTargetId = int.MinValue;
    private int _lastLoggedRightTargetId = int.MinValue;

    private void Awake()
    {
        CacheReferences();
    }

    private void OnValidate()
    {
        CacheReferences();
        RefreshMovementBoundsFromSensors();
    }

    private void Update()
    {
        CacheReferences();
        if (leftGhost == null || rightGhost == null || leftSensor == null || rightSensor == null)
        {
            return;
        }

        RefreshMovementBoundsFromSensors();

        var leftTarget = leftSensor.GetClosestAvatar(leftGhost.position);
        var rightTarget = rightSensor.GetClosestAvatar(rightGhost.position);
        LogTargetSelection(leftTarget, rightTarget);
        if (leftTarget == null && rightTarget == null)
        {
            CheckGhostContact();
            return;
        }

        var leadingSide = ResolveLeadingSide(leftTarget, rightTarget);
        float nextLeftX;
        float nextRightX;

        if (leadingSide == Level3Side.Left)
        {
            nextLeftX = Mathf.MoveTowards(leftGhost.position.x, leftTarget.FootPosition.x, moveSpeed * Time.deltaTime);
            nextLeftX = Mathf.Clamp(nextLeftX, leftMinX, leftMaxX);
            nextRightX = Mathf.Clamp(MirrorX(nextLeftX), rightMinX, rightMaxX);
            nextLeftX = MirrorX(nextRightX);
        }
        else
        {
            nextRightX = Mathf.MoveTowards(rightGhost.position.x, rightTarget.FootPosition.x, moveSpeed * Time.deltaTime);
            nextRightX = Mathf.Clamp(nextRightX, rightMinX, rightMaxX);
            nextLeftX = Mathf.Clamp(MirrorX(nextRightX), leftMinX, leftMaxX);
            nextRightX = MirrorX(nextLeftX);
        }

        var leftTargetPosition = new Vector2(nextLeftX, leftGhost.position.y);
        var rightTargetPosition = new Vector2(nextRightX, rightGhost.position.y);
        if ((!useBlockingDetection || !IsBlocked(leftGhostCollider, leftTargetPosition, rightGhostCollider)) &&
            (!useBlockingDetection || !IsBlocked(rightGhostCollider, rightTargetPosition, leftGhostCollider)))
        {
            leftGhost.position = new Vector3(leftTargetPosition.x, leftGhost.position.y, leftGhost.position.z);
            rightGhost.position = new Vector3(rightTargetPosition.x, rightGhost.position.y, rightGhost.position.z);
        }

        _lastLeadingSide = leadingSide;
        CheckGhostContact();
    }

    private Level3Side ResolveLeadingSide(Level3PlayerAvatar leftTarget, Level3PlayerAvatar rightTarget)
    {
        if (leftTarget != null && rightTarget == null)
        {
            return Level3Side.Left;
        }

        if (leftTarget == null && rightTarget != null)
        {
            return Level3Side.Right;
        }

        var leftDistance = Mathf.Abs(leftGhost.position.x - leftTarget.FootPosition.x);
        var rightDistance = Mathf.Abs(rightGhost.position.x - rightTarget.FootPosition.x);
        if (!Mathf.Approximately(leftDistance, rightDistance))
        {
            return leftDistance < rightDistance ? Level3Side.Left : Level3Side.Right;
        }

        return _lastLeadingSide;
    }

    private bool IsBlocked(BoxCollider2D ghostCollider, Vector2 targetPosition, BoxCollider2D otherGhostCollider)
    {
        if (ghostCollider == null)
        {
            return false;
        }

        var delta = targetPosition - (Vector2)ghostCollider.transform.position;
        var center = (Vector2)ghostCollider.bounds.center + delta;
        var size = ghostCollider.bounds.size * 0.95f;
        var hits = Physics2D.OverlapBoxAll(center, size, 0f);
        for (var i = 0; i < hits.Length; i++)
        {
            var hit = hits[i];
            if (hit == null || hit == ghostCollider || hit == otherGhostCollider || hit.isTrigger)
            {
                continue;
            }

            if (hit.GetComponentInParent<Level3PlayerAvatar>() != null)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private void CheckGhostContact()
    {
        TryKillContacts(leftGhostCollider, leftGhostRenderer, leftSensor, Level3Side.Left);
        TryKillContacts(rightGhostCollider, rightGhostRenderer, rightSensor, Level3Side.Right);
    }

    private void TryKillContacts(BoxCollider2D ghostCollider, SpriteRenderer ghostRenderer, GhostPlatformSensor sensor, Level3Side targetSide)
    {
        var killBounds = ResolveKillBounds(ghostCollider, ghostRenderer);
        if (!killBounds.HasValue)
        {
            if (ShouldLogDetailed(targetSide))
            {
                Level3GhostDebugFileLogger.Log($"{GhostDebugPrefix} TryKillContacts abort | side={targetSide} reason=no-kill-bounds frame={Time.frameCount} time={Time.realtimeSinceStartup:0.###}", this);
            }
            return;
        }

        var activeKillBounds = killBounds.Value;

        if (sensor != null)
        {
            foreach (var occupant in sensor.GetOccupants())
            {
                if (occupant == null || occupant.BodyCollider == null)
                {
                    continue;
                }

                var intersects = Intersects2D(activeKillBounds, occupant.BodyCollider.bounds);
                LogKillCheck("sensor-occupant", targetSide, activeKillBounds, occupant, intersects);
                if (intersects)
                {
                    LogKillDispatch("sensor-occupant", targetSide, occupant, activeKillBounds);
                    occupant.Kill();
                }
            }
        }

        if (ghostCollider == null)
        {
            return;
        }

        var hitCount = Physics2D.OverlapBoxNonAlloc(
            activeKillBounds.center,
            activeKillBounds.size,
            ghostCollider.transform.eulerAngles.z,
            _ghostOverlapResults);

        for (var i = 0; i < hitCount; i++)
        {
            var hit = _ghostOverlapResults[i];
            if (hit == null || hit == ghostCollider)
            {
                continue;
            }

            var avatar = hit.GetComponentInParent<Level3PlayerAvatar>();
            if (avatar == null || avatar.Side != targetSide || avatar.BodyCollider == null || !avatar.BodyCollider.enabled)
            {
                continue;
            }

            var intersects = Intersects2D(activeKillBounds, avatar.BodyCollider.bounds);
            LogKillCheck("overlap-hit", targetSide, activeKillBounds, avatar, intersects);
            if (intersects)
            {
                LogKillDispatch("overlap-hit", targetSide, avatar, activeKillBounds);
                avatar.Kill();
            }
        }
    }

    private void CacheReferences()
    {
        if (leftGhostCollider == null && leftGhost != null)
        {
            leftGhostCollider = leftGhost.GetComponent<BoxCollider2D>();
        }

        if (leftGhostRenderer == null && leftGhost != null)
        {
            leftGhostRenderer = leftGhost.GetComponent<SpriteRenderer>();
        }

        if (rightGhostCollider == null && rightGhost != null)
        {
            rightGhostCollider = rightGhost.GetComponent<BoxCollider2D>();
        }

        if (rightGhostRenderer == null && rightGhost != null)
        {
            rightGhostRenderer = rightGhost.GetComponent<SpriteRenderer>();
        }
    }

    private void RefreshMovementBoundsFromSensors()
    {
        if (!useSensorBoundsForMovement)
        {
            return;
        }

        var leftSensorCollider = leftSensor != null ? leftSensor.GetComponent<Collider2D>() : null;
        if (leftSensorCollider != null)
        {
            leftMinX = leftSensorCollider.bounds.min.x;
            leftMaxX = leftSensorCollider.bounds.max.x;
        }

        var rightSensorCollider = rightSensor != null ? rightSensor.GetComponent<Collider2D>() : null;
        if (rightSensorCollider != null)
        {
            rightMinX = rightSensorCollider.bounds.min.x;
            rightMaxX = rightSensorCollider.bounds.max.x;
        }
    }

    private float MirrorX(float xPosition)
    {
        return (mirrorCenterX * 2f) - xPosition;
    }

    private Bounds? ResolveKillBounds(BoxCollider2D ghostCollider, SpriteRenderer ghostRenderer)
    {
        if (ghostRenderer != null)
        {
            return InsetBounds(ghostRenderer.bounds, visualKillBoundsInset);
        }

        if (ghostCollider != null)
        {
            return ghostCollider.bounds;
        }

        return null;
    }

    private static Bounds InsetBounds(Bounds sourceBounds, Vector2 inset)
    {
        var size = sourceBounds.size;
        size.x = Mathf.Max(0.001f, size.x - (Mathf.Max(0f, inset.x) * 2f));
        size.y = Mathf.Max(0.001f, size.y - (Mathf.Max(0f, inset.y) * 2f));
        return new Bounds(sourceBounds.center, size);
    }

    private static bool Intersects2D(Bounds a, Bounds b)
    {
        return a.min.x <= b.max.x &&
               a.max.x >= b.min.x &&
               a.min.y <= b.max.y &&
               a.max.y >= b.min.y;
    }

    private void LogTargetSelection(Level3PlayerAvatar leftTarget, Level3PlayerAvatar rightTarget)
    {
        if (!EnableGhostRespawnDebug)
        {
            return;
        }

        var leftTargetId = leftTarget != null ? leftTarget.GetInstanceID() : 0;
        var rightTargetId = rightTarget != null ? rightTarget.GetInstanceID() : 0;
        if (leftTargetId == _lastLoggedLeftTargetId && rightTargetId == _lastLoggedRightTargetId)
        {
            return;
        }

        _lastLoggedLeftTargetId = leftTargetId;
        _lastLoggedRightTargetId = rightTargetId;
        var leftTargetText = leftTarget != null ? BuildAvatarState(leftTarget) : "null";
        var rightTargetText = rightTarget != null ? BuildAvatarState(rightTarget) : "null";
        Level3GhostDebugFileLogger.Log(
            $"{GhostDebugPrefix} TargetSelection frame={Time.frameCount} time={Time.realtimeSinceStartup:0.###} " +
            $"leftTarget={leftTargetText} rightTarget={rightTargetText}",
            this);
    }

    private void LogKillCheck(string source, Level3Side targetSide, Bounds killBounds, Level3PlayerAvatar avatar, bool intersects)
    {
        if (!ShouldLogDetailed(targetSide))
        {
            if (EnableGhostRespawnDebug && targetSide == Level3Side.Right && intersects)
            {
                Level3GhostDebugFileLogger.Log(
                    $"{GhostDebugPrefix} KillCheck source={source} side={targetSide} intersects={intersects} avatar={avatar.name}#{avatar.GetInstanceID()} frame={Time.frameCount}",
                    this);
            }
            return;
        }

        if (!ShouldLogKillCheck(source, targetSide, avatar, intersects))
        {
            return;
        }

        Level3GhostDebugFileLogger.Log(
            $"{GhostDebugPrefix} KillCheck source={source} side={targetSide} intersects={intersects} " +
            $"killBounds={FormatBounds(killBounds)} avatar={BuildAvatarState(avatar)}",
            this);
    }

    private void LogKillDispatch(string source, Level3Side targetSide, Level3PlayerAvatar avatar, Bounds killBounds)
    {
        if (!ShouldLogDetailed(targetSide))
        {
            if (EnableGhostRespawnDebug && targetSide == Level3Side.Right)
            {
                Level3GhostDebugFileLogger.Log($"{GhostDebugPrefix} KillDispatch source={source} side={targetSide} avatar={avatar.name}#{avatar.GetInstanceID()} frame={Time.frameCount}", this);
            }
            return;
        }

        Level3GhostDebugFileLogger.Log(
            $"{GhostDebugPrefix} KillDispatch source={source} side={targetSide} killBounds={FormatBounds(killBounds)} avatar={BuildAvatarState(avatar)}",
            this);
    }

    private static bool ShouldLogDetailed(Level3Side side)
    {
        return EnableGhostRespawnDebug && side == Level3Side.Left;
    }

    private bool ShouldLogKillCheck(string source, Level3Side targetSide, Level3PlayerAvatar avatar, bool intersects)
    {
        if (!EnableGhostRespawnDebug || avatar == null)
        {
            return false;
        }

        if (intersects)
        {
            return true;
        }

        if (targetSide != Level3Side.Left)
        {
            return false;
        }

        var key = $"{source}:{targetSide}:{avatar.GetInstanceID()}";
        if (_lastKillCheckLoggedFrame.TryGetValue(key, out var lastFrame) && Time.frameCount - lastFrame < 20)
        {
            return false;
        }

        _lastKillCheckLoggedFrame[key] = Time.frameCount;
        return true;
    }

    private static string BuildAvatarState(Level3PlayerAvatar avatar)
    {
        if (avatar == null)
        {
            return "null";
        }

        var body = avatar.Body;
        var collider2D = avatar.BodyCollider;
        var bodySimulated = body != null ? body.simulated.ToString() : "null";
        var colliderEnabled = collider2D != null ? collider2D.enabled.ToString() : "null";
        var boundsText = collider2D != null ? FormatBounds(collider2D.bounds) : "null";

        return
            $"side={avatar.Side} name={avatar.name} instanceId={avatar.GetInstanceID()} frame={Time.frameCount} " +
            $"time={Time.realtimeSinceStartup:0.###} position={FormatVector3(avatar.transform.position)} " +
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

    private void OnDrawGizmosSelected()
    {
        RefreshMovementBoundsFromSensors();
        Gizmos.color = new Color(0.8f, 0.2f, 1f, 0.8f);
        Gizmos.DrawLine(new Vector3(mirrorCenterX, -20f, 0f), new Vector3(mirrorCenterX, 20f, 0f));
        Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.45f);
        Gizmos.DrawLine(new Vector3(leftMinX, transform.position.y, 0f), new Vector3(leftMaxX, transform.position.y, 0f));
        Gizmos.DrawLine(new Vector3(rightMinX, transform.position.y, 0f), new Vector3(rightMaxX, transform.position.y, 0f));
    }
}



