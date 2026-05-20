using System;
using System.Collections.Generic;
using UnityEngine;

[AddComponentMenu("Chase The Sun/Level 3/Level 3 Dual Door Puzzle Controller")]
[DisallowMultipleComponent]
public class Level3DualDoorPuzzleController : MonoBehaviour
{
    public enum Level3PuzzleSide
    {
        Left,
        Right
    }

    public enum DoorSwitchPosition
    {
        Top,
        Middle,
        Bottom
    }

    public enum DoorSolveVisualMode
    {
        WholePuzzle,
        PerSideTarget
    }

    [Serializable]
    public struct LightTriple
    {
        public bool top;
        public bool middle;
        public bool bottom;

        public LightTriple(bool top, bool middle, bool bottom)
        {
            this.top = top;
            this.middle = middle;
            this.bottom = bottom;
        }
    }

    [Serializable]
    public struct ToggleTriple
    {
        public bool top;
        public bool middle;
        public bool bottom;

        public ToggleTriple(bool top, bool middle, bool bottom)
        {
            this.top = top;
            this.middle = middle;
            this.bottom = bottom;
        }
    }

    [Serializable]
    public class SideConfig
    {
        [Header("Lights")]
        public Level3PuzzleLight topLight;
        public Level3PuzzleLight middleLight;
        public Level3PuzzleLight bottomLight;

        [Header("States")]
        public LightTriple initialState;
        public LightTriple targetState;

        [Header("Switch Effects")]
        public ToggleTriple topSwitchEffect;
        public ToggleTriple middleSwitchEffect;
        public ToggleTriple bottomSwitchEffect;

        public ToggleTriple GetEffectFor(DoorSwitchPosition switchPosition)
        {
            switch (switchPosition)
            {
                case DoorSwitchPosition.Top:
                    return topSwitchEffect;
                case DoorSwitchPosition.Middle:
                    return middleSwitchEffect;
                default:
                    return bottomSwitchEffect;
            }
        }
    }

    [Header("Puzzle Setup")]
    [SerializeField] private SideConfig leftSide = CreateDefaultLeftSide();
    [SerializeField] private SideConfig rightSide = CreateDefaultRightSide();
    [SerializeField] private DoorSolveVisualMode doorSolveVisualMode = DoorSolveVisualMode.WholePuzzle;

    [Header("Debug (Runtime)")]
    [SerializeField] private LightTriple currentLeftState;
    [SerializeField] private LightTriple currentRightState;
    [SerializeField] private bool isSolved;

    private readonly List<Level3DoorInteractionStation> _doorStations = new List<Level3DoorInteractionStation>();
    private bool _hasRuntimeInitialized;

    public bool IsSolved => isSolved;

    private void Awake()
    {
        EnsureConfigs();
        InitializePuzzleState();
    }

    private void OnEnable()
    {
        EnsureConfigs();
        InitializePuzzleState();
    }

    private void Reset()
    {
        leftSide = CreateDefaultLeftSide();
        rightSide = CreateDefaultRightSide();
        SyncDebugStatesFromInitial();
    }

    private void OnValidate()
    {
        EnsureConfigs();

        if (!Application.isPlaying)
        {
            SyncDebugStatesFromInitial();
            ApplyLightStates();
        }
    }

    public void RegisterDoorStation(Level3DoorInteractionStation station)
    {
        if (station == null)
        {
            return;
        }

        if (!_doorStations.Contains(station))
        {
            _doorStations.Add(station);
        }

        if (Application.isPlaying && !_hasRuntimeInitialized)
        {
            InitializePuzzleState();
        }

        UpdateDoorVisual(station);
    }

    public void UnregisterDoorStation(Level3DoorInteractionStation station)
    {
        _doorStations.Remove(station);
    }

    public bool SubmitSwitchInput(Level3PuzzleSide actingSide, DoorSwitchPosition switchPosition)
    {
        var sourceConfig = GetConfig(actingSide);
        if (sourceConfig == null)
        {
            return false;
        }

        ApplyEffectToSide(actingSide, CreateSelfSwitchEffect(switchPosition));
        ApplyEffectToSide(GetOppositeSide(actingSide), sourceConfig.GetEffectFor(switchPosition));
        RefreshPuzzleState();
        return true;
    }

    public bool IsSideAtTarget(Level3PuzzleSide side)
    {
        var config = GetConfig(side);
        if (config == null)
        {
            return false;
        }

        return StatesMatch(GetCurrentState(side), config.targetState);
    }

    public bool ShouldDoorBeOpen(Level3PuzzleSide side)
    {
        if (doorSolveVisualMode == DoorSolveVisualMode.PerSideTarget)
        {
            return IsSideAtTarget(side);
        }

        return isSolved;
    }

    [ContextMenu("Reset Puzzle State")]
    public void ResetPuzzleState()
    {
        InitializePuzzleState();
    }

