using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class GhostPlatformSensor : MonoBehaviour
{
    private static readonly bool EnableGhostRespawnDebug = true;
    private const string GhostDebugPrefix = "[L3-GhostDebug][Sensor]";

    [SerializeField] private Level3Side acceptedSide = Level3Side.Left;

    private readonly HashSet<Level3PlayerAvatar> _occupants = new HashSet<Level3PlayerAvatar>();
    private readonly Collider2D[] _overlapResults = new Collider2D[16];
    private Collider2D _sensorCollider;
    private int _lastLoggedClosestAvatarId = int.MinValue;
    private int _lastLoggedOccupantCount = -1;

    private void Awake()
    {
        CacheReferences();
    }

    private void Reset()
    {
        var collider2D = GetComponent<Collider2D>();
        collider2D.isTrigger = true;
    }

    private void OnValidate()
    {
        CacheReferences();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryRegisterOccupant(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        var avatar = other.GetComponentInParent<Level3PlayerAvatar>();
        if (avatar != null)
        {
            if (ShouldLogDetailed(avatar))
            {
                Level3GhostDebugFileLogger.Log($"{GhostDebugPrefix} OnTriggerExit remove occupant | sensor={name} acceptedSide={acceptedSide} occupant={BuildAvatarState(avatar)} countBefore={_occupants.Count}", this);
            }
            _occupants.Remove(avatar);
        }
    }

    public void RefreshOccupants()
    {
        CacheReferences();
        if (ShouldLogDetailed())
        {
            Level3GhostDebugFileLogger.Log($"{GhostDebugPrefix} RefreshOccupants begin | sensor={name} acceptedSide={acceptedSide} countBefore={_occupants.Count} colliderEnabled={(_sensorCollider != null && _sensorCollider.enabled)} active={gameObject.activeInHierarchy} bounds={( _sensorCollider != null ? FormatBounds(_sensorCollider.bounds) : "null")}", this);
        }
        _occupants.Clear();

        if (_sensorCollider == null || !_sensorCollider.enabled || !gameObject.activeInHierarchy)
        {
            if (ShouldLogDetailed())
            {
                Level3GhostDebugFileLogger.Log($"{GhostDebugPrefix} RefreshOccupants abort | sensor={name} acceptedSide={acceptedSide} reason=invalid-sensor-state", this);
            }
            return;
        }

        var bounds = _sensorCollider.bounds;
        if (bounds.size.sqrMagnitude <= Mathf.Epsilon)
        {
            if (ShouldLogDetailed())
            {
                Level3GhostDebugFileLogger.Log($"{GhostDebugPrefix} RefreshOccupants abort | sensor={name} acceptedSide={acceptedSide} reason=zero-bounds", this);
            }
            return;
        }

        var hitCount = Physics2D.OverlapBoxNonAlloc(
            bounds.center,
            bounds.size,
            _sensorCollider.transform.eulerAngles.z,
            _overlapResults);

        for (var i = 0; i < hitCount; i++)
        {
            TryRegisterOccupant(_overlapResults[i]);
        }

        if (ShouldLogDetailed())
        {
            Level3GhostDebugFileLogger.Log($"{GhostDebugPrefix} RefreshOccupants end | sensor={name} acceptedSide={acceptedSide} countAfter={_occupants.Count} occupants={BuildOccupantList()}", this);
        }
    }

    public static void RefreshAllSensors()
    {
        var sensors = FindObjectsOfType<GhostPlatformSensor>(true);
        for (var i = 0; i < sensors.Length; i++)
        {
            if (sensors[i] != null)
            {
                sensors[i].RefreshOccupants();
            }
        }
    }

    public Level3PlayerAvatar GetClosestAvatar(Vector3 fromPosition)
    {
        Level3PlayerAvatar closest = null;
        var bestSqrDistance = float.MaxValue;

        foreach (var occupant in _occupants)
        {
            if (occupant == null || occupant.BodyCollider == null)
            {
                continue;
            }

            var sqrDistance = (occupant.Body.position - (Vector2)fromPosition).sqrMagnitude;
            if (sqrDistance < bestSqrDistance)
            {
                bestSqrDistance = sqrDistance;
                closest = occupant;
            }
        }

        LogClosestAvatarIfChanged(fromPosition, closest);

        return closest;
    }

    public IEnumerable<Level3PlayerAvatar> GetOccupants()
    {
        foreach (var occupant in _occupants)
        {
            if (occupant != null)
            {
                yield return occupant;
            }
        }
    }

    private void CacheReferences()
    {
        if (_sensorCollider == null)
        {
            _sensorCollider = GetComponent<Collider2D>();
        }
    }

    private void TryRegisterOccupant(Collider2D other)
    {
        if (other == null)
        {
            return;
        }

        var avatar = other.GetComponentInParent<Level3PlayerAvatar>();
        if (avatar == null)
        {
            return;
        }

        if (avatar.Side != acceptedSide)
        {
            if (ShouldLogDetailed(avatar))
            {
                Level3GhostDebugFileLogger.Log($"{GhostDebugPrefix} TryRegisterOccupant skipped | sensor={name} acceptedSide={acceptedSide} occupant={BuildAvatarState(avatar)} reason=side-mismatch", this);
            }
            return;
        }

        if (avatar.BodyCollider == null)
        {
            if (ShouldLogDetailed(avatar))
            {
                Level3GhostDebugFileLogger.Log($"{GhostDebugPrefix} TryRegisterOccupant skipped | sensor={name} acceptedSide={acceptedSide} occupant={BuildAvatarState(avatar)} reason=no-body-collider", this);
            }
            return;
        }

        if (!avatar.BodyCollider.enabled)
        {
            if (ShouldLogDetailed(avatar))
            {
                Level3GhostDebugFileLogger.Log($"{GhostDebugPrefix} TryRegisterOccupant skipped | sensor={name} acceptedSide={acceptedSide} occupant={BuildAvatarState(avatar)} reason=body-collider-disabled", this);
            }
            return;
        }

        var added = _occupants.Add(avatar);
        if (ShouldLogDetailed(avatar))
        {
            Level3GhostDebugFileLogger.Log($"{GhostDebugPrefix} TryRegisterOccupant {(added ? "added" : "already-present")} | sensor={name} acceptedSide={acceptedSide} occupant={BuildAvatarState(avatar)} count={_occupants.Count}", this);
        }
    }

    private bool ShouldLogDetailed()
    {
        return EnableGhostRespawnDebug && acceptedSide == Level3Side.Left;
    }

    private bool ShouldLogDetailed(Level3PlayerAvatar avatar)
    {
        return EnableGhostRespawnDebug && acceptedSide == Level3Side.Left && avatar != null && avatar.Side == Level3Side.Left;
    }

    private string BuildOccupantList()
    {
        var occupants = new List<string>();
        foreach (var occupant in _occupants)
        {
            if (occupant != null)
            {
                occupants.Add($"{occupant.name}#{occupant.GetInstanceID()}");
            }
        }

        return occupants.Count == 0 ? "[]" : $"[{string.Join(", ", occupants)}]";
    }

    private void LogClosestAvatarIfChanged(Vector3 fromPosition, Level3PlayerAvatar closest)
    {
        if (!ShouldLogDetailed())
        {
            return;
        }

        var closestId = closest != null ? closest.GetInstanceID() : 0;
        if (closestId == _lastLoggedClosestAvatarId && _occupants.Count == _lastLoggedOccupantCount)
        {
            return;
        }

        _lastLoggedClosestAvatarId = closestId;
        _lastLoggedOccupantCount = _occupants.Count;
        var closestText = closest != null ? BuildAvatarState(closest) : "null";
        Level3GhostDebugFileLogger.Log(
            $"{GhostDebugPrefix} GetClosestAvatar result | sensor={name} acceptedSide={acceptedSide} from={FormatVector3(fromPosition)} closest={closestText} count={_occupants.Count}",
            this);
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
}
