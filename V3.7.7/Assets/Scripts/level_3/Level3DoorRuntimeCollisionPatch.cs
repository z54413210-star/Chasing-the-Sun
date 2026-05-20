using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class Level3DoorRuntimeCollisionPatch : MonoBehaviour
{
    private const string Level3SceneName = "Level3";
    private const string RuntimeBlockerName = "Level3DoorRuntimeBlocker";

    private static readonly FieldInfo ControllerField = typeof(Level3DoorInteractionStation).GetField(
        "controller",
        BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly FieldInfo ClosedSpriteField = typeof(Level3DoorInteractionStation).GetField(
        "closedSprite",
        BindingFlags.Instance | BindingFlags.NonPublic);

    private static bool _sceneHookInstalled;

    private Level3DoorInteractionStation _station;
    private Level3DualDoorPuzzleController _controller;
    private SpriteRenderer _doorSpriteRenderer;
    private BoxCollider2D _blockingCollider;
    private bool _hasAppliedState;
    private bool _lastDoorOpenState;
    private bool _warnedMissingClosedSprite;
    private bool _warnedMissingStation;
    private bool _warnedMissingController;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetBootstrapState()
    {
        _sceneHookInstalled = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InstallSceneHook()
    {
        if (_sceneHookInstalled)
        {
            return;
        }

        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        _sceneHookInstalled = true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void PatchActiveScene()
    {
        PatchScene(SceneManager.GetActiveScene());
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode _)
    {
        PatchScene(scene);
    }

    private static void PatchScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded || scene.name != Level3SceneName)
        {
            return;
        }

        var rootObjects = scene.GetRootGameObjects();
        for (var rootIndex = 0; rootIndex < rootObjects.Length; rootIndex++)
        {
            var stations = rootObjects[rootIndex].GetComponentsInChildren<Level3DoorInteractionStation>(true);
            for (var stationIndex = 0; stationIndex < stations.Length; stationIndex++)
            {
                var station = stations[stationIndex];
                if (station == null || station.GetComponentInChildren<Level3DoorRuntimeCollisionPatch>(true) != null)
                {
                    continue;
                }

                station.gameObject.AddComponent<Level3DoorRuntimeCollisionPatch>();
            }
        }
    }

    private void Awake()
    {
        CacheReferences();
        EnsureRuntimeBlocker();
        SyncBlockingState(force: true);
    }

    private void OnEnable()
    {
        CacheReferences();
        EnsureRuntimeBlocker();
        SyncBlockingState(force: true);
    }

    private void LateUpdate()
    {
        SyncBlockingState(force: false);
    }

    private void CacheReferences()
    {
        if (_station == null)
        {
            _station = GetComponent<Level3DoorInteractionStation>();
            if (_station == null)
            {
                _station = GetComponentInParent<Level3DoorInteractionStation>();
            }
        }

        if (_doorSpriteRenderer == null)
        {
            _doorSpriteRenderer = GetComponent<SpriteRenderer>();
            if (_doorSpriteRenderer == null && _station != null)
            {
                _doorSpriteRenderer = _station.GetComponent<SpriteRenderer>();
            }
        }

        if (_controller == null && _station != null && ControllerField != null)
        {
            _controller = ControllerField.GetValue(_station) as Level3DualDoorPuzzleController;
        }

        if (_controller == null && _station != null)
        {
            var controllers = FindObjectsOfType<Level3DualDoorPuzzleController>();
            for (var i = 0; i < controllers.Length; i++)
            {
                if (controllers[i] != null && controllers[i].gameObject.scene == _station.gameObject.scene)
                {
                    _controller = controllers[i];
                    break;
                }
            }
        }

        if (_station == null && !_warnedMissingStation)
        {
            Debug.LogWarning($"{nameof(Level3DoorRuntimeCollisionPatch)} requires {nameof(Level3DoorInteractionStation)} on the same GameObject.", this);
            _warnedMissingStation = true;
        }
        else if (_station != null)
        {
            _warnedMissingStation = false;
        }

        if (_controller == null && _station != null && !_warnedMissingController)
        {
            Debug.LogWarning($"{name}: could not resolve {nameof(Level3DualDoorPuzzleController)}. Runtime door collider will stay non-blocking until the controller is found.", this);
            _warnedMissingController = true;
        }
        else if (_controller != null)
        {
            _warnedMissingController = false;
        }
    }

    private void EnsureRuntimeBlocker()
    {
        if (_blockingCollider != null)
        {
            UpdateBlockingColliderShape();
            return;
        }

        if (TryGetComponent<BoxCollider2D>(out var localCollider))
        {
            _blockingCollider = localCollider;
        }
        else
        {
            var blockerTransform = transform.Find(RuntimeBlockerName);
            GameObject blockerObject;
            if (blockerTransform != null)
            {
                blockerObject = blockerTransform.gameObject;
            }
            else
            {
                blockerObject = new GameObject(RuntimeBlockerName);
                blockerObject.transform.SetParent(transform, false);
                blockerObject.transform.localPosition = Vector3.zero;
                blockerObject.transform.localRotation = Quaternion.identity;
                blockerObject.transform.localScale = Vector3.one;
            }

            blockerObject.layer = LayerMask.NameToLayer(ChaseTheSunProjectSettings.GroundLayer);
            _blockingCollider = blockerObject.GetComponent<BoxCollider2D>();
            if (_blockingCollider == null)
            {
                _blockingCollider = blockerObject.AddComponent<BoxCollider2D>();
            }
        }

        if (_blockingCollider == null)
        {
            return;
        }

        _blockingCollider.gameObject.layer = LayerMask.NameToLayer(ChaseTheSunProjectSettings.GroundLayer);
        _blockingCollider.isTrigger = false;
        UpdateBlockingColliderShape();
    }

    private void UpdateBlockingColliderShape()
    {
        if (_blockingCollider == null)
        {
            return;
        }

        var closedSprite = ResolveClosedSprite();
        if (closedSprite == null)
        {
            return;
        }

        _blockingCollider.offset = closedSprite.bounds.center;
        _blockingCollider.size = closedSprite.bounds.size;
    }

    private Sprite ResolveClosedSprite()
    {
        if (_station != null && ClosedSpriteField != null)
        {
            var reflectedClosedSprite = ClosedSpriteField.GetValue(_station) as Sprite;
            if (reflectedClosedSprite != null)
            {
                _warnedMissingClosedSprite = false;
                return reflectedClosedSprite;
            }
        }

        if (_doorSpriteRenderer != null && _doorSpriteRenderer.sprite != null)
        {
            _warnedMissingClosedSprite = false;
            return _doorSpriteRenderer.sprite;
        }

        if (!_warnedMissingClosedSprite)
        {
            Debug.LogWarning($"{name}: could not resolve a closed-door sprite for runtime blocker sizing.", this);
            _warnedMissingClosedSprite = true;
        }

        return null;
    }

    private void SyncBlockingState(bool force)
    {
        CacheReferences();
        EnsureRuntimeBlocker();

        if (_blockingCollider == null)
        {
            return;
        }

        if (_station == null || _controller == null)
        {
            ApplyBlockingState(shouldBlock: false, force);
            return;
        }

        var doorShouldBeOpen = _controller != null && _controller.ShouldDoorBeOpen(_station.Side);
        if (!force && _hasAppliedState && doorShouldBeOpen == _lastDoorOpenState)
        {
            return;
        }

        ApplyBlockingState(!doorShouldBeOpen, force);
        _lastDoorOpenState = doorShouldBeOpen;
        _hasAppliedState = true;
    }

    private void ApplyBlockingState(bool shouldBlock, bool force)
    {
        if (_blockingCollider == null)
        {
            return;
        }

        if (!force && _blockingCollider.enabled == shouldBlock)
        {
            return;
        }

        _blockingCollider.enabled = shouldBlock;
        Physics2D.SyncTransforms();
    }
}
