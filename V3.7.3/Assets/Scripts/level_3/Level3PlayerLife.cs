using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Level3PlayerAvatar))]
public class Level3PlayerLife : MonoBehaviour
{
    private const float RespawnStaleSafetyBufferSeconds = 1f;
    private static readonly bool EnableGhostRespawnDebug = true;
    private const string GhostDebugPrefix = "[L3-GhostDebug][Life]";

    [SerializeField] private Level3PlayerAvatar avatar;
    [SerializeField] private Transform defaultSpawnPoint;
    [SerializeField] private FadeOverlay fadeOverlay;
    [SerializeField] private float fadeDuration = 0.18f;
    [SerializeField] private float blackScreenHoldDuration = 0.12f;

    private Level3Checkpoint _activeCheckpoint;
    private Vector3 _initialSpawnPosition;
    private bool _isRespawning;
    private float _respawnStartedRealtime;

    private void Awake()
    {
        CacheReferences();
        ResolveInitialSpawnPosition();
    }

    private void Start()
    {
        CacheReferences();
        ResolveFadeOverlayReference();
    }

    private void OnValidate()
    {
        CacheReferences();
    }

    public void SetCheckpoint(Level3Checkpoint checkpoint)
    {
        if (checkpoint == null || avatar == null)
        {
            return;
        }

        if (!checkpoint.AcceptsSide(avatar.Side))
        {
            return;
        }

        _activeCheckpoint = checkpoint;
    }

    public void Kill()
    {
        LogLifeEvent("Kill() requested");
        if (!isActiveAndEnabled)
        {
            LogLifeEvent("Kill() ignored: behaviour inactive");
            return;
        }

        RecoverFromStaleRespawnIfNeeded();
        if (_isRespawning)
        {
            LogLifeEvent("Kill() ignored: already respawning");
            return;
        }

        LogLifeEvent("Kill() accepted: starting respawn routine");
        StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        _isRespawning = true;
        _respawnStartedRealtime = Time.realtimeSinceStartup;
        LogLifeEvent("RespawnRoutine start");
        avatar.HandleDeathStarted();

        try
        {
            ResolveFadeOverlayReference();

            if (fadeOverlay != null)
            {
                var fadeIn = fadeOverlay.FadeTo(1f, fadeDuration);
                if (fadeIn != null)
                {
                    yield return fadeIn;
                }
            }

            LogLifeEvent("RespawnRoutine fade-in complete");

            if (blackScreenHoldDuration > 0f)
            {
                yield return new WaitForSecondsRealtime(blackScreenHoldDuration);
            }

            var respawnPosition = ResolveRespawnPosition();
            LogLifeEvent($"RespawnRoutine respawn target resolved={FormatVector3(respawnPosition)}");
            avatar.RespawnAt(respawnPosition);
            LogLifeEvent("RespawnRoutine avatar.RespawnAt complete");
            Physics2D.SyncTransforms();
            GhostPlatformSensor.RefreshAllSensors();
            LogLifeEvent("RespawnRoutine physics sync + sensor refresh complete");

            if (fadeOverlay != null)
            {
                var fadeOut = fadeOverlay.FadeTo(0f, fadeDuration);
                if (fadeOut != null)
                {
                    yield return fadeOut;
                }
            }

            LogLifeEvent("RespawnRoutine fade-out complete");
        }
        finally
        {
            LogLifeEvent("RespawnRoutine finally begin");
            avatar.HandleDeathFinished();
            _isRespawning = false;
            _respawnStartedRealtime = -1f;
            LogLifeEvent("RespawnRoutine finally end");
        }
    }

