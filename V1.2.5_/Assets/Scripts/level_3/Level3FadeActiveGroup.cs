using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class Level3FadeActiveGroup : MonoBehaviour
{
    [SerializeField] private float fadeInDurationSeconds = 0.25f;
    [SerializeField] private float fadeOutDurationSeconds = 0.25f;

    private SpriteRenderer[] _spriteRenderers = System.Array.Empty<SpriteRenderer>();
    private Collider2D[] _colliders = System.Array.Empty<Collider2D>();
    private float[] _baseAlphas = System.Array.Empty<float>();
    private Coroutine _transitionCoroutine;

    private void Awake()
    {
        CacheParts();
    }

    private void OnEnable()
    {
        CacheParts();
    }

    private void OnValidate()
    {
        CacheParts();
    }

    public void SetFadeDurations(float fadeInSeconds, float fadeOutSeconds)
    {
        fadeInDurationSeconds = Mathf.Max(0f, fadeInSeconds);
        fadeOutDurationSeconds = Mathf.Max(0f, fadeOutSeconds);
    }

    public void ShowImmediate()
    {
        CancelTransition();
        EnsureActive();
        CacheParts();
        SetAlphaMultiplier(1f);
        SetCollidersEnabled(true);
    }

    public void HideImmediate()
    {
        CancelTransition();
        CacheParts();
        SetAlphaMultiplier(0f);
        SetCollidersEnabled(false);
        gameObject.SetActive(false);
    }

    public void ShowWithFade()
    {
        CancelTransition();
        EnsureActive();
        CacheParts();
        SetCollidersEnabled(true);
        _transitionCoroutine = StartCoroutine(FadeRoutine(GetCurrentAlphaMultiplier(), 1f, fadeInDurationSeconds, disableAtEnd: false));
    }

    public void HideWithFadeThenDisable()
    {
        if (!gameObject.activeSelf)
        {
            return;
        }

        CancelTransition();
        CacheParts();
        _transitionCoroutine = StartCoroutine(FadeRoutine(GetCurrentAlphaMultiplier(), 0f, fadeOutDurationSeconds, disableAtEnd: true));
    }

    private IEnumerator FadeRoutine(float from, float to, float durationSeconds, bool disableAtEnd)
    {
        if (durationSeconds <= 0f)
        {
            SetAlphaMultiplier(to);
            if (disableAtEnd)
            {
                SetCollidersEnabled(false);
                gameObject.SetActive(false);
            }

            _transitionCoroutine = null;
            yield break;
        }

        var elapsed = 0f;
        while (elapsed < durationSeconds)
        {
            elapsed += Time.deltaTime;
            var t = Mathf.Clamp01(elapsed / durationSeconds);
            SetAlphaMultiplier(Mathf.Lerp(from, to, t));
            yield return null;
        }

        SetAlphaMultiplier(to);
        if (disableAtEnd)
        {
            SetCollidersEnabled(false);
            gameObject.SetActive(false);
        }

        _transitionCoroutine = null;
    }

    private void EnsureActive()
    {
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }
    }

    private void CacheParts()
    {
        _spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        _colliders = GetComponentsInChildren<Collider2D>(true);

        if (_baseAlphas.Length != _spriteRenderers.Length)
        {
            _baseAlphas = new float[_spriteRenderers.Length];
            for (var i = 0; i < _spriteRenderers.Length; i++)
            {
                var spriteRenderer = _spriteRenderers[i];
                _baseAlphas[i] = spriteRenderer != null ? spriteRenderer.color.a : 1f;
            }

            return;
        }

        for (var i = 0; i < _spriteRenderers.Length; i++)
        {
            var spriteRenderer = _spriteRenderers[i];
            if (spriteRenderer == null)
            {
                continue;
            }

            if (_baseAlphas[i] <= 0.0001f && spriteRenderer.color.a > 0.0001f)
            {
                _baseAlphas[i] = spriteRenderer.color.a;
            }
        }
    }

    private void SetCollidersEnabled(bool enabled)
    {
        for (var i = 0; i < _colliders.Length; i++)
        {
            if (_colliders[i] != null)
            {
                _colliders[i].enabled = enabled;
            }
        }
    }

    private float GetCurrentAlphaMultiplier()
    {
        if (_spriteRenderers.Length == 0 || _baseAlphas.Length == 0)
        {
            return 1f;
        }

        var spriteRenderer = _spriteRenderers[0];
        if (spriteRenderer == null)
        {
            return 1f;
        }

        var baseAlpha = _baseAlphas[0];
        if (baseAlpha <= 0.0001f)
        {
            return 1f;
        }

        return Mathf.Clamp01(spriteRenderer.color.a / baseAlpha);
    }

    private void SetAlphaMultiplier(float alphaMultiplier)
    {
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

    private void CancelTransition()
    {
        if (_transitionCoroutine == null)
        {
            return;
        }

        StopCoroutine(_transitionCoroutine);
        _transitionCoroutine = null;
    }
}
