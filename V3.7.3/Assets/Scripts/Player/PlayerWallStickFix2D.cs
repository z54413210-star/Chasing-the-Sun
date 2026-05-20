using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CapsuleCollider2D))]
public class PlayerWallStickFix2D : MonoBehaviour
{
    [SerializeField] private CapsuleCollider2D bodyCollider;
    [SerializeField] private float friction = 0f;
    [SerializeField] private float bounciness = 0f;
    [SerializeField] private bool onlyOverrideWhenNoCustomMaterial = true;

    private PhysicsMaterial2D _originalMaterial;
    private PhysicsMaterial2D _runtimeMaterial;
    private bool _hasAppliedOverride;

    private void Awake()
    {
        CacheReferences();
        ClampSettings();
        ApplyOverride();
    }

    private void OnEnable()
    {
        ApplyOverride();
    }

    private void OnDisable()
    {
        RestoreOriginalMaterial();
    }

    private void OnDestroy()
    {
        CleanupRuntimeMaterial();
    }

    private void OnValidate()
    {
        CacheReferences();
        ClampSettings();

        if (Application.isPlaying)
        {
            ApplyOverride();
        }
    }

    private void ApplyOverride()
    {
        if (bodyCollider == null)
        {
            return;
        }

        if (!_hasAppliedOverride)
        {
            if (onlyOverrideWhenNoCustomMaterial && bodyCollider.sharedMaterial != null)
            {
                return;
            }

            _originalMaterial = bodyCollider.sharedMaterial;
            _hasAppliedOverride = true;
        }

        EnsureRuntimeMaterial();
        bodyCollider.sharedMaterial = _runtimeMaterial;
    }

    private void RestoreOriginalMaterial()
    {
        if (!_hasAppliedOverride || bodyCollider == null)
        {
            return;
        }

        bodyCollider.sharedMaterial = _originalMaterial;
    }

    private void EnsureRuntimeMaterial()
    {
        if (_runtimeMaterial == null)
        {
            _runtimeMaterial = new PhysicsMaterial2D("PlayerWallStickFix2D_Runtime");
            _runtimeMaterial.hideFlags = HideFlags.HideAndDontSave;
        }

        _runtimeMaterial.friction = friction;
        _runtimeMaterial.bounciness = bounciness;
    }

    private void CleanupRuntimeMaterial()
    {
        if (_runtimeMaterial == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(_runtimeMaterial);
        }
        else
        {
            DestroyImmediate(_runtimeMaterial);
        }

        _runtimeMaterial = null;
    }

    private void CacheReferences()
    {
        if (bodyCollider == null)
        {
            bodyCollider = GetComponent<CapsuleCollider2D>();
        }
    }

    private void ClampSettings()
    {
        friction = Mathf.Max(0f, friction);
        bounciness = Mathf.Clamp01(bounciness);
    }
}
