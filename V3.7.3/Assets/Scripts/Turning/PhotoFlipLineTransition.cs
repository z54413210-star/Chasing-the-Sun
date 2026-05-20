using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// 全屏转场：渐亮至全白 → 开场问句文案 → 全屏照片淡入 → 停留 → 左侧白色文段逐段显示 → 渐黑 → 下一场景。空格跳过并直接加载 <see cref="nextSceneName"/>。
/// </summary>
[DisallowMultipleComponent]
public class PhotoFlipLineTransition : MonoBehaviour
{
    private const string DefaultIntroMessage =
        "Are you awake? You seemed to have a long dream. Remember this photo?";

    [Header("Content")]
    [SerializeField] private Sprite photoSprite;
    [TextArea(2, 6)]
    [SerializeField] private string introMessage = DefaultIntroMessage;
    [FormerlySerializedAs("transitionLines")]
    [Tooltip("照片展示结束后，在屏幕左侧逐段淡入的白色文案。同一段内换行：在 Inspector 里写 \\n（反斜杠+n），或从外部粘贴带真实换行的文本。")]
    [SerializeField] private string[] transitionParagraphs;
    [SerializeField] private TMP_FontAsset fontOverride;

    [Header("Brighten → white")]
    [SerializeField] private float brightenDuration = 1.35f;

    [Header("Intro (on white)")]
    [SerializeField] private float introFadeInDuration = 0.9f;
    [SerializeField] private float introHoldDuration = 1.6f;
    [SerializeField] private float introFadeOutDuration = 0.75f;
    [SerializeField] private float introFontSize = 32f;
    [SerializeField] private Color introTextColor = new Color(0.12f, 0.12f, 0.14f, 1f);

    [Header("Photo — full screen")]
    [SerializeField] private float photoFadeInDuration = 1.1f;
    [Tooltip("为 true 时保持比例，可能留边；为 false 时拉伸铺满全屏。")]
    [SerializeField] private bool photoPreserveAspect = false;
    [SerializeField] private float holdPhotoDuration = 2f;

    [Header("Paragraphs (white / left, on top of photo)")]
    [SerializeField] private float delayBeforeParagraphs = 0.25f;
    [FormerlySerializedAs("lineFadeInDuration")]
    [SerializeField] private float paragraphFadeInDuration = 1.05f;
    [FormerlySerializedAs("pauseAfterEachLine")]
    [SerializeField] private float pauseAfterEachParagraph = 1.2f;
    [SerializeField] private float bodyFontSize = 26f;
    [FormerlySerializedAs("lineSpacing")]
    [SerializeField] private float gapBetweenParagraphs = 20f;
    [SerializeField] private float intraParagraphLineSpacing = 8f;
    [Tooltip("左侧文段区域：左下角锚点 x，右下角锚点 x（0~1）。")]
    [SerializeField] private float paragraphsAnchorXMin = 0.04f;
    [SerializeField] private float paragraphsAnchorXMax = 0.48f;
    [SerializeField] private float paragraphsAnchorYMin = 0.12f;
    [SerializeField] private float paragraphsAnchorYMax = 0.78f;

    [Header("End — black, then load")]
    [SerializeField] private float finalBlackFadeDuration = 1f;

    [Header("Layout")]
    [SerializeField] private int canvasSortOrder = 600;

    [Header("Skip")]
    [SerializeField] private bool allowSkipWithSpace = true;
    [SerializeField] private KeyCode skipKey = KeyCode.Space;
    [SerializeField] private float skipBlackFlashDuration = 0f;
    [SerializeField] private bool showSkipHint = true;
    [Tooltip("若改用其它 Skip Key，请同步修改这句提示文案。")]
    [SerializeField] private string skipHintText = "Press spacebar to skip this";
    [SerializeField] private float skipHintFontSize = 22f;
    [SerializeField] private Color skipHintColor = new Color(0.22f, 0.22f, 0.25f, 0.72f);

    [Header("Player / time")]
    [SerializeField] private bool freezeTimeDuringTransition = true;
    [SerializeField] private bool disablePlayerWhilePlaying = true;

    [Header("Scene load")]
    [SerializeField] private bool loadSceneWhenFinished = true;
    [SerializeField] private string nextSceneName;
    [SerializeField] private UnityEvent onTransitionComplete;