    private void InitializePuzzleState()
    {
        currentLeftState = leftSide != null ? leftSide.initialState : default(LightTriple);
        currentRightState = rightSide != null ? rightSide.initialState : default(LightTriple);
        _hasRuntimeInitialized = true;
        RefreshPuzzleState();
    }

    private void RefreshPuzzleState()
    {
        ApplyLightStates();
        isSolved = IsSideAtTarget(Level3PuzzleSide.Left) && IsSideAtTarget(Level3PuzzleSide.Right);
        UpdateAllDoorVisuals();
    }

    private void ApplyLightStates()
    {
        ApplySideLights(leftSide, currentLeftState);
        ApplySideLights(rightSide, currentRightState);
    }

    private static void ApplySideLights(SideConfig config, LightTriple state)
    {
        if (config == null)
        {
            return;
        }

        if (config.topLight != null)
        {
            config.topLight.SetState(state.top);
        }

        if (config.middleLight != null)
        {
            config.middleLight.SetState(state.middle);
        }

        if (config.bottomLight != null)
        {
            config.bottomLight.SetState(state.bottom);
        }
    }

    private void ApplyEffectToSide(Level3PuzzleSide targetSide, ToggleTriple effect)
    {
        if (targetSide == Level3PuzzleSide.Left)
        {
            currentLeftState = ToggleState(currentLeftState, effect);
            return;
        }

        currentRightState = ToggleState(currentRightState, effect);
    }

    private static ToggleTriple CreateSelfSwitchEffect(DoorSwitchPosition switchPosition)
    {
        switch (switchPosition)
        {
            case DoorSwitchPosition.Top:
                return new ToggleTriple(true, false, false);
            case DoorSwitchPosition.Middle:
                return new ToggleTriple(false, true, false);
            default:
                return new ToggleTriple(false, false, true);
        }
    }

    private void UpdateAllDoorVisuals()
    {
        for (var i = _doorStations.Count - 1; i >= 0; i--)
        {
            var station = _doorStations[i];
            if (station == null)
            {
                _doorStations.RemoveAt(i);
                continue;
            }

            UpdateDoorVisual(station);
        }
    }

    private void UpdateDoorVisual(Level3DoorInteractionStation station)
    {
        if (station == null)
        {
            return;
        }

        station.SetDoorOpenVisual(ShouldDoorBeOpen(station.Side));
    }

    private SideConfig GetConfig(Level3PuzzleSide side)
    {
        return side == Level3PuzzleSide.Left ? leftSide : rightSide;
    }

    private LightTriple GetCurrentState(Level3PuzzleSide side)
    {
        return side == Level3PuzzleSide.Left ? currentLeftState : currentRightState;
    }

    private static Level3PuzzleSide GetOppositeSide(Level3PuzzleSide side)
    {
        return side == Level3PuzzleSide.Left ? Level3PuzzleSide.Right : Level3PuzzleSide.Left;
    }

    private static LightTriple ToggleState(LightTriple state, ToggleTriple effect)
    {
        state.top ^= effect.top;
        state.middle ^= effect.middle;
        state.bottom ^= effect.bottom;
        return state;
    }

    private static bool StatesMatch(LightTriple a, LightTriple b)
    {
        return a.top == b.top
            && a.middle == b.middle
            && a.bottom == b.bottom;
    }

    private void EnsureConfigs()
    {
        if (leftSide == null)
        {
            leftSide = CreateDefaultLeftSide();
        }

        if (rightSide == null)
        {
            rightSide = CreateDefaultRightSide();
        }
    }

    private void SyncDebugStatesFromInitial()
    {
        currentLeftState = leftSide != null ? leftSide.initialState : default(LightTriple);
        currentRightState = rightSide != null ? rightSide.initialState : default(LightTriple);
        isSolved = leftSide != null
            && rightSide != null
            && StatesMatch(currentLeftState, leftSide.targetState)
            && StatesMatch(currentRightState, rightSide.targetState);
    }

    private static SideConfig CreateDefaultLeftSide()
    {
        return new SideConfig
        {
            initialState = new LightTriple(false, true, false),
            targetState = new LightTriple(true, true, true),
            topSwitchEffect = new ToggleTriple(true, true, true),
            middleSwitchEffect = new ToggleTriple(true, true, false),
            bottomSwitchEffect = new ToggleTriple(false, true, true)
        };
    }

    private static SideConfig CreateDefaultRightSide()
    {
        return new SideConfig
        {
            initialState = new LightTriple(true, false, true),
            targetState = new LightTriple(false, false, false),
            topSwitchEffect = new ToggleTriple(true, true, true),
            middleSwitchEffect = new ToggleTriple(true, true, false),
            bottomSwitchEffect = new ToggleTriple(false, true, true)
        };
    }
}
