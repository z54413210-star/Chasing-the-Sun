using System.Collections.Generic;
using TMPro;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[AddComponentMenu("Chase The Sun/World Space Hint Trigger")]
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public class WorldSpaceHintTrigger : MonoBehaviour
{
    private enum HintCompletionMode
    {
        None,
        AnyWatchedKey,
        AllWatchedKeys,
        AnyHorizontalAndAnyVertical,
        WorldStateChanged
    }

    private enum TriggerTargetMode
    {
        AnyCollider,
        PlayerController2D,
        SpecificTag,
        LayerMask
    }

    private const string DefaultHintTextContent = "A / D - Move left / right\nW / S - Climb up / down ladders";
    private const string HintObjectName = "ReusableHintText";
    private const float DefaultTextScale = 0.08f;
    private const int HintSortingOrder = 25;

    [Header("Content")]
    [SerializeField] [TextArea(2, 5)] private string hintText = DefaultHintTextContent;
    [SerializeField] private TMP_FontAsset fontAsset;
    [SerializeField] private float fontSize = 6f;
    [SerializeField] private Color textColor = Color.white;

    [Header("Trigger")]
    [SerializeField] private Vector2 triggerSize;
    [SerializeField] private Vector2 triggerOffset;
    [SerializeField] private TriggerTargetMode triggerTargetMode = TriggerTargetMode.AnyCollider;
    [SerializeField] private string requiredTag = "Player";
    [SerializeField] private LayerMask triggerLayers = ~0;
    [SerializeField] private bool repeatOnReenter = true;

    [Header("Animation")]
    [SerializeField] private Vector3 textOffset;
    [SerializeField] private float fadeDuration = 0.18f;
    [SerializeField] private float riseDistance = 0.22f;

    [Header("Completion")]
    [SerializeField] private HintCompletionMode completionMode = HintCompletionMode.None;
    [SerializeField] private KeyCode[] watchedKeys = new KeyCode[0];
    [SerializeField] private PresentPastSceneManager sceneManager;

    private readonly List<KeyCode> _pressedKeys = new List<KeyCode>();

    private BoxCollider2D _triggerCollider;
    private SpriteRenderer _spriteRenderer;
    private TextMeshPro _hintText;
    private MeshRenderer _hintRenderer;
    private Transform _hintTransform;
    private Vector3 _baseTextLocalPosition;
    private float _visibility;
    private float _targetVisibility;
    private bool _isPlayerInRange;
    private bool _hasShownOnce;
    private bool _isCompleted;
    private bool _hasHorizontalInput;
    private bool _hasVerticalInput;
    private bool _hasObservedWorldState;
    private PresentPastSceneManager.WorldState _lastObservedWorldState;
#if UNITY_EDITOR
    private bool _editorRefreshQueued;
#endif

    private void Awake()
    {
        CacheComponents();
        ApplyDefaultValuesIfNeeded();
        CacheSceneManager();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
        {
#if UNITY_EDITOR
            QueueEditorRefresh();
#endif
            return;
        }

        EnsureSetup();
        CacheSceneManager();

        if (_isCompleted || !_isPlayerInRange)
        {
            HideImmediately();
            return;
        }

        _targetVisibility = 1f;
        ApplyVisualState();
    }

    private void Reset()
    {
        CacheComponents();
        ApplyDefaultValuesIfNeeded();
        EnsureSetup();
        HideImmediately();
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            return;
        }

        CacheComponents();
        ApplyDefaultValuesIfNeeded();
        EnsureTriggerCollider();
#if UNITY_EDITOR
        QueueEditorRefresh();
#endif
    }

    private void Update()
    {
        if (_hasShownOnce && !_isCompleted)
        {
            TryAdvanceCompletion();
        }

        AnimateHint();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_isCompleted || !IsMatchingCollider(other))
        {
            return;
        }

        if (!repeatOnReenter && _hasShownOnce)
        {
            return;
        }

        _isPlayerInRange = true;
        _hasShownOnce = true;
        CacheSceneManager();
        ShowHint();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!IsMatchingCollider(other))
        {
            return;
        }

        _isPlayerInRange = false;
        if (!_isCompleted)
        {
            HideHint();
        }
    }

    private void EnsureSetup()
    {
        CacheComponents();
        ApplyDefaultValuesIfNeeded();
        EnsureTriggerCollider();
        EnsureHintText();
        RefreshHintText();
    }

    private void CacheComponents()
    {
        if (_triggerCollider == null)
        {
            _triggerCollider = GetComponent<BoxCollider2D>();
        }

        if (_spriteRenderer == null)
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
        }

        if (_hintText == null)
        {
            var existingHint = transform.Find(HintObjectName);
            if (existingHint != null)
            {
                _hintTransform = existingHint;
                _hintText = existingHint.GetComponent<TextMeshPro>();
                _hintRenderer = existingHint.GetComponent<MeshRenderer>();
            }
        }
    }

    private void CacheSceneManager()
    {
        if (sceneManager == null && completionMode == HintCompletionMode.WorldStateChanged)
        {
            sceneManager = FindObjectOfType<PresentPastSceneManager>();
        }

        if (sceneManager != null && !_hasObservedWorldState)
        {
            _lastObservedWorldState = sceneManager.CurrentState;
            _hasObservedWorldState = true;
        }
    }

    private void ApplyDefaultValuesIfNeeded()
    {
        var bounds = GetLocalSpriteBounds();

        if (triggerSize.x <= 0f || triggerSize.y <= 0f)
        {
            triggerSize = new Vector2(
                Mathf.Max(bounds.size.x * 1.5f, 1.4f),
                Mathf.Max(bounds.size.y * 1.6f, 1.8f));
        }

        if (Mathf.Approximately(triggerOffset.sqrMagnitude, 0f))
        {
            triggerOffset = bounds.center;
        }

        if (textOffset.sqrMagnitude <= 0.0001f)
        {
            textOffset = bounds.center + new Vector3(0f, bounds.extents.y + 0.45f, 0f);
        }

        if (fadeDuration <= 0f)
        {
            fadeDuration = 0.18f;
        }

        if (riseDistance <= 0f)
        {
            riseDistance = 0.22f;
        }

        if (fontSize <= 0f)
        {
            fontSize = 6f;
        }
    }

    private Bounds GetLocalSpriteBounds()
    {
        if (_spriteRenderer != null && _spriteRenderer.sprite != null)
        {
            return _spriteRenderer.sprite.bounds;
        }

        return new Bounds(Vector3.zero, new Vector3(1.2f, 1.2f, 0f));
    }

    private void EnsureTriggerCollider()
    {
        if (_triggerCollider == null)
        {
            _triggerCollider = gameObject.AddComponent<BoxCollider2D>();
        }

        _triggerCollider.isTrigger = true;
        _triggerCollider.size = triggerSize;
        _triggerCollider.offset = triggerOffset;
    }

    private void EnsureHintText()
    {
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

        _hintTransform.localRotation = Quaternion.identity;
        _hintTransform.localScale = Vector3.one * DefaultTextScale;

        ApplyTextStyle();

        _hintText.enableWordWrapping = false;
        _hintText.overflowMode = TextOverflowModes.Overflow;
        _hintText.alignment = TextAlignmentOptions.Center;
        _hintText.isOrthographic = true;
        _hintText.rectTransform.sizeDelta = new Vector2(28f, 6f);

        if (_hintRenderer != null)
        {
            _hintRenderer.sortingLayerName = ChaseTheSunProjectSettings.OverlaySortingLayer;
            _hintRenderer.sortingOrder = HintSortingOrder;
            _hintRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _hintRenderer.receiveShadows = false;
        }

        _baseTextLocalPosition = textOffset;
    }

    private void RefreshHintText()
    {
        if (_hintText == null)
        {
            return;
        }

        ApplyTextStyle();
        _baseTextLocalPosition = textOffset;
    }

    private void ApplyTextStyle()
    {
        if (_hintText == null)
        {
            return;
        }

        if (fontAsset != null)
        {
            _hintText.font = fontAsset;
        }
        else if (TMP_Settings.defaultFontAsset != null)
        {
            _hintText.font = TMP_Settings.defaultFontAsset;
        }

        _hintText.text = ResolveHintText();
        _hintText.fontSize = Mathf.Max(0.1f, fontSize);
    }

    private string ResolveHintText()
    {
        return string.IsNullOrWhiteSpace(hintText) ? DefaultHintTextContent : hintText;
    }

    private void TryAdvanceCompletion()
    {
        switch (completionMode)
        {
            case HintCompletionMode.AnyWatchedKey:
                if (HasAnyWatchedKeyDown())
                {
                    CompleteHint();
                }

                break;
            case HintCompletionMode.AllWatchedKeys:
                TrackPressedWatchedKeys();
                if (HavePressedAllWatchedKeys())
                {
                    CompleteHint();
                }

                break;
            case HintCompletionMode.AnyHorizontalAndAnyVertical:
                if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.D))
                {
                    _hasHorizontalInput = true;
                }

                if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.S))
                {
                    _hasVerticalInput = true;
                }

                if (_hasHorizontalInput && _hasVerticalInput)
                {
                    CompleteHint();
                }

                break;
            case HintCompletionMode.WorldStateChanged:
                CacheSceneManager();
                if (sceneManager != null)
                {
                    if (sceneManager.CurrentState != _lastObservedWorldState)
                    {
                        _lastObservedWorldState = sceneManager.CurrentState;
                        CompleteHint();
                    }
                }
                else if (HasAnyWatchedKeyDown())
                {
                    CompleteHint();
                }

                break;
        }
    }

    private bool HasAnyWatchedKeyDown()
    {
        if (watchedKeys == null)
        {
            return false;
        }

        for (var i = 0; i < watchedKeys.Length; i++)
        {
            if (Input.GetKeyDown(watchedKeys[i]))
            {
                return true;
            }
        }

        return false;
    }

    private void TrackPressedWatchedKeys()
    {
        if (watchedKeys == null)
        {
            return;
        }

        for (var i = 0; i < watchedKeys.Length; i++)
        {
            var key = watchedKeys[i];
            if (Input.GetKeyDown(key) && !_pressedKeys.Contains(key))
            {
                _pressedKeys.Add(key);
            }
        }
    }

    private bool HavePressedAllWatchedKeys()
    {
        if (watchedKeys == null || watchedKeys.Length == 0)
        {
            return false;
        }

        for (var i = 0; i < watchedKeys.Length; i++)
        {
            if (!_pressedKeys.Contains(watchedKeys[i]))
            {
                return false;
            }
        }

        return true;
    }

    private void ShowHint()
    {
        if (_isCompleted)
        {
            return;
        }

        _targetVisibility = 1f;
        SetHintActive(true);
    }

    private void HideHint()
    {
        _targetVisibility = 0f;
    }

    private void HideImmediately()
    {
        _targetVisibility = 0f;
        _visibility = 0f;
        ApplyVisualState();
        SetHintActive(false);
    }

    private void CompleteHint()
    {
        _isCompleted = true;
        _isPlayerInRange = false;
        HideHint();
    }

    private void AnimateHint()
    {
        if (_hintText == null)
        {
            return;
        }

        if (_targetVisibility > 0f)
        {
            SetHintActive(true);
        }

        if (Mathf.Approximately(_visibility, _targetVisibility))
        {
            if (_targetVisibility <= 0f && _visibility <= 0f)
            {
                SetHintActive(false);
            }

            return;
        }

        var step = fadeDuration <= 0f ? 1f : Time.deltaTime / fadeDuration;
        _visibility = Mathf.MoveTowards(_visibility, _targetVisibility, step);
        ApplyVisualState();

        if (_targetVisibility <= 0f && _visibility <= 0f)
        {
            SetHintActive(false);
        }
    }

    private void ApplyVisualState()
    {
        if (_hintText == null || _hintTransform == null)
        {
            return;
        }

        _hintTransform.localPosition = _baseTextLocalPosition + (Vector3.up * riseDistance * _visibility);

        var color = textColor;
        color.a *= _visibility;
        _hintText.color = color;
    }

    private void SetHintActive(bool isActive)
    {
        if (_hintText != null)
        {
            _hintText.gameObject.SetActive(isActive);
        }
    }

