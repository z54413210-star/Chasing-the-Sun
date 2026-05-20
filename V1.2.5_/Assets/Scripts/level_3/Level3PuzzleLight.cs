using UnityEngine;

[AddComponentMenu("Chase The Sun/Level 3/Level 3 Puzzle Light")]
[DisallowMultipleComponent]
public class Level3PuzzleLight : MonoBehaviour
{
    [Header("Light Visuals")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite offSprite;
    [SerializeField] private Sprite onSprite;
    [SerializeField] private bool previewState;

    private bool _isOn;

    public bool IsOn => _isOn;

    private void Awake()
    {
        CacheReferences();
        _isOn = previewState;
        ApplyVisual(_isOn);
    }

    private void OnEnable()
    {
        CacheReferences();
        ApplyVisual(Application.isPlaying ? _isOn : previewState);
    }

    private void Reset()
    {
        CacheReferences();
    }

    private void OnValidate()
    {
        CacheReferences();
        ApplyVisual(Application.isPlaying ? _isOn : previewState);
    }

    public void SetState(bool isOn)
    {
        if (_isOn == isOn && Application.isPlaying)
        {
            ApplyVisual(_isOn);
            return;
        }

        _isOn = isOn;
        ApplyVisual(_isOn);
    }

    private void CacheReferences()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }
    }

    private void ApplyVisual(bool isOn)
    {
        if (spriteRenderer == null)
        {
            return;
        }

        var targetSprite = isOn ? onSprite : offSprite;
        if (targetSprite == null || spriteRenderer.sprite == targetSprite)
        {
            return;
        }

        spriteRenderer.sprite = targetSprite;
    }
}
