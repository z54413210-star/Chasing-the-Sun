using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public class MirrorGhostPlacementSync : MonoBehaviour
{
    [Header("Mirror Axis")]
    [SerializeField] private float localMirrorCenterX;

    [Header("Ghosts")]
    [SerializeField] private Transform leftGhost;
    [SerializeField] private Transform rightGhost;

    [Header("Optional Sensors")]
    [SerializeField] private bool mirrorSensors = true;
    [SerializeField] private Transform leftSensor;
    [SerializeField] private Transform rightSensor;

    [Header("Behavior")]
    [SerializeField] private bool syncWhilePlaying;
    [SerializeField] private float changeEpsilon = 0.0001f;

    [SerializeField, HideInInspector] private Vector3 lastLeftGhostLocalPosition;
    [SerializeField, HideInInspector] private Vector3 lastRightGhostLocalPosition;
    [SerializeField, HideInInspector] private Vector3 lastLeftSensorLocalPosition;
    [SerializeField, HideInInspector] private Vector3 lastRightSensorLocalPosition;

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

        SyncPair(leftGhost, rightGhost, ref lastLeftGhostLocalPosition, ref lastRightGhostLocalPosition);

        if (mirrorSensors)
        {
            SyncPair(leftSensor, rightSensor, ref lastLeftSensorLocalPosition, ref lastRightSensorLocalPosition);
        }
        else
        {
            CaptureSensorState();
        }
    }

    private void SyncPair(
        Transform left,
        Transform right,
        ref Vector3 lastLeftLocalPosition,
        ref Vector3 lastRightLocalPosition)
    {
        if (left == null || right == null)
        {
            return;
        }

        var leftLocal = left.localPosition;
        var rightLocal = right.localPosition;
        var leftChanged = HasMoved(leftLocal, lastLeftLocalPosition);
        var rightChanged = HasMoved(rightLocal, lastRightLocalPosition);

        if (!leftChanged && !rightChanged)
        {
            return;
        }

        if (leftChanged && !rightChanged)
        {
            right.localPosition = MirrorPosition(leftLocal);
        }
        else if (rightChanged && !leftChanged)
        {
            left.localPosition = MirrorPosition(rightLocal);
        }
        else
        {
            var leftDelta = (leftLocal - lastLeftLocalPosition).sqrMagnitude;
            var rightDelta = (rightLocal - lastRightLocalPosition).sqrMagnitude;
            if (leftDelta >= rightDelta)
            {
                right.localPosition = MirrorPosition(leftLocal);
            }
            else
            {
                left.localPosition = MirrorPosition(rightLocal);
            }
        }

        lastLeftLocalPosition = left.localPosition;
        lastRightLocalPosition = right.localPosition;
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
        if (leftGhost == null)
        {
            var child = transform.Find("LeftGhost");
            if (child != null)
            {
                leftGhost = child;
            }
        }

        if (rightGhost == null)
        {
            var child = transform.Find("RightGhost");
            if (child != null)
            {
                rightGhost = child;
            }
        }

        if (leftSensor == null)
        {
            var child = transform.Find("LeftSensor");
            if (child != null)
            {
                leftSensor = child;
            }
        }

        if (rightSensor == null)
        {
            var child = transform.Find("RightSensor");
            if (child != null)
            {
                rightSensor = child;
            }
        }
    }

    private void CaptureCurrentState()
    {
        lastLeftGhostLocalPosition = leftGhost != null ? leftGhost.localPosition : Vector3.zero;
        lastRightGhostLocalPosition = rightGhost != null ? rightGhost.localPosition : Vector3.zero;
        CaptureSensorState();
        _initialized = true;
    }

    private void CaptureSensorState()
    {
        lastLeftSensorLocalPosition = leftSensor != null ? leftSensor.localPosition : Vector3.zero;
        lastRightSensorLocalPosition = rightSensor != null ? rightSensor.localPosition : Vector3.zero;
    }

    private void OnDrawGizmosSelected()
    {
        var worldCenter = transform.TransformPoint(new Vector3(localMirrorCenterX, 0f, 0f));
        Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.9f);
        Gizmos.DrawLine(
            new Vector3(worldCenter.x, worldCenter.y - 20f, worldCenter.z),
            new Vector3(worldCenter.x, worldCenter.y + 20f, worldCenter.z));
    }
}
