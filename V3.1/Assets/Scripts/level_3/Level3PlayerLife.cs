using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Level3PlayerAvatar))]
public class Level3PlayerLife : MonoBehaviour
{
    [SerializeField] private Level3PlayerAvatar avatar;
    [SerializeField] private Transform defaultSpawnPoint;
    [SerializeField] private FadeOverlay fadeOverlay;
    [SerializeField] private float fadeDuration = 0.18f;
    [SerializeField] private float blackScreenHoldDuration = 0.12f;

    private Level3Checkpoint _activeCheckpoint;
    private Vector3 _initialSpawnPosition;
    private bool _isRespawning;

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
        if (_isRespawning || !isActiveAndEnabled)
        {
            return;
        }

        StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        _isRespawning = true;
        avatar.HandleDeathStarted();
        ResolveFadeOverlayReference();

        if (fadeOverlay != null)
        {
            var fadeIn = fadeOverlay.FadeTo(1f, fadeDuration);
            if (fadeIn != null)
            {
                yield return fadeIn;
            }
        }

        if (blackScreenHoldDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(blackScreenHoldDuration);
        }

        avatar.RespawnAt(ResolveRespawnPosition());

        if (fadeOverlay != null)
        {
            var fadeOut = fadeOverlay.FadeTo(0f, fadeDuration);
            if (fadeOut != null)
            {
                yield return fadeOut;
            }
        }

        avatar.HandleDeathFinished();
        _isRespawning = false;
    }

    private Vector3 ResolveRespawnPosition()
    {
        if (_activeCheckpoint != null)
        {
            return _activeCheckpoint.GetSpawnPosition();
        }

        if (defaultSpawnPoint != null)
        {
            return defaultSpawnPoint.position;
        }

        return _initialSpawnPosition;
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
}
