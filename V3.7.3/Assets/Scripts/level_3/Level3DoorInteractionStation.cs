using System.Collections.Generic;
using TMPro;
using UnityEngine;

[AddComponentMenu("Chase The Sun/Level 3/Level 3 Door Interaction Station")]
[DisallowMultipleComponent]
public class Level3DoorInteractionStation : MonoBehaviour
{
    private sealed class MovementLockState
    {
        public int Count;
        public bool WasEnabled;
    }

    private const string HintObjectName = "Level3DoorHintText";
    private const float DefaultTextScale = 0.08f;
    private const int HintSortingOrder = 25;

    private static readonly Dictionary<Behaviour, MovementLockState> MovementLocks =
        new Dictionary<Behaviour, MovementLockState>();

    [Header("Puzzle")]
    [SerializeField] private Level3DualDoorPuzzleController controller;
    [SerializeField] private Level3DualDoorPuzzleController.Level3PuzzleSide side =
        Level3DualDoorPuzzleController.Level3PuzzleSide.Left;

    [Header("Player Binding")]
    [SerializeField] private PlayerController2D specificPlayer;
    [SerializeField] private PlayerOneLevel3Controller2D specificLevel3PlayerOne;

    [Header("Input")]
    [SerializeField] private KeyCode enterKey = KeyCode.F;
    [SerializeField] private KeyCode quitKey = KeyCode.Q;
    [SerializeField] private KeyCode topKey = KeyCode.Alpha1;
    [SerializeField] private KeyCode middleKey = KeyCode.Alpha2;
    [SerializeField] private KeyCode bottomKey = KeyCode.Alpha3;

    [Header("Interaction Area")]
    [SerializeField] private Vector2 interactionBoxSize = new Vector2(1.6f, 2f);
    [SerializeField] private Vector2 interactionBoxOffset = new Vector2(0f, 0.8f);

    [Header("Text Content")]
    [SerializeField] private string idlePromptText = "F Interact";
    [SerializeField] private string startText = "Start";
    [SerializeField] private string quitText = "Quit";

    [Header("Hint Text")]
    [SerializeField] private Vector3 textOffset = new Vector3(0f, 1.4f, 0f);
    [SerializeField] private TMP_FontAsset fontAsset;
    [SerializeField] private float fontSize = 6f;
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private float textDuration = 1.2f;
    [SerializeField] private float textScale = DefaultTextScale;

    [Header("Door Visuals")]
    [SerializeField] private SpriteRenderer doorSpriteRenderer;
    [SerializeField] private Sprite closedSprite;
    [SerializeField] private Sprite openSprite;
    [SerializeField] [HideInInspector] private Level3DualDoorPuzzleController.Level3PuzzleSide lastConfiguredSide;
    [SerializeField] [HideInInspector] private bool hasAppliedSideKeyDefaults;

    private readonly Collider2D[] _overlapResults = new Collider2D[16];

    private SpriteRenderer _localSpriteRenderer;
    private TextMeshPro _hintText;
    private MeshRenderer _hintRenderer;
    private Transform _hintTransform;
    private Behaviour _playerInRange;
    private Behaviour _interactingPlayer;
    private Behaviour _lockedPlayer;
    private string _temporaryMessage;
    private float _temporaryMessageTimer;

    public Level3DualDoorPuzzleController.Level3PuzzleSide Side => side;
    public bool IsInteracting => _interactingPlayer != null;

    private void Awake()
    {
        CacheReferences();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        CacheReferences();
        EnsureHintText();
        RefreshHintVisuals();
        HideHint();

        if (controller != null)
        {
            controller.RegisterDoorStation(this);
        }
        else
        {
            SetDoorOpenVisual(false);
        }
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        ReleaseInteractionLock();

        if (controller != null)
        {
            controller.UnregisterDoorStation(this);
        }

        _playerInRange = null;
        _interactingPlayer = null;
        _temporaryMessage = null;
        _temporaryMessageTimer = 0f;
        HideHint();
    }

    private void Reset()
    {
        CacheReferences();
        ApplySpriteBasedDefaults();
        ApplyDefaultKeysForSide(force: true);
        lastConfiguredSide = side;
        hasAppliedSideKeyDefaults = true;
    }

    private void OnValidate()
    {
        CacheReferences();
        MaybeApplySideKeyDefaultsOnValidate();

        fontSize = Mathf.Max(0.1f, fontSize);
        textDuration = Mathf.Max(0.1f, textDuration);
        textScale = Mathf.Max(0.001f, textScale);
        interactionBoxSize = new Vector2(
            Mathf.Max(0.1f, interactionBoxSize.x),
            Mathf.Max(0.1f, interactionBoxSize.y));

        if (!Application.isPlaying && _hintText != null)
        {
            RefreshHintVisuals();
            RefreshDisplayedText();
        }
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        _playerInRange = FindPlayerInRange();
        UpdateTemporaryMessageTimer();

        if (_interactingPlayer == null)
        {
            TryEnterInteraction();
            RefreshDisplayedText();
            return;
        }

        if (_lockedPlayer == null)
        {
            _interactingPlayer = null;
            RefreshDisplayedText();
            return;
        }

        if (Input.GetKeyDown(quitKey))
        {
            StopInteraction(showQuitMessage: true);
            RefreshDisplayedText();
            return;
        }

        ProcessSwitchInputs();
        RefreshDisplayedText();
    }

