using System.Reflection;
using UnityEngine;

[DisallowMultipleComponent]
public class Level3MirrorGhostRespawnReset : MonoBehaviour, ILevel3TeamRespawnResettable
{
    [SerializeField] private MirrorGhostPairController pairController;
    [SerializeField] private Transform leftGhost;
    [SerializeField] private Transform rightGhost;
    [SerializeField] private Transform leftSensor;
    [SerializeField] private Transform rightSensor;
    [SerializeField] private GhostPlatformSensor leftGhostSensor;
    [SerializeField] private GhostPlatformSensor rightGhostSensor;

    private static readonly BindingFlags PrivateInstanceFlags = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly FieldInfo OccupantsField = typeof(GhostPlatformSensor).GetField("_occupants", PrivateInstanceFlags);
    private static readonly FieldInfo LastLeadingSideField = typeof(MirrorGhostPairController).GetField("_lastLeadingSide", PrivateInstanceFlags);

    private Vector3 _initialLeftGhostLocalPosition;
    private Vector3 _initialRightGhostLocalPosition;
    private Vector3 _initialLeftSensorLocalPosition;
    private Vector3 _initialRightSensorLocalPosition;
    private Level3Side _initialLeadingSide = Level3Side.Left;
    private bool _hasCapturedInitialState;

    private void Awake()
    {
        CacheReferences();
        CaptureInitialState();
    }

    private void OnEnable()
    {
        CaptureInitialState();
    }

    private void OnValidate()
    {
        CacheReferences();
    }

    public void RestoreForTeamRespawn()
    {
        CacheReferences();
        CaptureInitialState();

        RestoreLocalPosition(leftGhost, _initialLeftGhostLocalPosition);
        RestoreLocalPosition(rightGhost, _initialRightGhostLocalPosition);
        RestoreLocalPosition(leftSensor, _initialLeftSensorLocalPosition);
        RestoreLocalPosition(rightSensor, _initialRightSensorLocalPosition);

        ClearSensorOccupants(leftGhostSensor);
        ClearSensorOccupants(rightGhostSensor);

        if (pairController != null && LastLeadingSideField != null)
        {
            LastLeadingSideField.SetValue(pairController, _initialLeadingSide);
        }

        Physics2D.SyncTransforms();
    }

    private void CacheReferences()
    {
        if (pairController == null)
        {
            pairController = GetComponent<MirrorGhostPairController>();
        }

        if (leftGhost == null)
        {
            leftGhost = FindChildByName("LeftGhost");
        }

        if (rightGhost == null)
        {
            rightGhost = FindChildByName("RightGhost");
        }

        if (leftSensor == null)
        {
            leftSensor = FindChildByName("LeftSensor");
        }

        if (rightSensor == null)
        {
            rightSensor = FindChildByName("RightSensor");
        }

        if (leftGhostSensor == null && leftSensor != null)
        {
            leftGhostSensor = leftSensor.GetComponent<GhostPlatformSensor>();
        }

        if (rightGhostSensor == null && rightSensor != null)
        {
            rightGhostSensor = rightSensor.GetComponent<GhostPlatformSensor>();
        }
    }

    private void CaptureInitialState()
    {
        if (_hasCapturedInitialState)
        {
            return;
        }

        _initialLeftGhostLocalPosition = leftGhost != null ? leftGhost.localPosition : Vector3.zero;
        _initialRightGhostLocalPosition = rightGhost != null ? rightGhost.localPosition : Vector3.zero;
        _initialLeftSensorLocalPosition = leftSensor != null ? leftSensor.localPosition : Vector3.zero;
        _initialRightSensorLocalPosition = rightSensor != null ? rightSensor.localPosition : Vector3.zero;
        _initialLeadingSide = ReadCurrentLeadingSide();
        _hasCapturedInitialState = true;
    }

    private Level3Side ReadCurrentLeadingSide()
    {
        if (pairController == null || LastLeadingSideField == null)
        {
            return Level3Side.Left;
        }

        var value = LastLeadingSideField.GetValue(pairController);
        return value is Level3Side side ? side : Level3Side.Left;
    }

    private Transform FindChildByName(string childName)
    {
        var transforms = GetComponentsInChildren<Transform>(true);
        for (var i = 0; i < transforms.Length; i++)
        {
            if (transforms[i] != null && transforms[i].name == childName)
            {
                return transforms[i];
            }
        }

        return null;
    }

    private static void RestoreLocalPosition(Transform target, Vector3 localPosition)
    {
        if (target != null)
        {
            target.localPosition = localPosition;
        }
    }

    private static void ClearSensorOccupants(GhostPlatformSensor sensor)
    {
        if (sensor == null || OccupantsField == null)
        {
            return;
        }

        var occupants = OccupantsField.GetValue(sensor);
        if (occupants == null)
        {
            return;
        }

        var clearMethod = occupants.GetType().GetMethod("Clear", BindingFlags.Instance | BindingFlags.Public);
        if (clearMethod != null)
        {
            clearMethod.Invoke(occupants, null);
        }
    }
}
