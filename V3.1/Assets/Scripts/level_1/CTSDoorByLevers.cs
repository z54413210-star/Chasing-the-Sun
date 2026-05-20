using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("Chase The Sun/Level 1/CTS Door By Levers")]
[DisallowMultipleComponent]
public class CTSDoorByLevers : MonoBehaviour
{
    [Header("Door Visuals")]
    [SerializeField] private SpriteRenderer doorSpriteRenderer;
    [SerializeField] private Sprite closedSprite;
    [SerializeField] private Sprite openSprite;

    [Header("Blocking")]
    [SerializeField] private Collider2D[] blockingColliders = new Collider2D[0];

    [Header("Controlled By")]
    [SerializeField] private CTSLeverInteractable[] controllingLevers = new CTSLeverInteractable[0];

    private readonly List<CTSLeverInteractable> _validLevers = new List<CTSLeverInteractable>();

    private bool _isOpen;

    public bool IsOpen => _isOpen;

    private void Awake()
    {
        CacheReferences();
        ApplyDoorState(false);
    }

    private void OnEnable()
    {
        CacheReferences();
        RebuildLeverCache(logWarnings: true);
        SubscribeToLevers();
        RefreshDoorState();
    }

    private void OnDisable()
    {
        UnsubscribeFromLevers();
    }

    private void Reset()
    {
        CacheReferences();

        if (blockingColliders == null || blockingColliders.Length == 0)
        {
            blockingColliders = GetComponents<Collider2D>();
        }
    }

    private void OnValidate()
    {
        CacheReferences();

        if (blockingColliders == null)
        {
            blockingColliders = new Collider2D[0];
        }

        if (controllingLevers == null)
        {
            controllingLevers = new CTSLeverInteractable[0];
        }
    }

    private void CacheReferences()
    {
        if (doorSpriteRenderer == null)
        {
            doorSpriteRenderer = GetComponent<SpriteRenderer>();
        }
    }

    private void RebuildLeverCache(bool logWarnings)
    {
        _validLevers.Clear();

        var ignoredBrokenLever = false;
        var ignoredDuplicateLever = false;

        if (controllingLevers != null)
        {
            for (var i = 0; i < controllingLevers.Length; i++)
            {
                var lever = controllingLevers[i];
                if (lever == null)
                {
                    continue;
                }

                if (_validLevers.Contains(lever))
                {
                    ignoredDuplicateLever = true;
                    continue;
                }

                if (!lever.IsFunctional)
                {
                    ignoredBrokenLever = true;
                    continue;
                }

                _validLevers.Add(lever);
            }
        }

        if (!logWarnings)
        {
            return;
        }

        if (ignoredBrokenLever)
        {
            Debug.LogWarning($"{name}: broken levers were assigned and ignored. Doors only respond to functional levers.", this);
        }

        if (ignoredDuplicateLever)
        {
            Debug.LogWarning($"{name}: duplicate levers were assigned and ignored.", this);
        }

        if (_validLevers.Count == 0)
        {
            Debug.LogWarning($"{name}: no valid functional levers were assigned. The door will stay closed.", this);
        }

        if (doorSpriteRenderer == null)
        {
            Debug.LogWarning($"{name}: no SpriteRenderer was assigned. The door can still block/unblock, but sprites will not swap.", this);
        }
    }

    private void SubscribeToLevers()
    {
        for (var i = 0; i < _validLevers.Count; i++)
        {
            _validLevers[i].ActivatedStateChanged -= HandleLeverActivatedStateChanged;
            _validLevers[i].ActivatedStateChanged += HandleLeverActivatedStateChanged;
        }
    }

    private void UnsubscribeFromLevers()
    {
        for (var i = 0; i < _validLevers.Count; i++)
        {
            if (_validLevers[i] != null)
            {
                _validLevers[i].ActivatedStateChanged -= HandleLeverActivatedStateChanged;
            }
        }
    }

    private void HandleLeverActivatedStateChanged(CTSLeverInteractable _)
    {
        RefreshDoorState();
    }

    private void RefreshDoorState()
    {
        ApplyDoorState(ShouldDoorBeOpen());
    }

    private bool ShouldDoorBeOpen()
    {
        if (_validLevers.Count == 0)
        {
            return false;
        }

        for (var i = 0; i < _validLevers.Count; i++)
        {
            var lever = _validLevers[i];
            if (lever == null || !lever.IsActivated)
            {
                return false;
            }
        }

        return true;
    }

    private void ApplyDoorState(bool shouldOpen)
    {
        _isOpen = shouldOpen;

        if (doorSpriteRenderer != null)
        {
            var targetSprite = _isOpen ? openSprite : closedSprite;
            if (targetSprite != null)
            {
                doorSpriteRenderer.sprite = targetSprite;
            }
        }

        if (blockingColliders == null)
        {
            return;
        }

        for (var i = 0; i < blockingColliders.Length; i++)
        {
            if (blockingColliders[i] != null)
            {
                blockingColliders[i].enabled = !_isOpen;
            }
        }
    }
}