    public void SetDoorOpenVisual(bool isOpen)
    {
        CacheReferences();

        if (Application.isPlaying && isOpen && IsInteracting)
        {
            StopInteraction(showQuitMessage: false);
            RefreshDisplayedText();
        }

        if (doorSpriteRenderer == null)
        {
            return;
        }

        var targetSprite = isOpen ? openSprite : closedSprite;
        if (targetSprite == null || doorSpriteRenderer.sprite == targetSprite)
        {
            return;
        }

        doorSpriteRenderer.sprite = targetSprite;
    }

    private void CacheReferences()
    {
        if (doorSpriteRenderer == null)
        {
            doorSpriteRenderer = GetComponent<SpriteRenderer>();
        }

        if (_localSpriteRenderer == null)
        {
            _localSpriteRenderer = GetComponent<SpriteRenderer>();
        }
    }

    private void ApplySpriteBasedDefaults()
    {
        var bounds = GetLocalSpriteBounds();
        interactionBoxSize = new Vector2(
            Mathf.Max(bounds.size.x * 1.5f, 1.4f),
            Mathf.Max(bounds.size.y * 1.6f, 1.8f));
        interactionBoxOffset = bounds.center;
        textOffset = bounds.center + new Vector3(0f, bounds.extents.y + 0.45f, 0f);
    }

    private void ApplyDefaultKeysForSide(bool force)
    {
        if (!force)
        {
            return;
        }

        if (side == Level3DualDoorPuzzleController.Level3PuzzleSide.Left)
        {
            topKey = KeyCode.Alpha1;
            middleKey = KeyCode.Alpha2;
            bottomKey = KeyCode.Alpha3;
            return;
        }

        topKey = KeyCode.Alpha4;
        middleKey = KeyCode.Alpha5;
        bottomKey = KeyCode.Alpha6;
    }

    private void MaybeApplySideKeyDefaultsOnValidate()
    {
        if (!hasAppliedSideKeyDefaults)
        {
            ApplyDefaultKeysForSide(force: true);
            hasAppliedSideKeyDefaults = true;
            lastConfiguredSide = side;
            return;
        }

        if (side != lastConfiguredSide && KeysMatchSideDefaults(lastConfiguredSide))
        {
            ApplyDefaultKeysForSide(force: true);
        }

        lastConfiguredSide = side;
    }

    private bool KeysMatchSideDefaults(Level3DualDoorPuzzleController.Level3PuzzleSide sideToCheck)
    {
        if (sideToCheck == Level3DualDoorPuzzleController.Level3PuzzleSide.Left)
        {
            return topKey == KeyCode.Alpha1
                && middleKey == KeyCode.Alpha2
                && bottomKey == KeyCode.Alpha3;
        }

        return topKey == KeyCode.Alpha4
            && middleKey == KeyCode.Alpha5
            && bottomKey == KeyCode.Alpha6;
    }

    private Bounds GetLocalSpriteBounds()
    {
        if (_localSpriteRenderer != null && _localSpriteRenderer.sprite != null)
        {
            return _localSpriteRenderer.sprite.bounds;
        }

        return new Bounds(new Vector3(0f, 0.5f, 0f), new Vector3(1f, 1f, 0f));
    }

    private void TryEnterInteraction()
    {
        if (_playerInRange == null || !Input.GetKeyDown(enterKey))
        {
            return;
        }

        _interactingPlayer = _playerInRange;
        AcquireInteractionLock(_interactingPlayer);
        ShowTemporaryMessage(ResolveStartText());
    }

    private void StopInteraction(bool showQuitMessage)
    {
        ReleaseInteractionLock();
        _interactingPlayer = null;

        if (showQuitMessage)
        {
            ShowTemporaryMessage(ResolveQuitText());
        }
    }

    private void AcquireInteractionLock(Behaviour player)
    {
        if (player == null)
        {
            return;
        }

        _lockedPlayer = player;
        AcquireMovementLock(player);

        var playerBody = player.GetComponent<Rigidbody2D>();
        if (playerBody != null)
        {
            playerBody.velocity = Vector2.zero;
            playerBody.angularVelocity = 0f;
        }
    }

