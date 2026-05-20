using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public class MirrorPlatformPlacementSync : MonoBehaviour
{
    [Header("Mirror Axis")]
    [SerializeField] private float localMirrorCenterX;

    [Header("Platforms")]
    [SerializeField] private Transform leftPlatform;
    [SerializeField] private Transform rightPlatform;

    [Header("Behavior")]
    [SerializeField] private bool syncWhilePlaying;
    [SerializeField] private float changeEpsilon = 0.0001f;

    [SerializeField, HideInInspector] private Vector3 lastLeftPlatformLocalPosition;
    [SerializeField, HideInInspector] private Vector3 lastRightPlatformLocalPosition;

    private bool _initialized;

    private void Reset()
    {
        CacheReferences();
        CaptureCurrentState();
    }

    private void OnEnable()
    {
        CacheReferences();
        CaptureCurrentState();
    }

    private void OnValidate()
    {
        CacheReferences();
        if (!_initialized)
        {
            CaptureCurrentState();
        }
    }

    private void Update()
    {
        if (Application.isPlaying && !syncWhilePlaying)
        {
            return;
        }

        CacheReferences();
        if (!_initialized)
        {
            CaptureCurrentState();
            return;
        }

        SyncPair();
    }

    private void SyncPair()
    {
        if (leftPlatform == null || rightPlatform == null)
        {
            return;
        }

        var leftLocal = leftPlatform.localPosition;
        var rightLocal = rightPlatform.localPosition;
        var leftChanged = HasMoved(leftLocal, lastLeftPlatformLocalPosition);
        var rightChanged = HasMoved(rightLocal, lastRightPlatformLocalPosition);

        if (!leftChanged && !rightChanged)
        {
            return;
        }

        if (leftChanged && !rightChanged)
        {
            rightPlatform.localPosition = MirrorPosition(leftLocal);
        }
        else if (rightChanged && !leftChanged)
        {
            leftPlatform.localPosition = MirrorPosition(rightLocal);
        }
        else
        {
            var leftDelta = (leftLocal - lastLeftPlatformLocalPosition).sqrMagnitude;
            var rightDelta = (rightLocal - lastRightPlatformLocalPosition).sqrMagnitude;
            if (leftDelta >= rightDelta)
            {
                rightPlatform.localPosition = MirrorPosition(leftLocal);
            }
            else
            {
                leftPlatform.localPosition = MirrorPosition(rightLocal);
            }
        }

        lastLeftPlatformLocalPosition = leftPlatform.localPosition;
        lastRightPlatformLocalPosition = rightPlatform.localPosition;
    }

    private Vector3 MirrorPosition(Vector3 sourceLocalPosition)
    {
        return new Vector3((localMirrorCenterX * 2f) - sourceLocalPosition.x, sourceLocalPosition.y, sourceLocalPosition.z);
    }

    private bool HasMoved(Vector3 current, Vector3 previous)
    {
        return (current - previous).sqrMagnitude > (changeEpsilon * changeEpsilon);
    }

    private void CacheReferences()
    {
        if (leftPlatform == null)
        {
            var child = transform.Find("LeftPlatform");
            if (child != null)
            {
                leftPlatform = child;
            }
        }

        if (rightPlatform == null)
        {
            var child = transform.Find("RightPlatform");
            if (child != null)
            {
                rightPlatform = child;
            }
        }
    }

    private void CaptureCurrentState()
    {
        lastLeftPlatformLocalPosition = leftPlatform != null ? leftPlatform.localPosition : Vector3.zero;
        lastRightPlatformLocalPosition = rightPlatform != null ? rightPlatform.localPosition : Vector3.zero;
        _initialized = true;
    }

    private void OnDrawGizmosSelected()
    {
        var worldCenter = transform.TransformPoint(new Vector3(localMirrorCenterX, 0f, 0f));
        Gizmos.color = new Color(0.4f, 1f, 0.4f, 0.9f);
        Gizmos.DrawLine(
            new Vector3(worldCenter.x, worldCenter.y - 20f, worldCenter.z),
            new Vector3(worldCenter.x, worldCenter.y + 20f, worldCenter.z));
    }
}