    private CanvasGroup _whiteBrightenGroup;
    private CanvasGroup _blackOutGroup;
    private CanvasGroup _photoHostGroup;
    private CanvasGroup _introRootGroup;
    private CanvasGroup _paragraphsRootGroup;
    private CanvasGroup _skipHintGroup;
    private readonly List<CanvasGroup> _paragraphGroups = new List<CanvasGroup>();

    private GameObject _canvasRoot;
    private Coroutine _routine;
    private bool _isPlaying;
    private float _savedTimeScale = 1f;
    private PlayerController2D _cachedPlayer;
    private bool _hadPlayerControllerEnabled;

    public bool IsPlaying => _isPlaying;

    private void Awake()
    {
        BuildUi();
        if (_canvasRoot != null)
        {
            _canvasRoot.SetActive(false);
        }
    }

    private void Update()
    {
        if (!_isPlaying || !allowSkipWithSpace)
        {
            return;
        }

        if (Input.GetKeyDown(skipKey))
        {
            SkipToNextScene();
        }
    }

    public void Play()
    {
        if (_isPlaying)
        {
            return;
        }

        if (_canvasRoot == null)
        {
            BuildUi();
        }

        _routine = StartCoroutine(PlayRoutine());
    }

    public void SkipToNextScene()
    {
        if (!_isPlaying)
        {
            return;
        }

        _isPlaying = false;
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }

        if (skipBlackFlashDuration > 0f)
        {
            _routine = StartCoroutine(SkipRoutine());
            return;
        }

        if (_blackOutGroup != null)
        {
            _blackOutGroup.alpha = 1f;
        }