    private void ReleaseInteractionLock()
    {
        if (_lockedPlayer == null)
        {
            return;
        }

        ReleaseMovementLock(_lockedPlayer);
        _lockedPlayer = null;
    }

    private void ProcessSwitchInputs()
    {
        if (controller == null)
        {
            return;
        }

        if (Input.GetKeyDown(topKey))
        {
            controller.SubmitSwitchInput(side, Level3DualDoorPuzzleController.DoorSwitchPosition.Top);
        }

        if (Input.GetKeyDown(middleKey))
        {
            controller.SubmitSwitchInput(side, Level3DualDoorPuzzleController.DoorSwitchPosition.Middle);
        }

        if (Input.GetKeyDown(bottomKey))
        {
            controller.SubmitSwitchInput(side, Level3DualDoorPuzzleController.DoorSwitchPosition.Bottom);
        }
    }

    private Behaviour FindPlayerInRange()
    {
        var worldCenter = (Vector2)transform.TransformPoint(interactionBoxOffset);
        var worldSize = GetWorldInteractionSize();
        var hitCount = Physics2D.OverlapBoxNonAlloc(worldCenter, worldSize, transform.eulerAngles.z, _overlapResults);

        Behaviour bestPlayer = null;
        var bestDistance = float.MaxValue;

        for (var i = 0; i < hitCount; i++)
        {
            var colliderHit = _overlapResults[i];
            _overlapResults[i] = null;

            var player = ResolvePlayer(colliderHit);
            if (player == null)
            {
                continue;
            }

            if (specificPlayer != null && player != specificPlayer)
            {
                if (!IsSamePlayerObject(player, specificPlayer))
                {
                    continue;
                }
            }

            if (specificLevel3PlayerOne != null && player != specificLevel3PlayerOne)
            {
                continue;
            }

            var distance = GetPriorityDistanceSqr(player);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestPlayer = player;
            }
        }

