using UnityEngine;

[RequireComponent(typeof(Camera))]
[DisallowMultipleComponent]
[DefaultExecutionOrder(10000)]
public class CameraZoomBoundsSafe2D : MonoBehaviour
{
    [SerializeField] private KeyCode zoomOutKey = KeyCode.J;
    [SerializeField] private KeyCode zoomInKey = KeyCode.K;
    [SerializeField] private float minOrthographicSize = 4f;
    [SerializeField] private float maxOrthographicSize = 12f;
    [SerializeField] private float zoomSpeed = 8f;
    [SerializeField] private CameraBounds2D cameraBounds;

    private Camera _cameraComponent;

    private void Awake()
    {
        CacheComponents();
        ClampSettings();
        ClampCurrentSize();
        ClampCurrentPosition();
    }

    private void OnValidate()
    {
        CacheComponents();
        ClampSettings();
        ClampCurrentSize();
        ClampCurrentPosition();
    }

    private void Update()
    {
        if (_cameraComponent == null || !_cameraComponent.orthographic)
        {
            return;
        }

        float zoomDelta = 0f;
        if (Input.GetKey(zoomOutKey))
        {
            zoomDelta += 1f;
        }

        if (Input.GetKey(zoomInKey))
        {
            zoomDelta -= 1f;
        }

        if (Mathf.Approximately(zoomDelta, 0f))
        {
            return;
        }

        GetEffectiveSizeLimits(out var effectiveMinSize, out var effectiveMaxSize);
        _cameraComponent.orthographicSize = Mathf.Clamp(
            _cameraComponent.orthographicSize + (zoomDelta * zoomSpeed * Time.deltaTime),
            effectiveMinSize,
            effectiveMaxSize);
    }

    private void LateUpdate()
    {
        ClampCurrentPosition();
    }

    private void CacheComponents()
    {
        if (_cameraComponent == null)
        {
            _cameraComponent = GetComponent<Camera>();
        }
    }

    private void ClampSettings()
    {
        minOrthographicSize = Mathf.Max(0.01f, minOrthographicSize);
        maxOrthographicSize = Mathf.Max(minOrthographicSize, maxOrthographicSize);
        zoomSpeed = Mathf.Max(0f, zoomSpeed);
    }

    private void ClampCurrentSize()
    {
        if (_cameraComponent == null || !_cameraComponent.orthographic)
        {
            return;
        }

        GetEffectiveSizeLimits(out var effectiveMinSize, out var effectiveMaxSize);
        _cameraComponent.orthographicSize = Mathf.Clamp(
            _cameraComponent.orthographicSize,
            effectiveMinSize,
            effectiveMaxSize);
    }

    private void ClampCurrentPosition()
    {
        if (_cameraComponent == null || !_cameraComponent.orthographic || cameraBounds == null)
        {
            return;
        }

        transform.position = cameraBounds.ClampPosition(_cameraComponent, transform.position);
    }

    private void GetEffectiveSizeLimits(out float effectiveMinSize, out float effectiveMaxSize)
    {
        effectiveMaxSize = maxOrthographicSize;

        if (_cameraComponent != null && _cameraComponent.orthographic && cameraBounds != null)
        {
            var boundsCollider = cameraBounds.GetComponent<BoxCollider2D>();
            if (boundsCollider != null)
            {
                var bounds = boundsCollider.bounds;
                var verticalLimit = bounds.size.y * 0.5f;
                var horizontalLimit = bounds.size.x * 0.5f / Mathf.Max(_cameraComponent.aspect, 0.0001f);
                var boundsLimitedMaxSize = Mathf.Min(verticalLimit, horizontalLimit);

                if (boundsLimitedMaxSize > 0f)
                {
                    effectiveMaxSize = Mathf.Min(effectiveMaxSize, boundsLimitedMaxSize);
                }
            }
        }

        effectiveMaxSize = Mathf.Max(0.01f, effectiveMaxSize);
        effectiveMinSize = Mathf.Min(minOrthographicSize, effectiveMaxSize);
    }
}