        CompleteTransitionAndMaybeLoad();
    }

    private IEnumerator SkipRoutine()
    {
        if (_blackOutGroup != null)
        {
            yield return FadeCanvasGroup(_blackOutGroup, _blackOutGroup.alpha, 1f, skipBlackFlashDuration);
        }

        CompleteTransitionAndMaybeLoad();
        _routine = null;
    }

    private IEnumerator PlayRoutine()
    {
        _isPlaying = true;

        if (freezeTimeDuringTransition)
        {
            _savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }

        if (disablePlayerWhilePlaying)
        {
            _cachedPlayer = FindObjectOfType<PlayerController2D>();
            if (_cachedPlayer != null)
            {
                _hadPlayerControllerEnabled = _cachedPlayer.enabled;
                var rb = _cachedPlayer.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.velocity = Vector2.zero;
                    rb.angularVelocity = 0f;
                }

                _cachedPlayer.enabled = false;
            }
        }

        ResetVisualStateForPlay();
        _canvasRoot.SetActive(true);

        yield return FadeCanvasGroup(_whiteBrightenGroup, 0f, 1f, brightenDuration);

        yield return FadeCanvasGroup(_introRootGroup, 0f, 1f, introFadeInDuration);
        yield return WaitUnscaled(introHoldDuration);

        if (photoSprite != null && _photoHostGroup != null)
        {
            yield return RunIntroOutAndPhotoInParallel();
            yield return WaitUnscaled(holdPhotoDuration);
        }
        else
        {
            yield return FadeCanvasGroup(_introRootGroup, 1f, 0f, introFadeOutDuration);
        }

        yield return WaitUnscaled(delayBeforeParagraphs);

        foreach (var paragraphCg in _paragraphGroups)
        {
            if (paragraphCg == null)
            {
                continue;
            }

            yield return FadeCanvasGroup(paragraphCg, 0f, 1f, paragraphFadeInDuration);
            yield return WaitUnscaled(pauseAfterEachParagraph);
        }

        yield return FadeCanvasGroup(_blackOutGroup, 0f, 1f, finalBlackFadeDuration);

        CompleteTransitionAndMaybeLoad();
        _routine = null;
    }

    private IEnumerator RunIntroOutAndPhotoInParallel()
    {
        var introFrom = _introRootGroup != null ? _introRootGroup.alpha : 1f;
        var photoFrom = _photoHostGroup != null ? _photoHostGroup.alpha : 0f;
        var maxDur = Mathf.Max(introFadeOutDuration, photoFadeInDuration);
        if (maxDur <= 0f)
        {
            if (_introRootGroup != null)
            {
                _introRootGroup.alpha = 0f;
            }

            if (_photoHostGroup != null)
            {
                _photoHostGroup.alpha = 1f;
            }

            yield break;
        }

        var elapsed = 0f;
        while (elapsed < maxDur)
        {
            elapsed += Time.unscaledDeltaTime;
            var u = Mathf.Clamp01(elapsed / maxDur);
            if (_introRootGroup != null && introFadeOutDuration > 0f)
            {
                var ui = Mathf.Clamp01(elapsed / introFadeOutDuration);
                _introRootGroup.alpha = Mathf.Lerp(introFrom, 0f, ui);
            }
            else if (_introRootGroup != null)
            {
                _introRootGroup.alpha = 0f;
            }

            if (_photoHostGroup != null && photoFadeInDuration > 0f)
            {
                var up = Mathf.Clamp01(elapsed / photoFadeInDuration);
                _photoHostGroup.alpha = Mathf.Lerp(photoFrom, 1f, up);
            }
            else if (_photoHostGroup != null)
            {
                _photoHostGroup.alpha = 1f;
            }

            yield return null;
        }

        if (_introRootGroup != null)
        {
            _introRootGroup.alpha = 0f;
        }

        if (_photoHostGroup != null)
        {
            _photoHostGroup.alpha = 1f;
        }
    }

    private void ResetVisualStateForPlay()
    {
        if (_whiteBrightenGroup != null)
        {
            _whiteBrightenGroup.alpha = 0f;
        }

        if (_blackOutGroup != null)
        {
            _blackOutGroup.alpha = 0f;
        }

        if (_photoHostGroup != null)
        {
            _photoHostGroup.alpha = 0f;
        }

        if (_introRootGroup != null)
        {
            _introRootGroup.alpha = 0f;
        }

        if (_paragraphsRootGroup != null)
        {
            _paragraphsRootGroup.alpha = 1f;
        }

        foreach (var lg in _paragraphGroups)
        {
            if (lg != null)
            {
                lg.alpha = 0f;
            }
        }

        if (_skipHintGroup != null)
        {
            _skipHintGroup.alpha = allowSkipWithSpace && showSkipHint ? 1f : 0f;
        }
    }

    private void CompleteTransitionAndMaybeLoad()
    {
        if (disablePlayerWhilePlaying && _cachedPlayer != null)
        {
            _cachedPlayer.enabled = _hadPlayerControllerEnabled;
            _cachedPlayer = null;
        }

        if (freezeTimeDuringTransition)
        {
            Time.timeScale = _savedTimeScale;
        }

        _isPlaying = false;
        onTransitionComplete?.Invoke();

        if (loadSceneWhenFinished && !string.IsNullOrWhiteSpace(nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName);
            return;
        }

        if (_canvasRoot != null)
        {
            _canvasRoot.SetActive(false);
        }
    }

    private static IEnumerator FadeCanvasGroup(CanvasGroup g, float from, float to, float duration)
    {
        if (g == null)
        {
            yield break;
        }

        if (duration <= 0f)
        {
            g.alpha = to;
            yield break;
        }

        var elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            g.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        g.alpha = to;
    }

    private static IEnumerator WaitUnscaled(float seconds)
    {
        if (seconds <= 0f)
        {
            yield break;
        }

        var t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private void BuildUi()
    {
        if (_canvasRoot != null)
        {
            return;
        }

        _canvasRoot = new GameObject("PhotoFlipTransitionCanvas");
        _canvasRoot.transform.SetParent(transform, false);

        var canvas = _canvasRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = canvasSortOrder;

        var scaler = _canvasRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        _canvasRoot.AddComponent<GraphicRaycaster>();

        var rootBlocker = _canvasRoot.AddComponent<CanvasGroup>();
        rootBlocker.alpha = 1f;
        rootBlocker.blocksRaycasts = true;
        rootBlocker.interactable = false;

        var whiteGo = new GameObject("WhiteBrighten");
        whiteGo.transform.SetParent(_canvasRoot.transform, false);
        var whiteRect = whiteGo.AddComponent<RectTransform>();
        whiteRect.anchorMin = Vector2.zero;
        whiteRect.anchorMax = Vector2.one;
        whiteRect.offsetMin = Vector2.zero;
        whiteRect.offsetMax = Vector2.zero;
        var whiteImg = whiteGo.AddComponent<Image>();
        whiteImg.color = Color.white;
        whiteImg.raycastTarget = false;
        _whiteBrightenGroup = whiteGo.AddComponent<CanvasGroup>();
        _whiteBrightenGroup.alpha = 0f;
        _whiteBrightenGroup.blocksRaycasts = false;

        var introRoot = new GameObject("IntroText");
        introRoot.transform.SetParent(_canvasRoot.transform, false);
        var introRect = introRoot.AddComponent<RectTransform>();
        introRect.anchorMin = new Vector2(0.08f, 0.35f);
        introRect.anchorMax = new Vector2(0.92f, 0.65f);
        introRect.offsetMin = Vector2.zero;
        introRect.offsetMax = Vector2.zero;
        _introRootGroup = introRoot.AddComponent<CanvasGroup>();
        _introRootGroup.alpha = 0f;
        _introRootGroup.blocksRaycasts = false;

        var introTmpGo = new GameObject("IntroTMP");
        introTmpGo.transform.SetParent(introRoot.transform, false);
        var introTmpRect = introTmpGo.AddComponent<RectTransform>();
        introTmpRect.anchorMin = Vector2.zero;
        introTmpRect.anchorMax = Vector2.one;
        introTmpRect.offsetMin = Vector2.zero;
        introTmpRect.offsetMax = Vector2.zero;
        var introTmp = introTmpGo.AddComponent<TextMeshProUGUI>();
        introTmp.text = FormatMultilineForTmp(string.IsNullOrWhiteSpace(introMessage) ? DefaultIntroMessage : introMessage);
        introTmp.color = introTextColor;
        introTmp.fontSize = introFontSize;
        introTmp.alignment = TextAlignmentOptions.Center;
        introTmp.enableWordWrapping = true;
        introTmp.overflowMode = TextOverflowModes.Overflow;
        ApplyFont(introTmp);

        var photoHost = new GameObject("PhotoHost");
        photoHost.transform.SetParent(_canvasRoot.transform, false);
        var hostRect = photoHost.AddComponent<RectTransform>();
        hostRect.anchorMin = Vector2.zero;
        hostRect.anchorMax = Vector2.one;
        hostRect.offsetMin = Vector2.zero;
        hostRect.offsetMax = Vector2.zero;
        _photoHostGroup = photoHost.AddComponent<CanvasGroup>();
        _photoHostGroup.alpha = 0f;
        _photoHostGroup.blocksRaycasts = false;

        var img = photoHost.AddComponent<Image>();
        img.sprite = photoSprite;
        img.preserveAspect = photoPreserveAspect;
        img.color = Color.white;
        img.raycastTarget = false;

        var linesRoot = new GameObject("ParagraphsRoot");
        linesRoot.transform.SetParent(_canvasRoot.transform, false);
        var linesRect = linesRoot.AddComponent<RectTransform>();
        linesRect.anchorMin = new Vector2(paragraphsAnchorXMin, paragraphsAnchorYMin);
        linesRect.anchorMax = new Vector2(paragraphsAnchorXMax, paragraphsAnchorYMax);
        linesRect.offsetMin = Vector2.zero;
        linesRect.offsetMax = Vector2.zero;

        var vlg = linesRoot.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = gapBetweenParagraphs;
        vlg.padding = new RectOffset(8, 8, 4, 4);
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        _paragraphsRootGroup = linesRoot.AddComponent<CanvasGroup>();
        _paragraphsRootGroup.alpha = 1f;
        _paragraphsRootGroup.blocksRaycasts = false;

        _paragraphGroups.Clear();
        var paragraphs = transitionParagraphs != null ? transitionParagraphs : System.Array.Empty<string>();
        var estW = scaler.referenceResolution.x * Mathf.Max(0.05f, paragraphsAnchorXMax - paragraphsAnchorXMin) - 24f;

        foreach (var paragraph in paragraphs)
        {
            var formatted = FormatMultilineForTmp(paragraph);
            if (string.IsNullOrWhiteSpace(formatted))
            {
                continue;
            }

            var row = new GameObject("Paragraph");
            row.transform.SetParent(linesRoot.transform, false);
            var le = row.AddComponent<LayoutElement>();
            le.minHeight = 0f;

            var tmp = row.AddComponent<TextMeshProUGUI>();
            tmp.text = formatted;
            tmp.color = Color.white;
            tmp.fontSize = bodyFontSize;
            tmp.lineSpacing = intraParagraphLineSpacing;
            tmp.alignment = TextAlignmentOptions.TopLeft;
            tmp.enableWordWrapping = true;
            tmp.overflowMode = TextOverflowModes.Overflow;
            ApplyFont(tmp);

            tmp.ForceMeshUpdate();
            var pref = tmp.GetPreferredValues(estW, 0f);
            le.preferredHeight = Mathf.Max(pref.y + 10f, 40f);

            var cg = row.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            cg.blocksRaycasts = false;
            _paragraphGroups.Add(cg);
        }

        var skipHintGo = new GameObject("SkipHint");
        skipHintGo.transform.SetParent(_canvasRoot.transform, false);
        var skipHintRect = skipHintGo.AddComponent<RectTransform>();
        skipHintRect.anchorMin = new Vector2(0.5f, 0.06f);
        skipHintRect.anchorMax = new Vector2(0.5f, 0.06f);
        skipHintRect.pivot = new Vector2(0.5f, 0f);
        skipHintRect.anchoredPosition = Vector2.zero;
        skipHintRect.sizeDelta = new Vector2(920f, 44f);
        var skipTmp = skipHintGo.AddComponent<TextMeshProUGUI>();
        skipTmp.text = string.IsNullOrWhiteSpace(skipHintText) ? "Press spacebar to skip this" : skipHintText.Trim();
        skipTmp.color = skipHintColor;
        skipTmp.fontSize = skipHintFontSize;
        skipTmp.alignment = TextAlignmentOptions.Bottom;
        skipTmp.enableWordWrapping = true;
        skipTmp.overflowMode = TextOverflowModes.Overflow;
        ApplyFont(skipTmp);
        _skipHintGroup = skipHintGo.AddComponent<CanvasGroup>();
        _skipHintGroup.alpha = 0f;
        _skipHintGroup.blocksRaycasts = false;

        var blackOutGo = new GameObject("BlackOut");
        blackOutGo.transform.SetParent(_canvasRoot.transform, false);
        var blackRect = blackOutGo.AddComponent<RectTransform>();
        blackRect.anchorMin = Vector2.zero;
        blackRect.anchorMax = Vector2.one;
        blackRect.offsetMin = Vector2.zero;
        blackRect.offsetMax = Vector2.zero;
        var blackImg = blackOutGo.AddComponent<Image>();
        blackImg.color = Color.black;
        blackImg.raycastTarget = false;
        _blackOutGroup = blackOutGo.AddComponent<CanvasGroup>();
        _blackOutGroup.alpha = 0f;
        _blackOutGroup.blocksRaycasts = false;
    }

    private void ApplyFont(TextMeshProUGUI tmp)
    {
        if (tmp == null)
        {
            return;
        }

        if (fontOverride != null)
        {
            tmp.font = fontOverride;
            return;
        }

        if (TMP_Settings.defaultFontAsset != null)
        {
            tmp.font = TMP_Settings.defaultFontAsset;
        }
    }

    /// <summary>
    /// 统一换行：真实换行保留；字面量 \n（反斜杠+n）转为换行，便于在单行 Inspector 字段里分段。
    /// </summary>
    private static string FormatMultilineForTmp(string raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return raw;
        }

        var s = raw.Trim();
        s = s.Replace("\r\n", "\n").Replace('\r', '\n');
        s = s.Replace("\\n", "\n");
        return s;
    }

    private void OnDestroy()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }

        if (freezeTimeDuringTransition)
        {
            Time.timeScale = _savedTimeScale;
        }

        if (_cachedPlayer != null && disablePlayerWhilePlaying)
        {
            _cachedPlayer.enabled = _hadPlayerControllerEnabled;
        }
    }
}