        return bestPlayer;
    }

    private static Behaviour ResolvePlayer(Collider2D colliderHit)
    {
        if (colliderHit == null)
        {
            return null;
        }

        var level3PlayerOne = colliderHit.GetComponent<PlayerOneLevel3Controller2D>();
        if (level3PlayerOne != null)
        {
            return level3PlayerOne;
        }

        if (colliderHit.attachedRigidbody != null)
        {
            level3PlayerOne = colliderHit.attachedRigidbody.GetComponent<PlayerOneLevel3Controller2D>();
            if (level3PlayerOne != null)
            {
                return level3PlayerOne;
            }
        }

        level3PlayerOne = colliderHit.GetComponentInParent<PlayerOneLevel3Controller2D>();
        if (level3PlayerOne != null)
        {
            return level3PlayerOne;
        }

        var level3PlayerTwo = colliderHit.GetComponent<PlayerTwoController2D>();
        if (level3PlayerTwo != null)
        {
            return level3PlayerTwo;
        }

        if (colliderHit.attachedRigidbody != null)
        {
            level3PlayerTwo = colliderHit.attachedRigidbody.GetComponent<PlayerTwoController2D>();
            if (level3PlayerTwo != null)
            {
                return level3PlayerTwo;
            }
        }

        level3PlayerTwo = colliderHit.GetComponentInParent<PlayerTwoController2D>();
        if (level3PlayerTwo != null)
        {
            return level3PlayerTwo;
        }

        var player = colliderHit.GetComponent<PlayerController2D>();
        if (player != null)
        {
            return player;
        }

        if (colliderHit.attachedRigidbody != null)
        {
            player = colliderHit.attachedRigidbody.GetComponent<PlayerController2D>();
            if (player != null)
            {
                return player;
            }
        }

        return colliderHit.GetComponentInParent<PlayerController2D>();
    }

    private static bool IsSamePlayerObject(Behaviour player, PlayerController2D specificPlayer)
    {
        return player != null
            && specificPlayer != null
            && player.gameObject == specificPlayer.gameObject;
    }

    private Vector2 GetWorldInteractionSize()
    {
        var scale = transform.lossyScale;
        return new Vector2(
            interactionBoxSize.x * Mathf.Abs(scale.x),
            interactionBoxSize.y * Mathf.Abs(scale.y));
    }

    private float GetPriorityDistanceSqr(Behaviour player)
    {
        if (player == null)
        {
            return float.MaxValue;
        }

        var targetPoint = transform.TransformPoint(interactionBoxOffset);
        return (player.transform.position - targetPoint).sqrMagnitude;
    }

    private void UpdateTemporaryMessageTimer()
    {
        if (_temporaryMessageTimer <= 0f)
        {
            return;
        }

        _temporaryMessageTimer -= Time.deltaTime;
        if (_temporaryMessageTimer <= 0f)
        {
            _temporaryMessageTimer = 0f;
            _temporaryMessage = null;
        }
    }

    private void ShowTemporaryMessage(string message)
    {
        _temporaryMessage = string.IsNullOrWhiteSpace(message) ? ResolveIdlePromptText() : message;
        _temporaryMessageTimer = textDuration;
    }

    private void EnsureHintText()
    {
        if (_hintText != null)
        {
            return;
        }

        var existingHint = transform.Find(HintObjectName);
        if (existingHint != null)
        {
            _hintTransform = existingHint;
            _hintText = existingHint.GetComponent<TextMeshPro>();
            _hintRenderer = existingHint.GetComponent<MeshRenderer>();
        }

        if (_hintText == null)
        {
            var hintObject = new GameObject(HintObjectName);
            hintObject.transform.SetParent(transform, false);
            _hintTransform = hintObject.transform;
            _hintText = hintObject.AddComponent<TextMeshPro>();
            _hintRenderer = hintObject.GetComponent<MeshRenderer>();
        }

        if (_hintTransform == null)
        {
            _hintTransform = _hintText.transform;
        }

        if (_hintRenderer == null)
        {
            _hintRenderer = _hintText.GetComponent<MeshRenderer>();
        }
    }

    private void RefreshHintVisuals()
    {
        if (_hintText == null || _hintTransform == null)
        {
            return;
        }

        _hintTransform.localPosition = textOffset;
        _hintTransform.localRotation = Quaternion.identity;
        _hintTransform.localScale = Vector3.one * textScale;

        _hintText.enableWordWrapping = false;
        _hintText.overflowMode = TextOverflowModes.Overflow;
        _hintText.alignment = TextAlignmentOptions.Center;
        _hintText.isOrthographic = true;
        _hintText.rectTransform.sizeDelta = new Vector2(28f, 6f);
        _hintText.fontSize = fontSize;
        _hintText.color = textColor;

        if (fontAsset != null)
        {
            _hintText.font = fontAsset;
        }
        else if (TMP_Settings.defaultFontAsset != null)
        {
            _hintText.font = TMP_Settings.defaultFontAsset;
        }

        if (_hintRenderer != null)
        {
            _hintRenderer.sortingLayerName = ChaseTheSunProjectSettings.OverlaySortingLayer;
            _hintRenderer.sortingOrder = HintSortingOrder;
            _hintRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _hintRenderer.receiveShadows = false;
        }
    }

    private void RefreshDisplayedText()
    {
        if (_hintText == null)
        {
            return;
        }

        var displayText = ResolveDisplayText();
        if (string.IsNullOrWhiteSpace(displayText))
        {
            HideHint();
            return;
        }

        if (_hintText.text != displayText)
        {
            _hintText.text = displayText;
        }

        if (!_hintText.gameObject.activeSelf)
        {
            _hintText.gameObject.SetActive(true);
        }
    }

    private string ResolveDisplayText()
    {
        if (_temporaryMessageTimer > 0f && !string.IsNullOrWhiteSpace(_temporaryMessage))
        {
            return _temporaryMessage;
        }

        if (!IsInteracting && _playerInRange != null)
        {
            return ResolveIdlePromptText();
        }

        return string.Empty;
    }

    private string ResolveIdlePromptText()
    {
        return string.IsNullOrWhiteSpace(idlePromptText)
            ? enterKey + " Interact"
            : idlePromptText;
    }

    private string ResolveStartText()
    {
        return string.IsNullOrWhiteSpace(startText) ? "Start" : startText;
    }

    private string ResolveQuitText()
    {
        return string.IsNullOrWhiteSpace(quitText) ? "Quit" : quitText;
    }

    private void HideHint()
    {
        if (_hintText != null)
        {
            _hintText.gameObject.SetActive(false);
        }
    }

    private static void AcquireMovementLock(Behaviour player)
    {
        if (player == null)
        {
            return;
        }

        if (!MovementLocks.TryGetValue(player, out var lockState))
        {
            lockState = new MovementLockState();
            MovementLocks.Add(player, lockState);
        }

        if (lockState.Count == 0)
        {
            lockState.WasEnabled = player.enabled;
            if (player.enabled)
            {
                player.enabled = false;
            }
        }

        lockState.Count++;
    }

    private static void ReleaseMovementLock(Behaviour player)
    {
        if (player == null || !MovementLocks.TryGetValue(player, out var lockState))
        {
            return;
        }

        lockState.Count = Mathf.Max(0, lockState.Count - 1);
        if (lockState.Count > 0)
        {
            return;
        }

        if (lockState.WasEnabled)
        {
            player.enabled = true;
        }

        MovementLocks.Remove(player);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.8f);

        var previousMatrix = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(
            transform.TransformPoint(interactionBoxOffset),
            transform.rotation,
            new Vector3(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y), 1f));
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(interactionBoxSize.x, interactionBoxSize.y, 0f));
        Gizmos.matrix = previousMatrix;
    }
}
