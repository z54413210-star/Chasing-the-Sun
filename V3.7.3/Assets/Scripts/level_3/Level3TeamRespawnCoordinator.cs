using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class Level3TeamRespawnCoordinator : MonoBehaviour
{
    [SerializeField] private Level3PlayerAvatar leftAvatar;
    [SerializeField] private Level3PlayerAvatar rightAvatar;
    [SerializeField] private Transform leftFallbackSpawn;
    [SerializeField] private Transform rightFallbackSpawn;
    [SerializeField] private bool restoreRespawnableDynamics = true;
    [SerializeField] private FadeOverlay leftOverlay;
    [SerializeField] private FadeOverlay rightOverlay;
    [SerializeField] private bool autoCreateMissingLeftOverlay = true;

    public Level3PlayerAvatar LeftAvatar => leftAvatar;
    public Level3PlayerAvatar RightAvatar => rightAvatar;

    private static readonly BindingFlags PrivateInstanceFlags = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly FieldInfo IsRespawningField = typeof(Level3PlayerLife).GetField("_isRespawning", PrivateInstanceFlags);
    private static readonly FieldInfo FadeDurationField = typeof(Level3PlayerLife).GetField("fadeDuration", PrivateInstanceFlags);
    private static readonly FieldInfo BlackScreenHoldDurationField = typeof(Level3PlayerLife).GetField("blackScreenHoldDuration", PrivateInstanceFlags);
    private const float DefaultFadeDurationSeconds = 0.18f;
    private const float DefaultBlackScreenHoldSeconds = 0.12f;

    private RespawnableDynamic[] _respawnableDynamics = System.Array.Empty<RespawnableDynamic>();
    private ILevel3TeamRespawnResettable[] _teamResettables = System.Array.Empty<ILevel3TeamRespawnResettable>();

    private Level3Checkpoint _activeLeftCheckpoint;
    private Level3Checkpoint _activeRightCheckpoint;
    private Level3PlayerLife _leftLife;
    private Level3PlayerLife _rightLife;
    private Vector3 _initialLeftSpawnPosition;
    private Vector3 _initialRightSpawnPosition;
    private bool _hasInitialLeftSpawnPosition;
    private bool _hasInitialRightSpawnPosition;
    private bool _leftWasRespawning;
    private bool _rightWasRespawning;
    private bool _teamRespawnInProgress;
    private Coroutine _teamRespawnRoutine;

    private void Awake()
    {
        CacheAvatarReferences();
        CaptureInitialSpawnPositions();
        RefreshSceneCollections();
        EnsureOverlaysReady();
    }

    private void Start()
    {
        EnsureOverlaysReady();
    }

    private void OnValidate()
    {
        CacheAvatarReferences();
    }

    public void ActivateCheckpointPair(Level3Checkpoint leftCheckpoint, Level3Checkpoint rightCheckpoint)
    {
        _activeLeftCheckpoint = leftCheckpoint;
        _activeRightCheckpoint = rightCheckpoint;
    }

    private void Update()
    {
        CacheAvatarReferences();
        CaptureInitialSpawnPositions();
        EnsureOverlaysReady();

        var leftIsRespawning = IsRespawning(_leftLife);
        var rightIsRespawning = IsRespawning(_rightLife);

        if (!_teamRespawnInProgress)
        {
            if (leftIsRespawning && !_leftWasRespawning)
            {
                _teamRespawnRoutine = StartCoroutine(TeamRespawnRoutine(Level3Side.Left));
            }
            else if (rightIsRespawning && !_rightWasRespawning)
            {
                _teamRespawnRoutine = StartCoroutine(TeamRespawnRoutine(Level3Side.Right));
            }
        }

        _leftWasRespawning = leftIsRespawning;
        _rightWasRespawning = rightIsRespawning;
    }

    private IEnumerator TeamRespawnRoutine(Level3Side primarySide)
    {
        _teamRespawnInProgress = true;
        CacheAvatarReferences();
        RefreshSceneCollections();
        EnsureOverlaysReady();

        var secondarySide = GetOppositeSide(primarySide);
        var secondaryAvatar = GetAvatar(secondarySide);
        var primaryLife = primarySide == Level3Side.Left ? _leftLife : _rightLife;
        var secondaryLife = secondarySide == Level3Side.Left ? _leftLife : _rightLife;
        var secondaryOverlay = GetOverlay(secondarySide);
        var secondaryWasAlreadyRespawning = IsRespawning(secondaryLife);
        var fadeDuration = ResolveFloatField(primaryLife, FadeDurationField, DefaultFadeDurationSeconds);
        var blackScreenHoldDuration = ResolveFloatField(primaryLife, BlackScreenHoldDurationField, DefaultBlackScreenHoldSeconds);
        Rigidbody2D secondaryBody;
        Collider2D secondaryBodyCollider;
        bool secondaryBodyWasSimulated;
        bool secondaryColliderWasEnabled;
        bool secondaryPhysicsRestored = false;
        bool secondaryDeathFinished = false;

        if (secondaryAvatar != null && !secondaryWasAlreadyRespawning)
        {
            secondaryAvatar.HandleDeathStarted();
        }

        FreezeAvatarPhysics(
            secondaryAvatar,
            out secondaryBody,
            out secondaryBodyWasSimulated,
            out secondaryBodyCollider,
            out secondaryColliderWasEnabled);

        try
        {
            if (!secondaryWasAlreadyRespawning && secondaryOverlay != null)
            {
                yield return StartCoroutine(secondaryOverlay.FadeTo(1f, fadeDuration));
            }
            else if (fadeDuration > 0f)
            {
                yield return new WaitForSecondsRealtime(fadeDuration);
            }

            RestoreSceneState();

            if (blackScreenHoldDuration > 0f)
            {
                yield return new WaitForSecondsRealtime(blackScreenHoldDuration);
            }

            if (secondaryAvatar != null)
            {
                secondaryAvatar.RespawnAt(ResolveSpawnPosition(secondarySide));
            }

            RestoreAvatarPhysics(
                secondaryBody,
                secondaryBodyWasSimulated,
                secondaryBodyCollider,
                secondaryColliderWasEnabled);
            secondaryPhysicsRestored = true;

            Physics2D.SyncTransforms();

            if (!secondaryWasAlreadyRespawning && secondaryOverlay != null)
            {
                StartCoroutine(secondaryOverlay.FadeTo(0f, fadeDuration));
            }

            while (primaryLife != null && IsRespawning(primaryLife))
            {
                yield return null;
            }

            Physics2D.SyncTransforms();
            GhostPlatformSensor.RefreshAllSensors();

            if (!secondaryWasAlreadyRespawning && secondaryOverlay != null)
            {
                secondaryOverlay.SetAlpha(0f);
            }

            if (secondaryAvatar != null && !secondaryWasAlreadyRespawning)
            {
                secondaryAvatar.HandleDeathFinished();
                secondaryDeathFinished = true;
            }
        }
        finally
        {
            if (!secondaryPhysicsRestored)
            {
                RestoreAvatarPhysics(
                    secondaryBody,
                    secondaryBodyWasSimulated,
                    secondaryBodyCollider,
                    secondaryColliderWasEnabled);
            }

            Physics2D.SyncTransforms();
            GhostPlatformSensor.RefreshAllSensors();

            if (!secondaryWasAlreadyRespawning && secondaryOverlay != null)
            {
                secondaryOverlay.SetAlpha(0f);
            }

            if (!secondaryDeathFinished && secondaryAvatar != null && !secondaryWasAlreadyRespawning)
            {
                secondaryAvatar.HandleDeathFinished();
            }

            _teamRespawnInProgress = false;
            _teamRespawnRoutine = null;
            _leftWasRespawning = IsRespawning(_leftLife);
            _rightWasRespawning = IsRespawning(_rightLife);
        }
    }

    private void RestoreSceneState()
    {
        if (restoreRespawnableDynamics)
        {
            for (var i = 0; i < _respawnableDynamics.Length; i++)
            {
                var dynamicObject = _respawnableDynamics[i];
                if (dynamicObject != null)
                {
                    dynamicObject.RestoreRespawnState();
                }
            }
        }

        for (var i = 0; i < _teamResettables.Length; i++)
        {
            var resettable = _teamResettables[i];
            if (resettable != null)
            {
                resettable.RestoreForTeamRespawn();
            }
        }

        Physics2D.SyncTransforms();
    }

    private Vector3 ResolveSpawnPosition(Level3Side side)
    {
        var checkpoint = side == Level3Side.Left ? _activeLeftCheckpoint : _activeRightCheckpoint;
        if (checkpoint != null)
        {
            return checkpoint.GetSpawnPosition();
        }

        var fallback = side == Level3Side.Left ? leftFallbackSpawn : rightFallbackSpawn;
        if (fallback != null)
        {
            return fallback.position;
        }

        return side == Level3Side.Left ? _initialLeftSpawnPosition : _initialRightSpawnPosition;
    }

    private void RefreshSceneCollections()
    {
        _respawnableDynamics = FindObjectsOfType<RespawnableDynamic>(true);

        var behaviours = FindObjectsOfType<MonoBehaviour>(true);
        var resettables = new List<ILevel3TeamRespawnResettable>();
        for (var i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is ILevel3TeamRespawnResettable resettable)
            {
                resettables.Add(resettable);
            }
        }

        _teamResettables = resettables.ToArray();
    }

    private void CacheAvatarReferences()
    {
        if (leftAvatar == null || rightAvatar == null)
        {
            var avatars = FindObjectsOfType<Level3PlayerAvatar>(true);
            for (var i = 0; i < avatars.Length; i++)
            {
                if (avatars[i] == null)
                {
                    continue;
                }

                if (avatars[i].Side == Level3Side.Left && leftAvatar == null)
                {
                    leftAvatar = avatars[i];
                }
                else if (avatars[i].Side == Level3Side.Right && rightAvatar == null)
                {
                    rightAvatar = avatars[i];
                }
            }
        }

        _leftLife = leftAvatar != null ? leftAvatar.Life : null;
        if (_leftLife == null && leftAvatar != null)
        {
            _leftLife = leftAvatar.GetComponent<Level3PlayerLife>();
        }

        _rightLife = rightAvatar != null ? rightAvatar.Life : null;
        if (_rightLife == null && rightAvatar != null)
        {
            _rightLife = rightAvatar.GetComponent<Level3PlayerLife>();
        }
    }

    private void CaptureInitialSpawnPositions()
    {
        if (!_hasInitialLeftSpawnPosition && leftAvatar != null)
        {
            _initialLeftSpawnPosition = leftAvatar.transform.position;
            _hasInitialLeftSpawnPosition = true;
        }

        if (!_hasInitialRightSpawnPosition && rightAvatar != null)
        {
            _initialRightSpawnPosition = rightAvatar.transform.position;
            _hasInitialRightSpawnPosition = true;
        }
    }

    private Level3PlayerAvatar GetAvatar(Level3Side side)
    {
        return side == Level3Side.Left ? leftAvatar : rightAvatar;
    }

    private FadeOverlay GetOverlay(Level3Side side)
    {
        return side == Level3Side.Left ? leftOverlay : rightOverlay;
    }

    private void EnsureOverlaysReady()
    {
        if (rightOverlay == null)
        {
            rightOverlay = FindOverlayByNameFragment("right");
        }

        if (leftOverlay == null)
        {
            leftOverlay = FindOverlayByNameFragment("left");
        }

        if (rightOverlay == null)
        {
            var overlays = FindObjectsOfType<FadeOverlay>(true);
            if (overlays.Length == 1)
            {
                rightOverlay = overlays[0];
            }
        }

        if (leftOverlay == null && autoCreateMissingLeftOverlay)
        {
            leftOverlay = CreateLeftOverlay();
        }
    }

    private FadeOverlay FindOverlayByNameFragment(string nameFragment)
    {
        var overlays = FindObjectsOfType<FadeOverlay>(true);
        for (var i = 0; i < overlays.Length; i++)
        {
            var overlay = overlays[i];
            if (overlay == null)
            {
                continue;
            }

            var lowerName = overlay.name.ToLowerInvariant();
            if (lowerName.Contains(nameFragment))
            {
                return overlay;
            }
        }

        return null;
    }

    private FadeOverlay CreateLeftOverlay()
    {
        var parentCanvas = ResolveOverlayCanvas();
        if (parentCanvas == null)
        {
            return null;
        }

        var overlayObject = new GameObject(
            "LeftOverlay",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(CanvasGroup),
            typeof(FadeOverlay));

        overlayObject.layer = parentCanvas.gameObject.layer;
        var overlayTransform = overlayObject.GetComponent<RectTransform>();
        overlayTransform.SetParent(parentCanvas.transform, false);
        overlayTransform.anchorMin = new Vector2(0f, 0f);
        overlayTransform.anchorMax = new Vector2(0.5f, 1f);
        overlayTransform.offsetMin = Vector2.zero;
        overlayTransform.offsetMax = Vector2.zero;
        overlayTransform.pivot = new Vector2(0.5f, 0.5f);

        if (rightOverlay != null)
        {
            overlayTransform.SetSiblingIndex(rightOverlay.transform.GetSiblingIndex());
        }

        var image = overlayObject.GetComponent<Image>();
        var templateImage = rightOverlay != null ? rightOverlay.GetComponent<Image>() : null;
        image.color = templateImage != null ? templateImage.color : Color.black;
        image.raycastTarget = templateImage == null || templateImage.raycastTarget;

        var overlay = overlayObject.GetComponent<FadeOverlay>();
        overlay.SetAlpha(0f);
        return overlay;
    }

    private Canvas ResolveOverlayCanvas()
    {
        if (rightOverlay != null)
        {
            var rightCanvases = rightOverlay.GetComponentsInParent<Canvas>(true);
            if (rightCanvases.Length > 0)
            {
                return rightCanvases[0];
            }
        }

        if (leftOverlay != null)
        {
            var leftCanvases = leftOverlay.GetComponentsInParent<Canvas>(true);
            if (leftCanvases.Length > 0)
            {
                return leftCanvases[0];
            }
        }

        var allCanvases = FindObjectsOfType<Canvas>(true);
        for (var i = 0; i < allCanvases.Length; i++)
        {
            if (allCanvases[i] != null && allCanvases[i].isRootCanvas)
            {
                return allCanvases[i];
            }
        }

        return allCanvases.Length > 0 ? allCanvases[0] : null;
    }

    private static void FreezeAvatarPhysics(
        Level3PlayerAvatar avatar,
        out Rigidbody2D body,
        out bool bodyWasSimulated,
        out Collider2D bodyCollider,
        out bool bodyColliderWasEnabled)
    {
        body = avatar != null ? avatar.Body : null;
        bodyCollider = avatar != null ? avatar.BodyCollider : null;
        bodyWasSimulated = body != null && body.simulated;
        bodyColliderWasEnabled = bodyCollider != null && bodyCollider.enabled;

        if (body != null)
        {
            body.velocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.simulated = false;
        }

        if (bodyCollider != null)
        {
            bodyCollider.enabled = false;
        }
    }

    private static void RestoreAvatarPhysics(
        Rigidbody2D body,
        bool bodyWasSimulated,
        Collider2D bodyCollider,
        bool bodyColliderWasEnabled)
    {
        if (bodyCollider != null)
        {
            bodyCollider.enabled = bodyColliderWasEnabled;
        }

        if (body != null)
        {
            body.simulated = bodyWasSimulated;
            body.velocity = Vector2.zero;
            body.angularVelocity = 0f;
        }
    }

    private static Level3Side GetOppositeSide(Level3Side side)
    {
        return side == Level3Side.Left ? Level3Side.Right : Level3Side.Left;
    }

    private static bool IsRespawning(Level3PlayerLife life)
    {
        if (life == null || IsRespawningField == null)
        {
            return false;
        }

        var value = IsRespawningField.GetValue(life);
        return value is bool isRespawning && isRespawning;
    }

    private static float ResolveFloatField(Level3PlayerLife life, FieldInfo fieldInfo, float fallbackValue)
    {
        if (life == null || fieldInfo == null)
        {
            return fallbackValue;
        }

        var value = fieldInfo.GetValue(life);
        if (value is float floatValue)
        {
            return floatValue;
        }

        return fallbackValue;
    }
}
