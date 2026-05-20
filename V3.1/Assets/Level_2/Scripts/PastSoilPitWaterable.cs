using UnityEngine;
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class PastSoilPitWaterable : MonoBehaviour
{
    [Header("Cross-time ID (must match Present vine)")]
    [SerializeField] private string pitId = "pit_001";
    [Header("Input")]
    [SerializeField] private KeyCode waterKey = KeyCode.P;
    [Header("Visuals (Optional)")]
    [SerializeField] private GameObject dryPitVisual;
    [SerializeField] private GameObject wateredPitVisual;
    [SerializeField] private GameObject inRangeHintVisual;
    private bool _playerInRange;
    private void OnEnable()
    {
        ApplyVisual(TimeActionStateStore.IsPitWatered(pitId));
        if (inRangeHintVisual != null)
        {
            inRangeHintVisual.SetActive(false);
        }
    }
    private void Update()
    {
        if (!_playerInRange) return;
        if (TimeActionStateStore.IsPitWatered(pitId)) return;
        if (Input.GetKeyDown(waterKey))
        {
            TimeActionStateStore.SetPitWatered(pitId, true);
            ApplyVisual(true);
            if (inRangeHintVisual != null)
            {
                inRangeHintVisual.SetActive(false);
            }
        }
    }
    private void OnTriggerEnter2D(Collider2D other)
    {
        var player = other.GetComponentInParent<PlayerController2D>();
        if (player == null) return;
        _playerInRange = true;
        if (!TimeActionStateStore.IsPitWatered(pitId) && inRangeHintVisual != null)
        {
            inRangeHintVisual.SetActive(true);
        }
    }
    private void OnTriggerExit2D(Collider2D other)
    {
        var player = other.GetComponentInParent<PlayerController2D>();
        if (player == null) return;
        _playerInRange = false;
        if (inRangeHintVisual != null)
        {
            inRangeHintVisual.SetActive(false);
        }
    }
    private void ApplyVisual(bool watered)
    {
        if (dryPitVisual != null)
        {
            dryPitVisual.SetActive(!watered);
        }
        if (wateredPitVisual != null)
        {
            wateredPitVisual.SetActive(watered);
        }
    }
    private void Reset()
    {
        var trigger = GetComponent<Collider2D>();
        if (trigger != null)
        {
            trigger.isTrigger = true;
        }
    }
}