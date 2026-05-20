using System.Reflection;
using UnityEngine;

[DisallowMultipleComponent]
public class Level3CameraBoundsBinder : MonoBehaviour
{
    [SerializeField] private CameraFollow2D cameraFollow;
    [SerializeField] private CameraBounds2D assignedBounds;
    [SerializeField] private bool forceUseBounds = true;

    private static readonly FieldInfo CameraBoundsField = typeof(CameraFollow2D).GetField(
        "cameraBounds",
        BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly FieldInfo UseBoundsField = typeof(CameraFollow2D).GetField(
        "useBounds",
        BindingFlags.Instance | BindingFlags.NonPublic);

    private void Reset()
    {
        CacheComponents();
    }

    private void Awake()
    {
        ApplyBinding();
    }

    private void OnEnable()
    {
        ApplyBinding();
    }

    private void OnValidate()
    {
        CacheComponents();
        ApplyBinding();
    }

    [ContextMenu("Apply Binding")]
    private void ApplyBindingFromContextMenu()
    {
        ApplyBinding();
    }

    private void CacheComponents()
    {
        if (cameraFollow == null)
        {
            cameraFollow = GetComponent<CameraFollow2D>();
        }
    }

    private void ApplyBinding()
    {
        CacheComponents();

        if (cameraFollow == null || assignedBounds == null || CameraBoundsField == null)
        {
            return;
        }

        CameraBoundsField.SetValue(cameraFollow, assignedBounds);

        if (forceUseBounds && UseBoundsField != null)
        {
            UseBoundsField.SetValue(cameraFollow, true);
        }
    }
}
