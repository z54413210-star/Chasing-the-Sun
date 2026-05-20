using UnityEngine;

[DisallowMultipleComponent]
public class TargetPlatformActiveController : MonoBehaviour
{
    [SerializeField] private PressurePlateTrigger sourcePlate;
    [SerializeField] private GameObject targetRoot;
    [SerializeField] private bool defaultActive;
    [SerializeField] private bool pressedActive = true;

    private bool _subscribed;

    private void Awake()
    {
        if (targetRoot == null)
        {
            targetRoot = gameObject;
        }

        AttachListener();
        ApplyState(sourcePlate != null ? sourcePlate.IsPressed : false);
    }

    private void OnEnable()
    {
        AttachListener();
        ApplyState(sourcePlate != null ? sourcePlate.IsPressed : false);
    }

    private void OnDestroy()
    {
        if (sourcePlate != null && _subscribed)
        {
            sourcePlate.StateChanged -= HandlePlateStateChanged;
            _subscribed = false;
        }
    }

    private void HandlePlateStateChanged(bool isPressed)
    {
        ApplyState(isPressed);
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

    private void ApplyState(bool isPressed)
    {
        if (targetRoot == null)
        {
            return;
        }

        targetRoot.SetActive(isPressed ? pressedActive : defaultActive);
    }
}