    private Vector3 ResolveRespawnPosition()
    {
        Vector3 resolvedPosition;
        if (_activeCheckpoint != null)
        {
            resolvedPosition = _activeCheckpoint.GetSpawnPosition();
            LogLifeEvent($"ResolveRespawnPosition using checkpoint={_activeCheckpoint.name} resolved={FormatVector3(resolvedPosition)}");
            return resolvedPosition;
        }

        if (defaultSpawnPoint != null)
        {
            resolvedPosition = defaultSpawnPoint.position;
            LogLifeEvent($"ResolveRespawnPosition using defaultSpawnPoint={defaultSpawnPoint.name} resolved={FormatVector3(resolvedPosition)}");
            return resolvedPosition;
        }

        resolvedPosition = _initialSpawnPosition;
        LogLifeEvent($"ResolveRespawnPosition using initialSpawnPosition resolved={FormatVector3(resolvedPosition)}");
        return resolvedPosition;
    }

    private void ResolveInitialSpawnPosition()
    {
        _initialSpawnPosition = defaultSpawnPoint != null ? defaultSpawnPoint.position : transform.position;
    }

    private void ResolveFadeOverlayReference()
    {
        if (fadeOverlay != null)
        {
            fadeOverlay.SetAlpha(0f);
            return;
        }

        var overlays = FindObjectsOfType<FadeOverlay>(true);
        for (var i = 0; i < overlays.Length; i++)
        {
            var candidate = overlays[i];
            if (candidate == null)
            {
                continue;
            }

            var lowerName = candidate.name.ToLowerInvariant();
            if ((avatar.Side == Level3Side.Left && lowerName.Contains("left")) ||
                (avatar.Side == Level3Side.Right && lowerName.Contains("right")))
            {
                fadeOverlay = candidate;
                fadeOverlay.SetAlpha(0f);
                return;
            }
        }
    }

    private void CacheReferences()
    {
        if (avatar == null)
        {
            avatar = GetComponent<Level3PlayerAvatar>();
        }
    }

    private void RecoverFromStaleRespawnIfNeeded()
    {
        if (!_isRespawning)
        {
            return;
        }

        var staleThreshold = Mathf.Max(0.5f, (fadeDuration * 2f) + blackScreenHoldDuration + RespawnStaleSafetyBufferSeconds);
        if (Time.realtimeSinceStartup - _respawnStartedRealtime < staleThreshold)
        {
            return;
        }

        LogLifeEvent($"RecoverFromStaleRespawnIfNeeded triggered staleThreshold={staleThreshold:0.###}");
        avatar.HandleDeathFinished();
        _isRespawning = false;
        _respawnStartedRealtime = -1f;
        LogLifeEvent("RecoverFromStaleRespawnIfNeeded finished");
    }

    private void LogLifeEvent(string message)
    {
        if (!EnableGhostRespawnDebug)
        {
            return;
        }

        var avatarState = BuildAvatarState();
        Level3GhostDebugFileLogger.Log($"{GhostDebugPrefix} {message} | {avatarState}", this);
    }

    private string BuildAvatarState()
    {
        var sideText = avatar != null ? avatar.Side.ToString() : "Unknown";
        var avatarName = avatar != null ? avatar.name : name;
        var instanceId = avatar != null ? avatar.GetInstanceID() : GetInstanceID();
        var activeText = avatar != null ? avatar.gameObject.activeInHierarchy.ToString() : gameObject.activeInHierarchy.ToString();
        var body = avatar != null ? avatar.Body : null;
        var collider2D = avatar != null ? avatar.BodyCollider : null;
        var position = avatar != null ? avatar.transform.position : transform.position;
        var bodySimulated = body != null ? body.simulated.ToString() : "null";
        var colliderEnabled = collider2D != null ? collider2D.enabled.ToString() : "null";
        var boundsText = collider2D != null ? FormatBounds(collider2D.bounds) : "null";

        return
            $"side={sideText} name={avatarName} instanceId={instanceId} frame={Time.frameCount} " +
            $"time={Time.realtimeSinceStartup:0.###} position={FormatVector3(position)} " +
            $"enabled={enabled} active={activeText} isRespawning={_isRespawning} " +
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
