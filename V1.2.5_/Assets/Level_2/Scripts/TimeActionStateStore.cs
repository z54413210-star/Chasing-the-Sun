using System;
using System.Collections.Generic;
/// <summary>
/// Stores cross-time actions done in Past, read in Present.
/// Runtime-only (resets when exiting Play mode).
/// </summary>
public static class TimeActionStateStore
{
    private static readonly HashSet<string> WateredPitIds = new HashSet<string>();
    public static event Action<string, bool> OnPitWateredStateChanged;
    public static bool IsPitWatered(string pitId)
    {
        if (string.IsNullOrWhiteSpace(pitId)) return false;
        return WateredPitIds.Contains(pitId);
    }
    public static void SetPitWatered(string pitId, bool watered)
    {
        if (string.IsNullOrWhiteSpace(pitId)) return;
        var changed = false;
        if (watered)
        {
            changed = WateredPitIds.Add(pitId);
        }
        else
        {
            changed = WateredPitIds.Remove(pitId);
        }
        if (changed)
        {
            OnPitWateredStateChanged?.Invoke(pitId, watered);
        }
    }
    public static void ResetAll()
    {
        if (WateredPitIds.Count == 0) return;
        var ids = new List<string>(WateredPitIds);
        WateredPitIds.Clear();
        for (int i = 0; i < ids.Count; i++)
        {
            OnPitWateredStateChanged?.Invoke(ids[i], false);
        }
    }
}