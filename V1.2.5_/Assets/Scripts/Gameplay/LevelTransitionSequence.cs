using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 关卡间长转场：支持「黑场+分段白字+章节标题」「白场+照片+左文+章节标题」「终章：黑左文+致谢+结束图」等。
/// 按住空格超过 <see cref="skipHoldDurationSeconds"/> 可跳到章节标题或致谢段落（依 <see cref="LevelTransitionKind"/>）。
/// </summary>
[DisallowMultipleComponent]
public class LevelTransitionSequence : MonoBehaviour
{
    public enum LevelTransitionKind
    {
        /// <summary>Level0→1：渐暗至黑 → 分段白字 → 淡出 → 章节标题放大 → 进关。</summary>
        DarkIntroChapterTitle,
        /// <summary>Level1→2、2→3：渐白 → 开场英文 → 全屏照片 → 左白字 → 渐黑 → 章节标题 → 进关。</summary>
        PhotoWhiteParagraphsChapter,
        /// <summary>Level3→结束：照片流程但左字为黑；渐黑 → 白字致谢分段 → 结束全屏图。</summary>
        PhotoBlackParagraphsCreditsEndPhoto
    }

    private const string DefaultDarkIntro =
        "Hello! I know it's your first time to come here, a totally different and magical world. Don't be afraid. I will accompany you";

    private const string DefaultPhotoIntro =
        "Are you awake? You seemed to have a long dream. Remember this photo?";

    [Header("Mode")]
    [SerializeField] private LevelTransitionKind kind = LevelTransitionKind.PhotoWhiteParagraphsChapter;

    [Header("Scene")]
    [SerializeField] private string nextSceneName;
    [SerializeField] private bool loadSceneWhenFinished = true;
    [SerializeField] private UnityEvent onBeforeLoadScene;

    [Header("Content — Dark intro (Mode: DarkIntroChapterTitle)")]
    [Tooltip("分段显示，每一段淡入一次。")]
    [SerializeField] private string[] darkIntroSegments =
    {
        "Hello! I know it's your first time to come here, a totally different and magical world.",
        "Don't be afraid. I will accompany you"
    };

    [Header("Content — Photo flow")]
    [TextArea(2, 5)]
    [SerializeField] private string introMessage = DefaultPhotoIntro;
    [SerializeField] private Sprite photoSprite;
    [SerializeField] private string[] transitionParagraphs;
    [SerializeField] private Sprite endPhotoSprite;

    [Header("Content — Chapter title (DarkIntro / Photo*Chapter)")]
    [SerializeField] private string chapterTitleText = "Chapter1  The Lost Self";
    [SerializeField] private float chapterTitleFontSize = 44f;

    [Header("Content — End credits (Mode: Final only)")]
    [SerializeField] private string[] endCreditsSegments =
    {
        "Thanks for your playing.",
        "Get you get, and be confident"
    };

    [Header("Timing (unscaled)")]
    [SerializeField] private float darkenDuration = 1.2f;
    [SerializeField] private float brightenDuration = 1.35f;
    [SerializeField] private float darkSegmentFadeIn = 0.55f;
    [SerializeField] private float darkSegmentPause = 0.35f;
    [SerializeField] private float darkIntroFadeOutDuration = 0.75f;
    [SerializeField] private float introFadeInDuration = 0.9f;
    [SerializeField] private float introHoldDuration = 1.5f;
    [SerializeField] private float introFadeOutDuration = 0.75f;
    [SerializeField] private float photoFadeInDuration = 1.1f;
    [SerializeField] private float holdPhotoDuration = 2f;
    [SerializeField] private bool photoPreserveAspect;
    [SerializeField] private float delayBeforeParagraphs = 0.25f;
    [SerializeField] private float paragraphFadeInDuration = 1.05f;
    [SerializeField] private float pauseAfterEachParagraph = 1.1f;
    [SerializeField] private float finalBlackFadeDuration = 1f;
    [SerializeField] private float chapterGrowDuration = 3f;
    [SerializeField] private float chapterTitleStartScale = 0.35f;
    [SerializeField] private float chapterTitleEndScale = 1.05f;
    [SerializeField] private float chapterTitleFadeOutDuration = 0.45f;
    [SerializeField] private float creditsSegmentFadeIn = 0.5f;
    [SerializeField] private float creditsSegmentPause = 0.4f;
    [SerializeField] private float endPhotoFadeInDuration = 1f;
    [SerializeField] private float endPhotoHoldDuration = 2.5f;

