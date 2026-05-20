using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

[AddComponentMenu("Chase The Sun/Level 1/CTS Lever Interactable")]
[DisallowMultipleComponent]
public class CTSLeverInteractable : MonoBehaviour
{
    public enum LeverMode
    {
        Functional,
        Broken
    }

    private const string HintObjectName = "CTSLeverHintText";
    private const float DefaultTextScale = 0.08f;
    private const int HintSortingOrder = 25;

    private static readonly List<CTSLeverInteractable> ActiveLevers = new List<CTSLeverInteractable>();

    [Header("Lever")]
    [SerializeField] private LeverMode leverMode = LeverMode.Functional;
    [SerializeField] private KeyCode interactionKey = KeyCode.F;

    [Header("Interaction Area")]
    [SerializeField] private Vector2 interactionBoxSize = new Vector2(1.6f, 2f);
    [SerializeField] private Vector2 interactionBoxOffset = new Vector2(0f, 0.8f);

    [Header("Text Content")]
    [SerializeField] private string interactionPrompt = "F \u4ea4\u4e92";
    [SerializeField] [TextArea(2, 4)] private string activatedMessage = "\u62c9\u6746\u88ab\u62c9\u4e0b\u4e86\u3002";
    [SerializeField] [TextArea(2, 4)] private string alreadyActivatedMessage = "\u4f60\u5df2\u7ecf\u62c9\u52a8\u8fc7\u4e86\u3002";
    [SerializeField] [TextArea(2, 4)] private string brokenMessage = "\u5b83\u597d\u50cf\u5df2\u7ecf\u574f\u4e86\u3002";

    [Header("Hint Text")]
    [SerializeField] private Vector3 textOffset = new Vector3(0f, 1.4f, 0f);
    [SerializeField] private TMP_FontAsset fontAsset;
    [SerializeField] private float fontSize = 6f;
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private float feedbackDuration = 1.8f;
    [SerializeField] private float textScale = DefaultTextScale;

    private readonly Collider2D[] _overlapResults = new Collider2D[8];

    private SpriteRenderer _spriteRenderer;
    private TextMeshPro _hintText;
    private MeshRenderer _hintRenderer;
    private Transform _hintTransform;
    private PlayerController2D _playerInRange;
    private string _temporaryMessage;
    private float _temporaryMessageTimer;
    private bool _isActivated;

    public event Action<CTSLeverInteractable> ActivatedStateChanged;

    public bool IsActivated => _isActivated;
    public bool IsFunctional => leverMode == LeverMode.Functional;
    public LeverMode CurrentMode => leverMode;

    private void Awake()
    {
        CacheComponents();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        RegisterLever();
        EnsureHintText();
        RefreshHintVisuals();
        HideHint();
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        UnregisterLever();
        _playerInRange = null;
        _temporaryMessage = null;
        _temporaryMessageTimer = 0f;
        HideHint();
    }

    private void Reset()
    {
        CacheComponents();
        ApplySpriteBasedDefaults();
    }

    private void OnValidate()
    {
        CacheComponents();
        fontSize = Mathf.Max(0.1f, fontSize);
        feedbackDuration = Mathf.Max(0.1f, feedbackDuration);
        textScale = Mathf.Max(0.001f, textScale);
        interactionBoxSize = new Vector2(Mathf.Max(0.1f, interactionBoxSize.x), Mathf.Max(0.1f, interactionBoxSize.y));

        if (Application.isPlaying)
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

        if (_temporaryMessageTimer > 0f)
        {
            _temporaryMessageTimer -= Time.deltaTime;
            if (_temporaryMessageTimer <= 0f)
            {
                _temporaryMessageTimer = 0f;
                _temporaryMessage = null;
            }
        }
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        RefreshDisplayedText();

        if (!CanProcessInteraction())
        {
            return;
        }

        ProcessInteraction();
        RefreshDisplayedText();
    }

    private void CacheComponents()
    {
        if (_spriteRenderer == null)
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
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

    private Bounds GetLocalSpriteBounds()
    {
        if (_spriteRenderer != null && _spriteRenderer.sprite != null)
        {
            return _spriteRenderer.sprite.bounds;
        }

        return new Bounds(new Vector3(0f, 0.5f, 0f), new Vector3(1f, 1f, 0f));
    }

    private void RegisterLever()
    {
        if (!ActiveLevers.Contains(this))
        {
            ActiveLevers.Add(this);
        }
    }

    private void UnregisterLever()
    {
        ActiveLevers.Remove(this);
    }

    private PlayerController2D FindPlayerInRange()
    {
        var worldCenter = (Vector2)transform.TransformPoint(interactionBoxOffset);
        var worldSize = GetWorldInteractionSize();
        var hitCount = Physics2D.OverlapBoxNonAlloc(worldCenter, worldSize, transform.eulerAngles.z, _overlapResults);

        PlayerController2D bestPlayer = null;
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

            var distance = GetPriorityDistanceSqr(player);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestPlayer = player;
            }
        }

