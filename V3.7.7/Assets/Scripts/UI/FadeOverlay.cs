using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class FadeOverlay : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;

    private void Awake()
    {
        CacheComponents();
        SetAlpha(canvasGroup.alpha);
    }

    private void OnValidate()
    {
        CacheComponents();
    }

    public static FadeOverlay FindOrCreateOverlay()
    {
        var existingOverlay = FindObjectOfType<FadeOverlay>(true);
        if (existingOverlay != null)
        {
            existingOverlay.SetAlpha(0f);
            return existingOverlay;
        }

        var canvasObject = new GameObject("RuntimeFadeCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;

        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        var fadeObject = new GameObject("Fade", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup), typeof(FadeOverlay));
        var fadeRect = fadeObject.GetComponent<RectTransform>();
        fadeRect.SetParent(canvasObject.transform, false);
        fadeRect.anchorMin = Vector2.zero;
        fadeRect.anchorMax = Vector2.one;
        fadeRect.offsetMin = Vector2.zero;
        fadeRect.offsetMax = Vector2.zero;

        var image = fadeObject.GetComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = true;

        var overlay = fadeObject.GetComponent<FadeOverlay>();
        overlay.SetAlpha(0f);
        return overlay;
    }

    public IEnumerator FadeTo(float targetAlpha, float duration)
    {
        CacheComponents();

        var startAlpha = canvasGroup.alpha;
        if (Mathf.Approximately(duration, 0f))
        {
            SetAlpha(targetAlpha);
            yield break;
        }

        var elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetAlpha(Mathf.Lerp(startAlpha, targetAlpha, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }

        SetAlpha(targetAlpha);
    }

    public void SetAlpha(float alpha)
    {
        CacheComponents();
        canvasGroup.alpha = Mathf.Clamp01(alpha);
        canvasGroup.blocksRaycasts = canvasGroup.alpha > 0.001f;
        canvasGroup.interactable = canvasGroup.blocksRaycasts;
    }

    private void CacheComponents()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }
    }
}
