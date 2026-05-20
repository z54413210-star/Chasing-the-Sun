using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerTwoController2D))]
[RequireComponent(typeof(CapsuleCollider2D))]
public class PlayerTwoLadderTraversalAssist : MonoBehaviour
{
    [SerializeField] private PlayerTwoController2D playerController;
    [SerializeField] private CapsuleCollider2D bodyCollider;
    [SerializeField] private LayerMask climbBlockingLayers;
    [SerializeField] private float verticalProbePadding = 0.35f;
    [SerializeField] private float horizontalProbePadding = 0.04f;

    private readonly List<Collider2D> _ignoredColliders = new List<Collider2D>();
    private readonly Collider2D[] _overlapResults = new Collider2D[24];

    private void Awake()
    {
        CacheReferences();
        ConfigureDefaults();
    }

    private void OnValidate()
    {
        CacheReferences();
        ConfigureDefaults();
    }

    private void OnDisable()
    {
        RestoreIgnoredCollisions();
    }

    public void RestoreImmediately()
    {
        RestoreIgnoredCollisions();
    }

    private void FixedUpdate()
    {
        CacheReferences();
        if (playerController == null || bodyCollider == null)
        {
            return;
        }

        if (!playerController.IsClimbing)
        {
            RestoreIgnoredCollisions();
            return;
        }

        IgnoreBlockingCollidersWhileClimbing();
    }

    private void IgnoreBlockingCollidersWhileClimbing()
    {
        var bounds = bodyCollider.bounds;
        var probeCenter = bounds.center;
        var probeSize = new Vector2(
            bounds.size.x + horizontalProbePadding,
            bounds.size.y + (verticalProbePadding * 2f));

        var hitCount = Physics2D.OverlapBoxNonAlloc(probeCenter, probeSize, 0f, _overlapResults, climbBlockingLayers);
        for (var i = 0; i < hitCount; i++)
        {
            var hitCollider = _overlapResults[i];
            if (hitCollider == null || hitCollider == bodyCollider || hitCollider.isTrigger)
            {
                continue;
            }

            IgnoreCollider(hitCollider);
        }
    }

    private void IgnoreCollider(Collider2D targetCollider)
    {
        if (targetCollider == null || _ignoredColliders.Contains(targetCollider))
        {
            return;
        }

        Physics2D.IgnoreCollision(bodyCollider, targetCollider, true);
        _ignoredColliders.Add(targetCollider);
    }

    private void RestoreIgnoredCollisions()
    {
        if (bodyCollider == null)
        {
            _ignoredColliders.Clear();
            return;
        }

        for (var i = 0; i < _ignoredColliders.Count; i++)
        {
            var targetCollider = _ignoredColliders[i];
            if (targetCollider != null)
            {
                Physics2D.IgnoreCollision(bodyCollider, targetCollider, false);
            }
        }

        _ignoredColliders.Clear();
    }

    private void CacheReferences()
    {
        if (playerController == null)
        {
            playerController = GetComponent<PlayerTwoController2D>();
        }

        if (bodyCollider == null)
        {
            bodyCollider = GetComponent<CapsuleCollider2D>();
        }
    }

    private void ConfigureDefaults()
    {
        if (climbBlockingLayers == 0)
        {
            climbBlockingLayers = ChaseTheSunProjectSettings.MaskFromNames(
                ChaseTheSunProjectSettings.GroundLayer,
                ChaseTheSunProjectSettings.PushableLayer);
        }
    }
}

