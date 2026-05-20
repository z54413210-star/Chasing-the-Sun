using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class DelayedPressurePlatePlatformController : MonoBehaviour
{
    [SerializeField] private PressurePlateTrigger sourcePlate;
    [SerializeField] private GameObject targetRoot;
    [SerializeField] private float fadeInDurationSeconds = 0.2f;
    [SerializeField] private float fadeOutDurationSeconds = 0.35f;

    private bool _subscribed;
    private Coroutine _transitionCoroutine;
    private SpriteRenderer[] _spriteRenderers;
    private float[] _baseAlphas;

    private void Awake()
    {
        if (targetRoot == null)
        {
            targetRoot = gameObject;
        }

        CacheTargetParts();
        AttachListener();
        ApplyInitialState();
    }

    private void OnEnable()
    {
        CacheTargetParts();
        AttachListener();
    }

    private void OnDestroy()
    {
        CancelTransition();
        if (sourcePlate != null && _subscribed)
        {
            sourcePlate.StateChanged -= HandlePlateStateChanged;
            _subscribed = false;
        }
    }

    private void HandlePlateStateChanged(bool isPressed)
    {
        CancelTransition();

        if (isPressed)
        {
            _transitionCoroutine = StartManagedCoroutine(FadeInRoutine());
            return;
        }

        _transitionCoroutine = StartManagedCoroutine(FadeOutRoutine());
    }

    private IEnumerator FadeInRoutine()
    {
        if (targetRoot == null)
        {
            yield break;
        }

        if (!targetRoot.activeSelf)
        {
            targetRoot.SetActive(true);
        }

        CacheTargetParts();
        SetAlphaMultiplier(0f);

        if (fadeInDurationSeconds <= 0f)
        {
            SetAlphaMultiplier(1f);
            _transitionCoroutine = null;
            yield break;
        }

        var elapsed = 0f;
        while (elapsed < fadeInDurationSeconds)
        {
            elapsed += Time.deltaTime;
            SetAlphaMultiplier(Mathf.Clamp01(elapsed / fadeInDurationSeconds));
            yield return null;
        }

        SetAlphaMultiplier(1f);
        _transitionCoroutine = null;
    }

    private IEnumerator FadeOutRoutine()
    {
        if (targetRoot == null)
        {
            yield break;
        }

        if (!targetRoot.activeSelf)
        {
            _transitionCoroutine = null;
            yield break;
        }

        CacheTargetParts();

        if (fadeOutDurationSeconds <= 0f)
        {
            SetAlphaMultiplier(0f);
            targetRoot.SetActive(false);
            _transitionCoroutine = null;
            yield break;
        }

        var elapsed = 0f;
        while (elapsed < fadeOutDurationSeconds)
        {
            elapsed += Time.deltaTime;
            SetAlphaMultiplier(1f - Mathf.Clamp01(elapsed / fadeOutDurationSeconds));
            yield return null;
        }

        SetAlphaMultiplier(0f);
        targetRoot.SetActive(false);
        _transitionCoroutine = null;
    }

    private void ApplyInitialState()
    {
        if (targetRoot == null)
        {
            return;
        }

        var isPressed = sourcePlate != null && sourcePlate.IsPressed;
        if (isPressed)
        {
            if (!targetRoot.activeSelf)
            {
                targetRoot.SetActive(true);
            }

            CacheTargetParts();
            SetAlphaMultiplier(1f);
            return;
        }

        CacheTargetParts();
        SetAlphaMultiplier(0f);
        targetRoot.SetActive(false);
    }

    private void CacheTargetParts()
    {
        if (targetRoot == null)
        {
            return;
        }

        _spriteRenderers = targetRoot.GetComponentsInChildren<SpriteRenderer>(true);
        if (_baseAlphas != null && _baseAlphas.Length == _spriteRenderers.Length)
        {
            return;
        }

        _baseAlphas = new float[_spriteRenderers.Length];
        for (var i = 0; i < _spriteRenderers.Length; i++)
        {
            _baseAlphas[i] = _spriteRenderers[i] != null ? _spriteRenderers[i].color.a : 1f;
        }
    }

    private void SetAlphaMultiplier(float alphaMultiplier)
    {
        if (_spriteRenderers == null || _baseAlphas == null)
        {
            return;
        }

        for (var i = 0; i < _spriteRenderers.Length; i++)
        {
            var spriteRenderer = _spriteRenderers[i];
            if (spriteRenderer == null)
            {
                continue;
            }

            var color = spriteRenderer.color;
            color.a = _baseAlphas[i] * alphaMultiplier;
            spriteRenderer.color = color;
        }
    }

    private void AttachListener()
    {
        if (sourcePlate == null || _subscribed)
        {
            return;
        }

        sourcePlate.StateChanged += HandlePlateStateChanged;
        _subscribed = true;
    }

    private void CancelTransition()
    {
        if (_transitionCoroutine == null)
        {
            return;
        }

        StopManagedCoroutine(_transitionCoroutine);
        _transitionCoroutine = null;
    }

    private Coroutine StartManagedCoroutine(IEnumerator routine)
    {
        if (sourcePlate != null)
        {
            return sourcePlate.StartCoroutine(routine);
        }

        return StartCoroutine(routine);
    }

    private void StopManagedCoroutine(Coroutine routine)
    {
        if (routine == null)
        {
            return;
        }

        if (sourcePlate != null)
        {
            sourcePlate.StopCoroutine(routine);
            return;
        }

        StopCoroutine(routine);
    }
}