    [Header("Typography")]
    [SerializeField] private TMP_FontAsset fontOverride;
    [SerializeField] private float bodyFontSize = 26f;
    [SerializeField] private Color darkIntroTextColor = Color.white;
    [SerializeField] private Color introOnWhiteColor = new Color(0.12f, 0.12f, 0.14f, 1f);
    [SerializeField] private Color chapterTitleColor = Color.white;
    [SerializeField] private Color creditsTextColor = Color.white;
    [SerializeField] private Color paragraphWhite = Color.white;
    [SerializeField] private Color paragraphBlack = new Color(0.08f, 0.08f, 0.1f, 1f);

    [Header("Skip")]
    [SerializeField] private bool allowSkipWithSpace = true;
    [SerializeField] private KeyCode skipKey = KeyCode.Space;
    [SerializeField] private float skipHoldDurationSeconds = 0.4f;
    [SerializeField] private bool showSkipHint = true;
    [SerializeField] private string skipHintText = "Press spacebar to skip";
    [SerializeField] private float skipHintFontSize = 18f;

    [Header("Player / time")]
    [SerializeField] private int canvasSortOrder = 610;
    [SerializeField] private bool freezeTimeDuringTransition = true;
    [SerializeField] private bool disablePlayerWhilePlaying = true;

    private CanvasGroup _blackFullGroup;
    private CanvasGroup _whiteFullGroup;
    private CanvasGroup _introGroup;
    private CanvasGroup _photoGroup;
    private CanvasGroup _paragraphsRootGroup;
    private readonly List<CanvasGroup> _paragraphLineGroups = new List<CanvasGroup>();
    private readonly List<CanvasGroup> _darkSegmentGroups = new List<CanvasGroup>();
    private readonly List<CanvasGroup> _creditsLineGroups = new List<CanvasGroup>();
    private CanvasGroup _chapterTitleGroup;
    private RectTransform _chapterTitleRect;
    private CanvasGroup _creditsRootGroup;
    private CanvasGroup _endPhotoGroup;
    private CanvasGroup _skipHintGroup;
    private TextMeshProUGUI _introTmp;
    private CanvasGroup _darkIntroRootGroup;

