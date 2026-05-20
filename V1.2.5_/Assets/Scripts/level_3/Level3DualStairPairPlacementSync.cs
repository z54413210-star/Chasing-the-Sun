using System;
using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public class Level3DualStairPairPlacementSync : MonoBehaviour
{
    [SerializeField] private float localMirrorCenterX;
    [SerializeField] private Transform leftStep;
    [SerializeField] private Transform rightStep;
    [SerializeField] private bool syncWhilePlaying;
    [SerializeField] private float changeEpsilon = 0.0001f;
    [SerializeField, HideInInspector] private Vector3 lastLeftLocalPosition;
    [SerializeField, HideInInspector] private Vector3 lastRightLocalPosition;

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
        if (leftStep == null || rightStep == null)
        {
            return;
        }

        var leftLocal = leftStep.localPosition;
        var rightLocal = rightStep.localPosition;
        var leftChanged = HasMoved(leftLocal, lastLeftLocalPosition);
        var rightChanged = HasMoved(rightLocal, lastRightLocalPosition);

        if (!leftChanged && !rightChanged)
        {
            return;
        }

        if (leftChanged && !rightChanged)
        {
            rightStep.localPosition = MirrorPosition(leftLocal);
        }
        else if (rightChanged && !leftChanged)
        {
            leftStep.localPosition = MirrorPosition(rightLocal);
        }
        else
        {
            var leftDelta = (leftLocal - lastLeftLocalPosition).sqrMagnitude;
            var rightDelta = (rightLocal - lastRightLocalPosition).sqrMagnitude;
            if (leftDelta >= rightDelta)
            {
                rightStep.localPosition = MirrorPosition(leftLocal);
            }
            else
            {
                leftStep.localPosition = MirrorPosition(rightLocal);
            }
        }

        lastLeftLocalPosition = leftStep.localPosition;
        lastRightLocalPosition = rightStep.localPosition;
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
        if (leftStep == null || rightStep == null)
        {
            foreach (Transform child in transform)
            {
                if (leftStep == null && child.name.StartsWith("LeftStep", StringComparison.Ordinal))
                {
                    leftStep = child;
                    continue;
                }

                if (rightStep == null && child.name.StartsWith("RightStep", StringComparison.Ordinal))
                {
                    rightStep = child;
                }
            }
        }
    }

    private void CaptureCurrentState()
    {
        lastLeftLocalPosition = leftStep != null ? leftStep.localPosition : Vector3.zero;
        lastRightLocalPosition = rightStep != null ? rightStep.localPosition : Vector3.zero;
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