        return bestPlayer;
    }

    private static PlayerController2D ResolvePlayer(Collider2D colliderHit)
    {
        if (colliderHit == null)
        {
            return null;
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

    private Vector2 GetWorldInteractionSize()
    {
        var scale = transform.lossyScale;
        return new Vector2(
            interactionBoxSize.x * Mathf.Abs(scale.x),
            interactionBoxSize.y * Mathf.Abs(scale.y));
    }

    private bool CanProcessInteraction()
    {
        return _playerInRange != null
            && Input.GetKeyDown(interactionKey)
            && IsHighestPriorityLever();
    }

    private bool IsHighestPriorityLever()
    {
        if (_playerInRange == null)
        {
            return false;
        }

        var myDistance = GetPriorityDistanceSqr(_playerInRange);
        var myInstanceId = GetInstanceID();

        for (var i = 0; i < ActiveLevers.Count; i++)
        {
            var candidate = ActiveLevers[i];
            if (candidate == null || candidate == this || !candidate.isActiveAndEnabled)
            {
                continue;
            }

            if (candidate.interactionKey != interactionKey)
            {
                continue;
            }

            if (candidate._playerInRange != _playerInRange)
            {
                continue;
            }

            var candidateDistance = candidate.GetPriorityDistanceSqr(_playerInRange);
            if (candidateDistance < myDistance - 0.0001f)
            {
                return false;
            }

            if (Mathf.Abs(candidateDistance - myDistance) <= 0.0001f && candidate.GetInstanceID() < myInstanceId)
            {
                return false;
            }
        }

        return true;
    }

    private float GetPriorityDistanceSqr(PlayerController2D player)
    {
        if (player == null)
        {
            return float.MaxValue;
        }

        var targetPoint = transform.TransformPoint(interactionBoxOffset);
        return (player.transform.position - targetPoint).sqrMagnitude;
    }

    private void ProcessInteraction()
    {
        if (leverMode == LeverMode.Broken)
        {
            ShowTemporaryMessage(ResolveBrokenMessage());
            return;
        }

        if (_isActivated)
        {
            ShowTemporaryMessage(ResolveAlreadyActivatedMessage());
            return;
        }

        _isActivated = true;
        ShowTemporaryMessage(ResolveActivatedMessage());
        ActivatedStateChanged?.Invoke(this);
    }

    private void ShowTemporaryMessage(string message)
    {
        _temporaryMessage = string.IsNullOrWhiteSpace(message) ? ResolveInteractionPrompt() : message;
        _temporaryMessageTimer = feedbackDuration;
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

        if (_playerInRange != null && IsHighestPriorityLever())
        {
            return ResolveInteractionPrompt();
        }

        return string.Empty;
    }

    private string ResolveInteractionPrompt()
    {
        return string.IsNullOrWhiteSpace(interactionPrompt)
            ? interactionKey + " \u4ea4\u4e92"
            : interactionPrompt;
    }

    private string ResolveActivatedMessage()
    {
        return string.IsNullOrWhiteSpace(activatedMessage) ? "\u62c9\u6746\u88ab\u62c9\u4e0b\u4e86\u3002" : activatedMessage;
    }

    private string ResolveAlreadyActivatedMessage()
    {
        return string.IsNullOrWhiteSpace(alreadyActivatedMessage) ? "\u4f60\u5df2\u7ecf\u62c9\u52a8\u8fc7\u4e86\u3002" : alreadyActivatedMessage;
    }

    private string ResolveBrokenMessage()
    {
        return string.IsNullOrWhiteSpace(brokenMessage) ? "\u5b83\u597d\u50cf\u5df2\u7ecf\u574f\u4e86\u3002" : brokenMessage;
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

    private void HideHint()
    {
        if (_hintText != null)
        {
            _hintText.gameObject.SetActive(false);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = leverMode == LeverMode.Broken
            ? new Color(1f, 0.45f, 0.2f, 0.8f)
            : new Color(0.2f, 0.85f, 1f, 0.8f);

        var previousMatrix = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(
            transform.TransformPoint(interactionBoxOffset),
            transform.rotation,
            new Vector3(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y), 1f));
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(interactionBoxSize.x, interactionBoxSize.y, 0f));
        Gizmos.matrix = previousMatrix;
    }
}