#if UNITY_EDITOR
    private void QueueEditorRefresh()
    {
        if (_editorRefreshQueued)
        {
            return;
        }

        _editorRefreshQueued = true;
        EditorApplication.delayCall += RefreshEditorPreview;
    }

    private void RefreshEditorPreview()
    {
        EditorApplication.delayCall -= RefreshEditorPreview;
        _editorRefreshQueued = false;

        if (this == null || gameObject == null || Application.isPlaying)
        {
            return;
        }

        EnsureSetup();
        ApplyVisualState();
    }
#endif

    private bool IsMatchingCollider(Collider2D other)
    {
        if (other == null)
        {
            return false;
        }

        switch (triggerTargetMode)
        {
            case TriggerTargetMode.AnyCollider:
                return true;
            case TriggerTargetMode.PlayerController2D:
                return other.GetComponent<PlayerController2D>() != null || other.GetComponentInParent<PlayerController2D>() != null;
            case TriggerTargetMode.SpecificTag:
                return MatchesRequiredTag(other);
            case TriggerTargetMode.LayerMask:
                return MatchesLayerMask(other);
            default:
                return false;
        }
    }

    private bool MatchesRequiredTag(Collider2D other)
    {
        if (!string.IsNullOrWhiteSpace(requiredTag))
        {
            if (other.CompareTag(requiredTag))
            {
                return true;
            }

            if (other.attachedRigidbody != null && other.attachedRigidbody.CompareTag(requiredTag))
            {
                return true;
            }
        }

        return false;
    }

    private bool MatchesLayerMask(Collider2D other)
    {
        var otherLayerMask = 1 << other.gameObject.layer;
        if ((triggerLayers.value & otherLayerMask) != 0)
        {
            return true;
        }

        if (other.attachedRigidbody != null)
        {
            var rigidbodyLayerMask = 1 << other.attachedRigidbody.gameObject.layer;
            return (triggerLayers.value & rigidbodyLayerMask) != 0;
        }

        return false;
    }
}
