using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public class MirrorBoxPlacementSync : MonoBehaviour
{
    [Header("Mirror Axis")]
    [SerializeField] private float localMirrorCenterX;

    [Header("Boxes")]
    [SerializeField] private Transform leftBox;
    [SerializeField] private Transform rightBox;

    [Header("Behavior")]
    [SerializeField] private bool syncWhilePlaying;
    [SerializeField] private float changeEpsilon = 0.0001f;

    [SerializeField, HideInInspector] private Vector3 lastLeftBoxLocalPosition;
    [SerializeField, HideInInspector] private Vector3 lastRightBoxLocalPosition;

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
        if (leftBox == null || rightBox == null)
        {
            return;
        }

        var leftLocal = leftBox.localPosition;
        var rightLocal = rightBox.localPosition;
        var leftChanged = HasMoved(leftLocal, lastLeftBoxLocalPosition);
        var rightChanged = HasMoved(rightLocal, lastRightBoxLocalPosition);

        if (!leftChanged && !rightChanged)
        {
            return;
        }

        if (leftChanged && !rightChanged)
        {
            rightBox.localPosition = MirrorPosition(leftLocal);
        }
        else if (rightChanged && !leftChanged)
        {
            leftBox.localPosition = MirrorPosition(rightLocal);
        }
        else
        {
            var leftDelta = (leftLocal - lastLeftBoxLocalPosition).sqrMagnitude;
            var rightDelta = (rightLocal - lastRightBoxLocalPosition).sqrMagnitude;
            if (leftDelta >= rightDelta)
            {
                rightBox.localPosition = MirrorPosition(leftLocal);
            }
            else
            {
                leftBox.localPosition = MirrorPosition(rightLocal);
            }
        }

        lastLeftBoxLocalPosition = leftBox.localPosition;
        lastRightBoxLocalPosition = rightBox.localPosition;
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
        if (leftBox == null)
        {
            var child = transform.Find("LeftBox");
            if (child != null)
            {
                leftBox = child;
            }
        }

        if (rightBox == null)
        {
            var child = transform.Find("RightBox");
            if (child != null)
            {
                rightBox = child;
            }
        }
    }

    private void CaptureCurrentState()
    {
        lastLeftBoxLocalPosition = leftBox != null ? leftBox.localPosition : Vector3.zero;
        lastRightBoxLocalPosition = rightBox != null ? rightBox.localPosition : Vector3.zero;
        _initialized = true;
    }

    private void OnDrawGizmosSelected()
    {
        var worldCenter = transform.TransformPoint(new Vector3(localMirrorCenterX, 0f, 0f));
        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.9f);
        Gizmos.DrawLine(
            new Vector3(worldCenter.x, worldCenter.y - 20f, worldCenter.z),
            new Vector3(worldCenter.x, worldCenter.y + 20f, worldCenter.z));
    }
}
