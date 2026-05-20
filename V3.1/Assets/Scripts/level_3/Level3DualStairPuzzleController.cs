using System;
using UnityEngine;

[DisallowMultipleComponent]
public class Level3DualStairPuzzleController : MonoBehaviour
{
    [Serializable]
    private class DualStairStepSlot
    {
        public Level3FadeActiveGroup leftRoot;
        public Level3FadeActiveGroup rightRoot;
        public PressurePlateTrigger leftPlate;
        public PressurePlateTrigger rightPlate;
    }

    [SerializeField] private bool useUnifiedFadeDurations = true;
    [SerializeField] private float unifiedFadeInDurationSeconds = 0.25f;
    [SerializeField] private float unifiedFadeOutDurationSeconds = 0.25f;
    [SerializeField] private DualStairStepSlot[] steps = Array.Empty<DualStairStepSlot>();

    private Action<bool>[] _leftHandlers = Array.Empty<Action<bool>>();
    private Action<bool>[] _rightHandlers = Array.Empty<Action<bool>>();
    private bool _listenersAttached;

    private void Awake()
    {
        ApplyUnifiedFadeDurations();

        if (!Application.isPlaying)
        {
            return;
        }

        InitializeState();
        AttachListeners();
    }

    private void OnValidate()
    {
        unifiedFadeInDurationSeconds = Mathf.Max(0f, unifiedFadeInDurationSeconds);
        unifiedFadeOutDurationSeconds = Mathf.Max(0f, unifiedFadeOutDurationSeconds);
        ApplyUnifiedFadeDurations();
    }

    private void OnDestroy()
    {
        DetachListeners();
    }

    private void InitializeState()
    {
        ApplyImmediateVisibility(GetLeftRoot(0), true);
        ApplyImmediateVisibility(GetRightRoot(0), true);

        ApplyImmediateVisibility(GetRightRoot(1), GetLeftPlatePressed(0));
        ApplyImmediateVisibility(GetLeftRoot(1), GetRightPlatePressed(1));
        ApplyImmediateVisibility(GetRightRoot(2), GetLeftPlatePressed(1));
        ApplyImmediateVisibility(GetLeftRoot(2), GetRightPlatePressed(2));
        ApplyImmediateVisibility(GetRightRoot(3), GetLeftPlatePressed(2));
        ApplyImmediateVisibility(GetLeftRoot(3), GetRightPlatePressed(3));
    }

    private void AttachListeners()
    {
        if (_listenersAttached)
        {
            return;
        }

        _leftHandlers = new Action<bool>[steps.Length];
        _rightHandlers = new Action<bool>[steps.Length];

        for (var i = 0; i < steps.Length; i++)
        {
            var capturedIndex = i;
            if (steps[i].leftPlate != null)
            {
                _leftHandlers[i] = isPressed => HandleLeftPlateStateChanged(capturedIndex, isPressed);
                steps[i].leftPlate.StateChanged += _leftHandlers[i];
            }

            if (steps[i].rightPlate != null)
            {
                _rightHandlers[i] = isPressed => HandleRightPlateStateChanged(capturedIndex, isPressed);
                steps[i].rightPlate.StateChanged += _rightHandlers[i];
            }
        }

        _listenersAttached = true;
    }

    private void DetachListeners()
    {
        if (!_listenersAttached)
        {
            return;
        }

        for (var i = 0; i < steps.Length; i++)
        {
            if (steps[i].leftPlate != null && _leftHandlers.Length > i && _leftHandlers[i] != null)
            {
                steps[i].leftPlate.StateChanged -= _leftHandlers[i];
            }

            if (steps[i].rightPlate != null && _rightHandlers.Length > i && _rightHandlers[i] != null)
            {
                steps[i].rightPlate.StateChanged -= _rightHandlers[i];
            }
        }

        _leftHandlers = Array.Empty<Action<bool>>();
        _rightHandlers = Array.Empty<Action<bool>>();
        _listenersAttached = false;
    }

    private void ApplyUnifiedFadeDurations()
    {
        if (!useUnifiedFadeDurations)
        {
            return;
        }

        for (var i = 0; i < steps.Length; i++)
        {
            ApplyFadeDuration(steps[i].leftRoot);
            ApplyFadeDuration(steps[i].rightRoot);
        }
    }

    private void ApplyFadeDuration(Level3FadeActiveGroup group)
    {
        if (group != null)
        {
            group.SetFadeDurations(unifiedFadeInDurationSeconds, unifiedFadeOutDurationSeconds);
        }
    }

    private void HandleLeftPlateStateChanged(int stepIndex, bool isPressed)
    {
        switch (stepIndex)
        {
            case 0:
                ApplyFadeVisibility(GetRightRoot(1), isPressed);
                break;
            case 1:
                ApplyFadeVisibility(GetRightRoot(2), isPressed);
                break;
            case 2:
                ApplyFadeVisibility(GetRightRoot(3), isPressed);
                break;
        }
    }

    private void HandleRightPlateStateChanged(int stepIndex, bool isPressed)
    {
        switch (stepIndex)
        {
            case 1:
                ApplyFadeVisibility(GetLeftRoot(1), isPressed);
                break;
            case 2:
                ApplyFadeVisibility(GetLeftRoot(2), isPressed);
                break;
            case 3:
                ApplyFadeVisibility(GetLeftRoot(3), isPressed);
                break;
        }
    }

    private Level3FadeActiveGroup GetLeftRoot(int stepIndex)
    {
        return IsValidStep(stepIndex) ? steps[stepIndex].leftRoot : null;
    }

    private Level3FadeActiveGroup GetRightRoot(int stepIndex)
    {
        return IsValidStep(stepIndex) ? steps[stepIndex].rightRoot : null;
    }

    private bool GetLeftPlatePressed(int stepIndex)
    {
        return IsValidStep(stepIndex) && steps[stepIndex].leftPlate != null && steps[stepIndex].leftPlate.IsPressed;
    }

    private bool GetRightPlatePressed(int stepIndex)
    {
        return IsValidStep(stepIndex) && steps[stepIndex].rightPlate != null && steps[stepIndex].rightPlate.IsPressed;
    }

    private bool IsValidStep(int stepIndex)
    {
        return stepIndex >= 0 && stepIndex < steps.Length;
    }

    private static void ApplyImmediateVisibility(Level3FadeActiveGroup group, bool visible)
    {
        if (group == null)
        {
            return;
        }

        if (visible)
        {
            group.ShowImmediate();
        }
        else
        {
            group.HideImmediate();
        }
    }

    private static void ApplyFadeVisibility(Level3FadeActiveGroup group, bool visible)
    {
        if (group == null)
        {
            return;
        }

        if (visible)
        {
            group.ShowWithFade();
        }
        else
        {
            group.HideWithFadeThenDisable();
        }
    }
}