    private GameObject _canvasRoot;
    private Coroutine _routine;
    private bool _isPlaying;
    private float _savedTimeScale = 1f;
    private PlayerController2D _cachedPlayer;
    private bool _hadPlayerEnabled;
    private float _skipHoldTimer;
    private bool _skipJumpRequested;

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
            _skipHoldTimer = 0f;
            return;
        }

        if (skipHoldDurationSeconds <= 0f)
        {
            if (Input.GetKeyDown(skipKey))
            {
                _skipJumpRequested = true;
            }

            return;
        }

        if (Input.GetKey(skipKey))
        {
            _skipHoldTimer += Time.unscaledDeltaTime;
            if (_skipHoldTimer >= skipHoldDurationSeconds)
            {
                _skipHoldTimer = 0f;
                _skipJumpRequested = true;
            }
        }
        else
        {
            _skipHoldTimer = 0f;
        }
    }

    public void Play()
    {
        if (_isPlaying)
        {
            return;
        }

        if (loadSceneWhenFinished && string.IsNullOrWhiteSpace(nextSceneName))
        {
            Debug.LogWarning($"{nameof(LevelTransitionSequence)}: Next Scene Name is empty — 终章可不填以停留在结束画面。", this);
        }

        if (_canvasRoot == null)
        {
            BuildUi();
        }

        _routine = StartCoroutine(RunRoutine());
    }

    private IEnumerator RunRoutine()
    {
        _isPlaying = true;
        _skipJumpRequested = false;
        _skipHoldTimer = 0f;

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
                _hadPlayerEnabled = _cachedPlayer.enabled;
                var rb = _cachedPlayer.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.velocity = Vector2.zero;
                    rb.angularVelocity = 0f;
                }

                _cachedPlayer.enabled = false;
            }
        }

        ResetAllVisuals();
        _canvasRoot.SetActive(true);

        switch (kind)
        {
            case LevelTransitionKind.DarkIntroChapterTitle:
                yield return RunDarkIntroChapter();
                break;
            case LevelTransitionKind.PhotoWhiteParagraphsChapter:
                yield return RunPhotoChapter(paragraphWhite);
                break;
            case LevelTransitionKind.PhotoBlackParagraphsCreditsEndPhoto:
                yield return RunFinalEnding();
                break;
        }

        TeardownPlayerAndTime();
        _isPlaying = false;
        onBeforeLoadScene?.Invoke();

        if (loadSceneWhenFinished && !string.IsNullOrWhiteSpace(nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName);
        }
        else if (_canvasRoot != null)
        {
            _canvasRoot.SetActive(false);
        }

        _routine = null;
    }

    private IEnumerator RunDarkIntroChapter()
    {
        yield return FadeCanvasGroup(_blackFullGroup, 0f, 1f, darkenDuration);

        if (!_skipJumpRequested)
        {
            foreach (var cg in _darkSegmentGroups)
            {
                if (_skipJumpRequested)
                {
                    break;
                }

                if (cg != null)
                {
                    yield return FadeCanvasGroup(cg, 0f, 1f, darkSegmentFadeIn);
                    yield return WaitUnscaled(darkSegmentPause);
                }
            }
        }

        if (!_skipJumpRequested && _darkSegmentGroups.Count > 0 && _darkIntroRootGroup != null)
        {
            yield return FadeCanvasGroup(_darkIntroRootGroup, 1f, 0f, darkIntroFadeOutDuration);
        }
        else if (_darkIntroRootGroup != null)
        {
            _darkIntroRootGroup.alpha = 0f;
        }

        _skipJumpRequested = false;
        yield return RunChapterTitleZoomThenFade();
    }

    private IEnumerator RunPhotoChapter(Color paragraphColor)
    {
        yield return RunPhotoToBlackNoChapter(paragraphColor);
        _skipJumpRequested = false;
        yield return RunChapterTitleZoomThenFade();
    }

    /// <summary>白场照片段落 → 淡出白幕并淡入黑幕（不播章节标题）。终章用。</summary>
    private IEnumerator RunPhotoToBlackNoChapter(Color paragraphColor)
    {
        if (_skipJumpRequested)
        {
            HidePhotoFlowQuick();
            if (_whiteFullGroup != null)
            {
                _whiteFullGroup.alpha = 0f;
            }

            if (_blackFullGroup != null)
            {
                _blackFullGroup.alpha = 1f;
            }

            yield break;
        }

        yield return FadeCanvasGroup(_whiteFullGroup, 0f, 1f, brightenDuration);
        yield return FadeCanvasGroup(_introGroup, 0f, 1f, introFadeInDuration);
        yield return WaitUnscaled(introHoldDuration);

        if (_skipJumpRequested)
        {
            HidePhotoFlowQuick();
            if (_whiteFullGroup != null)
            {
                _whiteFullGroup.alpha = 0f;
            }

            if (_blackFullGroup != null)
            {
                _blackFullGroup.alpha = 1f;
            }

            yield break;
        }

        if (photoSprite != null && _photoGroup != null)
        {
            yield return RunIntroOutPhotoInParallel();
            yield return WaitUnscaled(holdPhotoDuration);
        }
        else
        {
            yield return FadeCanvasGroup(_introGroup, 1f, 0f, introFadeOutDuration);
        }

        if (_skipJumpRequested)
        {
            HidePhotoFlowQuick();
            if (_whiteFullGroup != null)
            {
                _whiteFullGroup.alpha = 0f;
            }

            if (_blackFullGroup != null)
            {
                _blackFullGroup.alpha = 1f;
            }

            yield break;
        }

        yield return WaitUnscaled(delayBeforeParagraphs);
        ApplyParagraphColor(paragraphColor);

        foreach (var cg in _paragraphLineGroups)
        {
            if (_skipJumpRequested)
            {
                break;
            }

            if (cg != null)
            {
                yield return FadeCanvasGroup(cg, 0f, 1f, paragraphFadeInDuration);
                yield return WaitUnscaled(pauseAfterEachParagraph);
            }
        }

        HidePhotoFlowQuick();

        if (!_skipJumpRequested)
        {
            yield return FadeWhiteOutThenBlackIn();
        }
        else
        {
            if (_whiteFullGroup != null)
            {
                _whiteFullGroup.alpha = 0f;
            }

            if (_blackFullGroup != null)
            {
                _blackFullGroup.alpha = 1f;
            }
        }
    }

    private IEnumerator FadeWhiteOutThenBlackIn()
    {
        if (_whiteFullGroup == null || _blackFullGroup == null)
        {
            yield break;
        }

        var d = Mathf.Max(0.01f, finalBlackFadeDuration);
        var t = 0f;
        while (t < d)
        {
            t += Time.unscaledDeltaTime;
            var u = Mathf.Clamp01(t / d);
            _whiteFullGroup.alpha = Mathf.Lerp(1f, 0f, u);
            _blackFullGroup.alpha = Mathf.Lerp(0f, 1f, u);
            yield return null;
        }

        _whiteFullGroup.alpha = 0f;
        _blackFullGroup.alpha = 1f;
    }

    private IEnumerator RunFinalEnding()
    {
        if (!_skipJumpRequested)
        {
            yield return RunPhotoToBlackNoChapter(paragraphBlack);
        }
        else
        {
            HidePhotoFlowQuick();
            if (_whiteFullGroup != null)
            {
                _whiteFullGroup.alpha = 0f;
            }

            if (_blackFullGroup != null)
            {
                _blackFullGroup.alpha = 1f;
            }
        }

        _skipJumpRequested = false;

        if (_chapterTitleGroup != null)
        {
            _chapterTitleGroup.alpha = 0f;
        }

        if (_creditsRootGroup != null)
        {
            _creditsRootGroup.alpha = 1f;
        }

        foreach (var cg in _creditsLineGroups)
        {
            if (_skipJumpRequested)
            {
                RevealAllCreditsLinesInstant();
                break;
            }

            if (cg != null)
            {
                yield return FadeCanvasGroup(cg, 0f, 1f, creditsSegmentFadeIn);
                yield return WaitUnscaled(creditsSegmentPause);
            }
        }

        if (_creditsRootGroup != null)
        {
            yield return FadeCanvasGroup(_creditsRootGroup, 1f, 0f, 0.5f);
        }

        _skipJumpRequested = false;

        if (endPhotoSprite != null && _endPhotoGroup != null)
        {
            yield return FadeCanvasGroup(_endPhotoGroup, 0f, 1f, endPhotoFadeInDuration);
            yield return WaitUnscaled(endPhotoHoldDuration);
        }
    }

    private void RevealAllCreditsLinesInstant()
    {
        foreach (var cg in _creditsLineGroups)
        {
            if (cg != null)
            {
                cg.alpha = 1f;
            }
        }
    }

    private void HidePhotoFlowQuick()
    {
        if (_introGroup != null)
        {
            _introGroup.alpha = 0f;
        }

        if (_photoGroup != null)
        {
            _photoGroup.alpha = 0f;
        }

        if (_paragraphsRootGroup != null)
        {
            _paragraphsRootGroup.alpha = 0f;
        }

        foreach (var cg in _paragraphLineGroups)
        {
            if (cg != null)
            {
                cg.alpha = 0f;
            }
        }

        if (_whiteFullGroup != null)
        {
            _whiteFullGroup.alpha = 0f;
        }
    }

    private void ApplyParagraphColor(Color c)
    {
        foreach (var cg in _paragraphLineGroups)
        {
            if (cg == null)
            {
                continue;
            }

            var tmp = cg.GetComponent<TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.color = c;
            }
        }
    }

    private IEnumerator RunIntroOutPhotoInParallel()
    {
        var introFrom = _introGroup != null ? _introGroup.alpha : 1f;
        var photoFrom = _photoGroup != null ? _photoGroup.alpha : 0f;
        var maxDur = Mathf.Max(introFadeOutDuration, photoFadeInDuration);
        if (maxDur <= 0f)
        {
            if (_introGroup != null)
            {
                _introGroup.alpha = 0f;
            }

            if (_photoGroup != null)
            {
                _photoGroup.alpha = 1f;
            }

            yield break;
        }

        var elapsed = 0f;
        while (elapsed < maxDur)
        {
            elapsed += Time.unscaledDeltaTime;
            if (_introGroup != null && introFadeOutDuration > 0f)
            {
                var ui = Mathf.Clamp01(elapsed / introFadeOutDuration);
                _introGroup.alpha = Mathf.Lerp(introFrom, 0f, ui);
            }
            else if (_introGroup != null)
            {
                _introGroup.alpha = 0f;
            }

            if (_photoGroup != null && photoFadeInDuration > 0f)
            {
                var up = Mathf.Clamp01(elapsed / photoFadeInDuration);
                _photoGroup.alpha = Mathf.Lerp(photoFrom, 1f, up);
            }
            else if (_photoGroup != null)
            {
                _photoGroup.alpha = 1f;
            }

            yield return null;
        }

        if (_introGroup != null)
        {
            _introGroup.alpha = 0f;
        }

        if (_photoGroup != null)
        {
            _photoGroup.alpha = 1f;
        }
    }

    private IEnumerator RunChapterTitleZoomThenFade()
    {
        if (_chapterTitleGroup == null || _chapterTitleRect == null)
        {
            yield break;
        }

        _chapterTitleGroup.alpha = 1f;
        _chapterTitleRect.localScale = Vector3.one * chapterTitleStartScale;

        var t = 0f;
        while (t < chapterGrowDuration)
        {
            t += Time.unscaledDeltaTime;
            var u = Mathf.Clamp01(t / Mathf.Max(0.0001f, chapterGrowDuration));
            var s = Mathf.Lerp(chapterTitleStartScale, chapterTitleEndScale, u);
            _chapterTitleRect.localScale = Vector3.one * s;
            yield return null;
        }

        _chapterTitleRect.localScale = Vector3.one * chapterTitleEndScale;
        yield return FadeCanvasGroup(_chapterTitleGroup, 1f, 0f, chapterTitleFadeOutDuration);
    }

    private void TeardownPlayerAndTime()
    {
        if (_cachedPlayer != null && disablePlayerWhilePlaying)
        {
            _cachedPlayer.enabled = _hadPlayerEnabled;
            _cachedPlayer = null;
        }

        if (freezeTimeDuringTransition)
        {
            Time.timeScale = _savedTimeScale;
        }
    }

    private void ResetAllVisuals()
    {
        if (_blackFullGroup != null)
        {
            _blackFullGroup.alpha = 0f;
        }

        if (_whiteFullGroup != null)
        {
            _whiteFullGroup.alpha = 0f;
        }

        if (_introGroup != null)
        {
            _introGroup.alpha = 0f;
        }

        if (_photoGroup != null)
        {
            _photoGroup.alpha = 0f;
        }

        if (_paragraphsRootGroup != null)
        {
            _paragraphsRootGroup.alpha = 1f;
        }

        foreach (var cg in _paragraphLineGroups)
        {
            if (cg != null)
            {
                cg.alpha = 0f;
            }
        }

        foreach (var cg in _darkSegmentGroups)
        {
            if (cg != null)
            {
                cg.alpha = 0f;
            }
        }

        if (_darkIntroRootGroup != null)
        {
            _darkIntroRootGroup.alpha = 1f;
        }

        if (_chapterTitleGroup != null)
        {
            _chapterTitleGroup.alpha = 0f;
        }

        if (_creditsRootGroup != null)
        {
            _creditsRootGroup.alpha = 0f;
        }

        foreach (var cg in _creditsLineGroups)
        {
            if (cg != null)
            {
                cg.alpha = 0f;
            }
        }

        if (_endPhotoGroup != null)
        {
            _endPhotoGroup.alpha = 0f;
        }

        if (_skipHintGroup != null)
        {
            _skipHintGroup.alpha = allowSkipWithSpace && showSkipHint ? 1f : 0f;
        }
    }

    private void BuildUi()
    {
        if (_canvasRoot != null)
        {
            return;
        }

        _canvasRoot = new GameObject("LevelTransitionSequenceCanvas");
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
        var rootCg = _canvasRoot.AddComponent<CanvasGroup>();
        rootCg.alpha = 1f;
        rootCg.blocksRaycasts = true;
        rootCg.interactable = false;

        _blackFullGroup = CreateFullScreenImage(_canvasRoot.transform, "BlackFull", Color.black, 0);

        _whiteFullGroup = CreateFullScreenImage(_canvasRoot.transform, "WhiteFull", Color.white, 0);

        var introGo = new GameObject("IntroOnWhite");
        introGo.transform.SetParent(_canvasRoot.transform, false);
        var introRt = introGo.AddComponent<RectTransform>();
        introRt.anchorMin = new Vector2(0.08f, 0.32f);
        introRt.anchorMax = new Vector2(0.92f, 0.68f);
        introRt.offsetMin = Vector2.zero;
        introRt.offsetMax = Vector2.zero;
        _introGroup = introGo.AddComponent<CanvasGroup>();
        _introGroup.alpha = 0f;

        var introChild = new GameObject("TMP");
        introChild.transform.SetParent(introGo.transform, false);
        var introChildRt = introChild.AddComponent<RectTransform>();
        introChildRt.anchorMin = Vector2.zero;
        introChildRt.anchorMax = Vector2.one;
        introChildRt.offsetMin = Vector2.zero;
        introChildRt.offsetMax = Vector2.zero;
        _introTmp = introChild.AddComponent<TextMeshProUGUI>();
        _introTmp.text = FormatMultiline(string.IsNullOrWhiteSpace(introMessage) ? DefaultPhotoIntro : introMessage);
        _introTmp.color = introOnWhiteColor;
        _introTmp.fontSize = 32f;
        _introTmp.alignment = TextAlignmentOptions.Center;
        _introTmp.enableWordWrapping = true;
        ApplyFont(_introTmp);

        var photoGo = new GameObject("Photo");
        photoGo.transform.SetParent(_canvasRoot.transform, false);
        var photoRt = photoGo.AddComponent<RectTransform>();
        photoRt.anchorMin = Vector2.zero;
        photoRt.anchorMax = Vector2.one;
        photoRt.offsetMin = Vector2.zero;
        photoRt.offsetMax = Vector2.zero;
        var photoImg = photoGo.AddComponent<Image>();
        photoImg.sprite = photoSprite;
        photoImg.preserveAspect = photoPreserveAspect;
        photoImg.color = Color.white;
        _photoGroup = photoGo.AddComponent<CanvasGroup>();
        _photoGroup.alpha = 0f;

        var paraRoot = new GameObject("Paragraphs");
        paraRoot.transform.SetParent(_canvasRoot.transform, false);
        var paraRt = paraRoot.AddComponent<RectTransform>();
        paraRt.anchorMin = new Vector2(0.04f, 0.1f);
        paraRt.anchorMax = new Vector2(0.48f, 0.82f);
        paraRt.offsetMin = Vector2.zero;
        paraRt.offsetMax = Vector2.zero;
        var vlg = paraRoot.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 18f;
        vlg.padding = new RectOffset(8, 8, 4, 4);
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        _paragraphsRootGroup = paraRoot.AddComponent<CanvasGroup>();
        _paragraphsRootGroup.alpha = 1f;

        _paragraphLineGroups.Clear();
        var estW = scaler.referenceResolution.x * 0.44f - 24f;
        var paras = transitionParagraphs != null ? transitionParagraphs : System.Array.Empty<string>();
        foreach (var p in paras)
        {
            var formatted = FormatMultiline(p);
            if (string.IsNullOrWhiteSpace(formatted))
            {
                continue;
            }

            var row = new GameObject("P");
            row.transform.SetParent(paraRoot.transform, false);
            var le = row.AddComponent<LayoutElement>();
            var tmp = row.AddComponent<TextMeshProUGUI>();
            tmp.text = formatted;
            tmp.color = paragraphWhite;
            tmp.fontSize = bodyFontSize;
            tmp.alignment = TextAlignmentOptions.TopLeft;
            tmp.enableWordWrapping = true;
            ApplyFont(tmp);
            tmp.ForceMeshUpdate();
            le.preferredHeight = Mathf.Max(tmp.GetPreferredValues(estW, 0f).y + 8f, 36f);
            var cg = row.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            _paragraphLineGroups.Add(cg);
        }

        var darkRoot = new GameObject("DarkIntro");
        darkRoot.transform.SetParent(_canvasRoot.transform, false);
        var darkRt = darkRoot.AddComponent<RectTransform>();
        darkRt.anchorMin = new Vector2(0.06f, 0.2f);
        darkRt.anchorMax = new Vector2(0.94f, 0.8f);
        darkRt.offsetMin = Vector2.zero;
        darkRt.offsetMax = Vector2.zero;
        var darkRootCg = darkRoot.AddComponent<CanvasGroup>();
        darkRootCg.alpha = 1f;
        _darkIntroRootGroup = darkRootCg;
        var darkVlg = darkRoot.AddComponent<VerticalLayoutGroup>();
        darkVlg.spacing = 22f;
        darkVlg.childAlignment = TextAnchor.MiddleCenter;
        darkVlg.childControlWidth = true;
        darkVlg.childControlHeight = true;
        darkVlg.childForceExpandWidth = true;
        darkVlg.childForceExpandHeight = false;

        _darkSegmentGroups.Clear();
        var segs = darkIntroSegments != null && darkIntroSegments.Length > 0
            ? darkIntroSegments
            : new[] { DefaultDarkIntro };
        foreach (var seg in segs)
        {
            var f = FormatMultiline(seg);
            if (string.IsNullOrWhiteSpace(f))
            {
                continue;
            }

            var row = new GameObject("DarkSeg");
            row.transform.SetParent(darkRoot.transform, false);
            var le = row.AddComponent<LayoutElement>();
            var tmp = row.AddComponent<TextMeshProUGUI>();
            tmp.text = f;
            tmp.color = darkIntroTextColor;
            tmp.fontSize = bodyFontSize + 2f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = true;
            ApplyFont(tmp);
            tmp.ForceMeshUpdate();
            le.preferredHeight = Mathf.Max(tmp.GetPreferredValues(estW, 0f).y + 8f, 40f);
            var cg = row.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            _darkSegmentGroups.Add(cg);
        }

        var chGo = new GameObject("ChapterTitle");
        chGo.transform.SetParent(_canvasRoot.transform, false);
        _chapterTitleRect = chGo.AddComponent<RectTransform>();
        _chapterTitleRect.anchorMin = new Vector2(0.5f, 0.5f);
        _chapterTitleRect.anchorMax = new Vector2(0.5f, 0.5f);
        _chapterTitleRect.pivot = new Vector2(0.5f, 0.5f);
        _chapterTitleRect.sizeDelta = new Vector2(1700f, 200f);
        _chapterTitleGroup = chGo.AddComponent<CanvasGroup>();
        _chapterTitleGroup.alpha = 0f;
        var chTmp = chGo.AddComponent<TextMeshProUGUI>();
        chTmp.text = chapterTitleText;
        chTmp.color = chapterTitleColor;
        chTmp.fontSize = chapterTitleFontSize;
        chTmp.alignment = TextAlignmentOptions.Center;
        chTmp.enableWordWrapping = true;
        ApplyFont(chTmp);

        var credRoot = new GameObject("EndCredits");
        credRoot.transform.SetParent(_canvasRoot.transform, false);
        var credRt = credRoot.AddComponent<RectTransform>();
        credRt.anchorMin = new Vector2(0.08f, 0.25f);
        credRt.anchorMax = new Vector2(0.92f, 0.75f);
        credRt.offsetMin = Vector2.zero;
        credRt.offsetMax = Vector2.zero;
        _creditsRootGroup = credRoot.AddComponent<CanvasGroup>();
        _creditsRootGroup.alpha = 0f;
        var credVlg = credRoot.AddComponent<VerticalLayoutGroup>();
        credVlg.spacing = 16f;
        credVlg.childAlignment = TextAnchor.MiddleCenter;

        _creditsLineGroups.Clear();
        foreach (var line in endCreditsSegments)
        {
            var f = FormatMultiline(line);
            if (string.IsNullOrWhiteSpace(f))
            {
                continue;
            }

            var row = new GameObject("CredLine");
            row.transform.SetParent(credRoot.transform, false);
            var le = row.AddComponent<LayoutElement>();
            var tmp = row.AddComponent<TextMeshProUGUI>();
            tmp.text = f;
            tmp.color = creditsTextColor;
            tmp.fontSize = bodyFontSize + 4f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = true;
            ApplyFont(tmp);
            tmp.ForceMeshUpdate();
            le.preferredHeight = Mathf.Max(tmp.GetPreferredValues(estW, 0f).y + 6f, 36f);
            var cg = row.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            _creditsLineGroups.Add(cg);
        }

        var endPh = new GameObject("EndPhoto");
        endPh.transform.SetParent(_canvasRoot.transform, false);
        var endRt = endPh.AddComponent<RectTransform>();
        endRt.anchorMin = Vector2.zero;
        endRt.anchorMax = Vector2.one;
        endRt.offsetMin = Vector2.zero;
        endRt.offsetMax = Vector2.zero;
        var endImg = endPh.AddComponent<Image>();
        endImg.sprite = endPhotoSprite;
        endImg.preserveAspect = photoPreserveAspect;
        endImg.color = Color.white;
        _endPhotoGroup = endPh.AddComponent<CanvasGroup>();
        _endPhotoGroup.alpha = 0f;

        var skipGo = new GameObject("SkipHint");
        skipGo.transform.SetParent(_canvasRoot.transform, false);
        var skipRt = skipGo.AddComponent<RectTransform>();
        skipRt.anchorMin = new Vector2(0.5f, 0.05f);
        skipRt.anchorMax = new Vector2(0.5f, 0.05f);
        skipRt.pivot = new Vector2(0.5f, 0f);
        skipRt.sizeDelta = new Vector2(900f, 40f);
        var skipTmp = skipGo.AddComponent<TextMeshProUGUI>();
        skipTmp.text = string.IsNullOrWhiteSpace(skipHintText) ? "Press spacebar to skip" : skipHintText;
        skipTmp.color = new Color(1f, 1f, 1f, 0.55f);
        skipTmp.fontSize = skipHintFontSize;
        skipTmp.alignment = TextAlignmentOptions.Bottom;
        ApplyFont(skipTmp);
        _skipHintGroup = skipGo.AddComponent<CanvasGroup>();
        _skipHintGroup.alpha = 0f;
    }

    private static CanvasGroup CreateFullScreenImage(Transform parent, string name, Color c, float startAlpha)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.color = c;
        img.raycastTarget = false;
        var cg = go.AddComponent<CanvasGroup>();
        cg.alpha = startAlpha;
        return cg;
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
        }
        else if (TMP_Settings.defaultFontAsset != null)
        {
            tmp.font = TMP_Settings.defaultFontAsset;
        }
    }

    private static string FormatMultiline(string raw)
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
            _cachedPlayer.enabled = _hadPlayerEnabled;
        }
    }
}
