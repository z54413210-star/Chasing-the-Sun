using UnityEngine;
[DisallowMultipleComponent]
public class PresentVineFromPast : MonoBehaviour
{
    [Header("Cross-time ID (must match Past pit)")]
    [SerializeField] private string pitId = "pit_001";
    [Header("Visual")]
    [SerializeField] private GameObject vineVisualRoot;
    [SerializeField] private bool hideWhenNotWatered = true;
    private void OnEnable()
    {
        TimeActionStateStore.OnPitWateredStateChanged += HandlePitStateChanged;
        RefreshFromStore();
    }
    private void OnDisable()
    {
        TimeActionStateStore.OnPitWateredStateChanged -= HandlePitStateChanged;
    }
    private void HandlePitStateChanged(string changedPitId, bool watered)
    {
        if (!string.Equals(changedPitId, pitId)) return;
        ApplyVisual(watered);
    }
    private void RefreshFromStore()
    {
        ApplyVisual(TimeActionStateStore.IsPitWatered(pitId));
    }
    private void ApplyVisual(bool watered)
    {
        var target = vineVisualRoot != null ? vineVisualRoot : gameObject;
        if (target == null) return;
        if (watered)
        {
            target.SetActive(true);
        }
        else if (hideWhenNotWatered)
        {
            target.SetActive(false);
        }
    }
}
