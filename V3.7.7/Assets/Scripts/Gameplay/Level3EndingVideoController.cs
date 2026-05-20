using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

[DisallowMultipleComponent]
public class Level3EndingVideoController : MonoBehaviour
{
    private const string DefaultIntroMessage =
        "Hey...\nYou made it this far.\nDo you remember now?\nThe light you were chasing.\nCome with me.\nLet's watch it rise-together.";

    [Header("Content")]
    [TextArea(3, 8)]
    [SerializeField] private string introMessage = DefaultIntroMessage;
    [SerializeField] private Sprite introPhotoSprite;
    [SerializeField] private string[] whiteParagraphs = Array.Empty<string>();
    [SerializeField] private string[] creditsSegments = Array.Empty<string>();
    [SerializeField] private Sprite endPhotoSprite;
    [SerializeField] private string videoRelativePath = "Videos/level3_ending.mp4";
    [SerializeField] private TMP_FontAsset fontOverride;

    [Header("Timing (unscaled)")]
    [SerializeField] private float brightenDuration = 1.35f;
    [SerializeField] private float introFadeInDuration = 0.9f;
    [SerializeField] private float introHoldDuration = 5f;
    [SerializeField] private float introFadeOutDuration = 0.75f;
    [SerializeField] private float photoFadeInDuration = 1.1f;
    [SerializeField] private float holdPhotoDuration = 2f;
    [SerializeField] private float delayBeforeParagraphs = 0.25f;
    [SerializeField] private float paragraphFadeInDuration = 1.05f;
    [SerializeField] private float pauseAfterEachParagraph = 3f;
    [SerializeField] private float photoFlowFadeToBlackDuration = 1f;
    [SerializeField] private float blackBufferBeforeVideo = 0.15f;
    [SerializeField] private float videoFadeInDuration = 0.8f;
    [SerializeField] private float videoFadeOutDuration = 0.8f;
    [SerializeField] private float blackBufferAfterVideo = 0.15f;
    [SerializeField] private float creditsSegmentFadeIn = 1f;
    [SerializeField] private float creditsSegmentPause = 3f;
    [SerializeField] private float creditsFadeOutDuration = 0.5f;
    [SerializeField] private float endPhotoFadeInDuration = 1f;
    [SerializeField] private float videoPrepareTimeoutSeconds = 10f;

    [Header("Layout")]
    [SerializeField] private bool photoPreserveAspect;
    [SerializeField] private float paragraphsAnchorXMin = 0.04f;
    [SerializeField] private float paragraphsAnchorXMax = 0.48f;
    [SerializeField] private float paragraphsAnchorYMin = 0.12f;
    [SerializeField] private float paragraphsAnchorYMax = 0.78f;
    [SerializeField] private float creditsTopOffset = 50f;
    [SerializeField] private int canvasSortOrder = 620;

    [Header("Typography")]
    [SerializeField] private float introFontSize = 32f;
    [SerializeField] private float paragraphFontSize = 32f;
    [SerializeField] private float creditsFontSize = 32f;
    [SerializeField] private float creditsLineSpacing = 50f;
    [SerializeField] private Color introTextColor = new Color(0.12f, 0.12f, 0.14f, 1f);
    [SerializeField] private Color paragraphTextColor = new Color(0.8037735f, 0.15154569f, 0.09554285f, 1f);
    [SerializeField] private Color creditsTextColor = Color.white;

    [Header("Video Skip")]
    [SerializeField] private bool allowVideoSkip = true;
    [SerializeField] private KeyCode skipPrimaryKey = KeyCode.Space;
    [SerializeField] private KeyCode skipSecondaryKey = KeyCode.Escape;
    [SerializeField] private string skipHintText = "Press Space or Esc to skip video";
    [SerializeField] private float skipHintFontSize = 18f;
    [SerializeField] private Color skipHintColor = new Color(1f, 1f, 1f, 0.55f);

    private readonly List<CanvasGroup> _paragraphGroups = new List<CanvasGroup>();
    private readonly List<CanvasGroup> _creditsGroups = new List<CanvasGroup>();

    private GameObject _canvasRoot;
    private CanvasGroup _blackGroup;
    private CanvasGroup _whiteGroup;
    private CanvasGroup _introGroup;
    private CanvasGroup _photoGroup;
    private CanvasGroup _paragraphsRootGroup;
    private CanvasGroup _videoGroup;
    private CanvasGroup _creditsRootGroup;
    private CanvasGroup _endPhotoGroup;
    private CanvasGroup _skipHintGroup;
    private RawImage _videoRawImage;
    private AspectRatioFitter _videoAspectFitter;
    private VideoPlayer _videoPlayer;
    private RenderTexture _videoTexture;
    private Coroutine _routine;
    private bool _acceptVideoSkip;
    private bool _skipVideoRequested;
    private bool _videoPreparationStarted;
    private bool _videoPrepared;
    private bool _videoPreparationFailed;
    private bool _videoLoopReached;
    private string _videoPreparationError;
    private string _resolvedVideoPath;

    private void Awake()
    {
        BuildUi();
        ConfigureVideoPlayer();
        BeginVideoPreparationIfPossible();
        ResetVisualState();
    }

    private void Start()
    {
        if (_routine == null)
        {
            _routine = StartCoroutine(PlayRoutine());
        }
    }

    private void Update()
    {
        if (!_acceptVideoSkip || !allowVideoSkip)
        {
            return;
        }

        if (Input.GetKeyDown(skipPrimaryKey) || Input.GetKeyDown(skipSecondaryKey))
        {
            _skipVideoRequested = true;
        }
    }

    private IEnumerator PlayRoutine()
    {
        ResetVisualState();

        yield return FadeCanvasGroup(_whiteGroup, 0f, 1f, brightenDuration);
        yield return FadeCanvasGroup(_introGroup, 0f, 1f, introFadeInDuration);
        yield return WaitUnscaled(introHoldDuration);

        if (introPhotoSprite != null && _photoGroup != null)
        {
            yield return RunIntroOutAndPhotoInParallel();
            yield return WaitUnscaled(holdPhotoDuration);
        }
        else
        {
            yield return FadeCanvasGroup(_introGroup, _introGroup != null ? _introGroup.alpha : 1f, 0f, introFadeOutDuration);
        }

        yield return WaitUnscaled(delayBeforeParagraphs);

        foreach (var paragraphGroup in _paragraphGroups)
        {
            if (paragraphGroup == null)
            {
                continue;
            }

            yield return FadeCanvasGroup(paragraphGroup, 0f, 1f, paragraphFadeInDuration);
            yield return WaitUnscaled(pauseAfterEachParagraph);
        }

        yield return FadePhotoFlowToBlack();
        yield return WaitUnscaled(blackBufferBeforeVideo);
        yield return RunVideoSegmentIfAvailable();
        yield return WaitUnscaled(blackBufferAfterVideo);
        yield return RunCredits();

        if (_creditsRootGroup != null)
        {
            yield return FadeCanvasGroup(_creditsRootGroup, _creditsRootGroup.alpha, 0f, creditsFadeOutDuration);
        }

        if (_endPhotoGroup != null && endPhotoSprite != null)
        {
            yield return FadeCanvasGroup(_endPhotoGroup, 0f, 1f, endPhotoFadeInDuration);
        }

        _routine = null;
    }

    private IEnumerator RunIntroOutAndPhotoInParallel()
    {
        var introFrom = _introGroup != null ? _introGroup.alpha : 1f;
        var photoFrom = _photoGroup != null ? _photoGroup.alpha : 0f;
        var duration = Mathf.Max(introFadeOutDuration, photoFadeInDuration);

        if (duration <= 0f)
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
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            if (_introGroup != null)
            {
                var introU = introFadeOutDuration > 0f ? Mathf.Clamp01(elapsed / introFadeOutDuration) : 1f;
                _introGroup.alpha = Mathf.Lerp(introFrom, 0f, introU);
            }

            if (_photoGroup != null)
            {
                var photoU = photoFadeInDuration > 0f ? Mathf.Clamp01(elapsed / photoFadeInDuration) : 1f;
                _photoGroup.alpha = Mathf.Lerp(photoFrom, 1f, photoU);
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

    private IEnumerator FadePhotoFlowToBlack()
    {
        var duration = Mathf.Max(0f, photoFlowFadeToBlackDuration);
        if (duration <= 0f)
        {
            SetPhotoFlowAlpha(0f);
            yield break;
        }

        var whiteFrom = _whiteGroup != null ? _whiteGroup.alpha : 0f;
        var photoFrom = _photoGroup != null ? _photoGroup.alpha : 0f;
        var paragraphsFrom = _paragraphsRootGroup != null ? _paragraphsRootGroup.alpha : 0f;
        var introFrom = _introGroup != null ? _introGroup.alpha : 0f;

        var elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            var u = Mathf.Clamp01(elapsed / duration);

            if (_whiteGroup != null)
            {
                _whiteGroup.alpha = Mathf.Lerp(whiteFrom, 0f, u);
            }

            if (_photoGroup != null)
            {
                _photoGroup.alpha = Mathf.Lerp(photoFrom, 0f, u);
            }

            if (_paragraphsRootGroup != null)
            {
                _paragraphsRootGroup.alpha = Mathf.Lerp(paragraphsFrom, 0f, u);
            }

            if (_introGroup != null)
            {
                _introGroup.alpha = Mathf.Lerp(introFrom, 0f, u);
            }

            yield return null;
        }

        SetPhotoFlowAlpha(0f);
    }

    private IEnumerator RunVideoSegmentIfAvailable()
    {
        yield return WaitForPreparedVideo();

        if (_videoPlayer == null || !_videoPrepared || _videoPreparationFailed)
        {
            yield break;
        }

        _skipVideoRequested = false;
        _videoLoopReached = false;

        if (_skipHintGroup != null)
        {
            _skipHintGroup.alpha = allowVideoSkip ? 1f : 0f;
        }

        try
        {
            _videoPlayer.Stop();
            _videoPlayer.frame = 0;
        }
        catch
        {
        }

        _videoPlayer.Play();
        yield return FadeCanvasGroup(_videoGroup, 0f, 1f, videoFadeInDuration);

        _acceptVideoSkip = allowVideoSkip;
        while (!_skipVideoRequested && !_videoLoopReached && !_videoPreparationFailed)
        {
            if (_videoPlayer != null && _videoPlayer.isPrepared && _videoPlayer.length > 0d && _videoPlayer.time >= _videoPlayer.length - 0.05d)
            {
                break;
            }

            yield return null;
        }

        _acceptVideoSkip = false;

        if (_skipHintGroup != null)
        {
            yield return FadeCanvasGroup(_skipHintGroup, _skipHintGroup.alpha, 0f, 0.15f);
        }

        yield return FadeCanvasGroup(_videoGroup, _videoGroup.alpha, 0f, videoFadeOutDuration);

        if (_videoPlayer != null)
        {
            _videoPlayer.Stop();
        }
    }

    private IEnumerator RunCredits()
    {
        if (_creditsRootGroup != null)
        {
            _creditsRootGroup.alpha = 1f;
        }

        foreach (var creditGroup in _creditsGroups)
        {
            if (creditGroup == null)
            {
                continue;
            }

            yield return FadeCanvasGroup(creditGroup, 0f, 1f, creditsSegmentFadeIn);
            yield return WaitUnscaled(creditsSegmentPause);
        }
    }

    private IEnumerator WaitForPreparedVideo()
    {
        if (_videoPreparationFailed)
        {
            yield break;
        }

        if (_videoPrepared)
        {
            yield break;
        }

        if (!_videoPreparationStarted)
        {
            BeginVideoPreparationIfPossible();
        }

        if (_videoPreparationFailed)
        {
            yield break;
        }

        var timeout = Mathf.Max(0.1f, videoPrepareTimeoutSeconds);
        var elapsed = 0f;
        while (!_videoPrepared && !_videoPreparationFailed && elapsed < timeout)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (_videoPrepared || _videoPreparationFailed)
        {
            yield break;
        }

        _videoPreparationFailed = true;
        _videoPreparationError = "prepare timed out after " + timeout.ToString("0.##") + " seconds";
        Debug.LogWarning(nameof(Level3EndingVideoController) + " skipped the video because " + _videoPreparationError + ". Expected path: '" + _resolvedVideoPath + "'.", this);
    }
    private void BuildUi()
    {
        if (_canvasRoot != null)
        {
            return;
        }

        _canvasRoot = new GameObject("Level3EndingVideoCanvas");
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

        var rootGroup = _canvasRoot.AddComponent<CanvasGroup>();
        rootGroup.alpha = 1f;
        rootGroup.blocksRaycasts = true;
        rootGroup.interactable = false;

        _blackGroup = CreateFullScreenImage(_canvasRoot.transform, "Black", Color.black);
        _blackGroup.alpha = 1f;

        _whiteGroup = CreateFullScreenImage(_canvasRoot.transform, "White", Color.white);
        _whiteGroup.alpha = 0f;

        var introRoot = new GameObject("Intro");
        introRoot.transform.SetParent(_canvasRoot.transform, false);
        var introRect = introRoot.AddComponent<RectTransform>();
        introRect.anchorMin = new Vector2(0.08f, 0.32f);
        introRect.anchorMax = new Vector2(0.92f, 0.68f);
        introRect.offsetMin = Vector2.zero;
        introRect.offsetMax = Vector2.zero;
        _introGroup = introRoot.AddComponent<CanvasGroup>();
        _introGroup.alpha = 0f;

        var introTextGo = new GameObject("Text");
        introTextGo.transform.SetParent(introRoot.transform, false);
        var introTextRect = introTextGo.AddComponent<RectTransform>();
        introTextRect.anchorMin = Vector2.zero;
        introTextRect.anchorMax = Vector2.one;
        introTextRect.offsetMin = Vector2.zero;
        introTextRect.offsetMax = Vector2.zero;
        var introTmp = introTextGo.AddComponent<TextMeshProUGUI>();
        introTmp.text = FormatMultiline(introMessage);
        introTmp.color = introTextColor;
        introTmp.fontSize = introFontSize;
        introTmp.alignment = TextAlignmentOptions.Center;
        introTmp.enableWordWrapping = true;
        introTmp.overflowMode = TextOverflowModes.Overflow;
        ApplyFont(introTmp);

        var photoGo = new GameObject("IntroPhoto");
        photoGo.transform.SetParent(_canvasRoot.transform, false);
        var photoRect = photoGo.AddComponent<RectTransform>();
        photoRect.anchorMin = Vector2.zero;
        photoRect.anchorMax = Vector2.one;
        photoRect.offsetMin = Vector2.zero;
        photoRect.offsetMax = Vector2.zero;
        var photoImage = photoGo.AddComponent<Image>();
        photoImage.sprite = introPhotoSprite;
        photoImage.preserveAspect = photoPreserveAspect;
        photoImage.color = Color.white;
        photoImage.raycastTarget = false;
        _photoGroup = photoGo.AddComponent<CanvasGroup>();
        _photoGroup.alpha = 0f;

        var paragraphRoot = new GameObject("Paragraphs");
        paragraphRoot.transform.SetParent(_canvasRoot.transform, false);
        var paragraphRect = paragraphRoot.AddComponent<RectTransform>();
        paragraphRect.anchorMin = new Vector2(paragraphsAnchorXMin, paragraphsAnchorYMin);
        paragraphRect.anchorMax = new Vector2(paragraphsAnchorXMax, paragraphsAnchorYMax);
        paragraphRect.offsetMin = Vector2.zero;
        paragraphRect.offsetMax = Vector2.zero;
        var paragraphLayout = paragraphRoot.AddComponent<VerticalLayoutGroup>();
        paragraphLayout.spacing = 18f;
        paragraphLayout.padding = new RectOffset(8, 8, 4, 4);
        paragraphLayout.childAlignment = TextAnchor.UpperLeft;
        paragraphLayout.childControlWidth = true;
        paragraphLayout.childControlHeight = true;
        paragraphLayout.childForceExpandWidth = true;
        paragraphLayout.childForceExpandHeight = false;
        _paragraphsRootGroup = paragraphRoot.AddComponent<CanvasGroup>();
        _paragraphsRootGroup.alpha = 1f;

        _paragraphGroups.Clear();
        var estimatedParagraphWidth = scaler.referenceResolution.x * Mathf.Max(0.05f, paragraphsAnchorXMax - paragraphsAnchorXMin) - 24f;
        foreach (var paragraph in whiteParagraphs ?? Array.Empty<string>())
        {
            var formattedParagraph = FormatMultiline(paragraph);
            if (string.IsNullOrWhiteSpace(formattedParagraph))
            {
                continue;
            }

            var row = new GameObject("Paragraph");
            row.transform.SetParent(paragraphRoot.transform, false);
            var layoutElement = row.AddComponent<LayoutElement>();
            var paragraphTmp = row.AddComponent<TextMeshProUGUI>();
            paragraphTmp.text = formattedParagraph;
            paragraphTmp.color = paragraphTextColor;
            paragraphTmp.fontSize = paragraphFontSize;
            paragraphTmp.alignment = TextAlignmentOptions.TopLeft;
            paragraphTmp.enableWordWrapping = true;
            paragraphTmp.overflowMode = TextOverflowModes.Overflow;
            ApplyFont(paragraphTmp);
            paragraphTmp.ForceMeshUpdate();
            layoutElement.preferredHeight = Mathf.Max(paragraphTmp.GetPreferredValues(estimatedParagraphWidth, 0f).y + 8f, 36f);
            var group = row.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            _paragraphGroups.Add(group);
        }

        var videoRoot = new GameObject("VideoRoot");
        videoRoot.transform.SetParent(_canvasRoot.transform, false);
        var videoRootRect = videoRoot.AddComponent<RectTransform>();
        videoRootRect.anchorMin = Vector2.zero;
        videoRootRect.anchorMax = Vector2.one;
        videoRootRect.offsetMin = Vector2.zero;
        videoRootRect.offsetMax = Vector2.zero;
        _videoGroup = videoRoot.AddComponent<CanvasGroup>();
        _videoGroup.alpha = 0f;

        var videoBackground = videoRoot.AddComponent<Image>();
        videoBackground.color = Color.black;
        videoBackground.raycastTarget = false;

        var rawImageGo = new GameObject("Video");
        rawImageGo.transform.SetParent(videoRoot.transform, false);
        var rawImageRect = rawImageGo.AddComponent<RectTransform>();
        rawImageRect.anchorMin = Vector2.zero;
        rawImageRect.anchorMax = Vector2.one;
        rawImageRect.offsetMin = Vector2.zero;
        rawImageRect.offsetMax = Vector2.zero;
        _videoRawImage = rawImageGo.AddComponent<RawImage>();
        _videoRawImage.color = Color.white;
        _videoRawImage.raycastTarget = false;
        _videoAspectFitter = rawImageGo.AddComponent<AspectRatioFitter>();
        _videoAspectFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        _videoAspectFitter.aspectRatio = 16f / 9f;

        var creditsRoot = new GameObject("Credits");
        creditsRoot.transform.SetParent(_canvasRoot.transform, false);
        var creditsRect = creditsRoot.AddComponent<RectTransform>();
        creditsRect.anchorMin = new Vector2(0.08f, 0f);
        creditsRect.anchorMax = new Vector2(0.92f, 1f);
        creditsRect.offsetMin = Vector2.zero;
        creditsRect.offsetMax = new Vector2(0f, -creditsTopOffset);
        var creditsLayout = creditsRoot.AddComponent<VerticalLayoutGroup>();
        creditsLayout.spacing = creditsLineSpacing;
        creditsLayout.childAlignment = TextAnchor.UpperCenter;
        creditsLayout.childControlWidth = true;
        creditsLayout.childControlHeight = true;
        creditsLayout.childForceExpandWidth = true;
        creditsLayout.childForceExpandHeight = false;
        _creditsRootGroup = creditsRoot.AddComponent<CanvasGroup>();
        _creditsRootGroup.alpha = 1f;

        _creditsGroups.Clear();
        var estimatedCreditsWidth = scaler.referenceResolution.x * 0.84f;
        foreach (var credit in creditsSegments ?? Array.Empty<string>())
        {
            var formattedCredit = FormatMultiline(credit);
            if (string.IsNullOrWhiteSpace(formattedCredit))
            {
                continue;
            }

            var row = new GameObject("Credit");
            row.transform.SetParent(creditsRoot.transform, false);
            var layoutElement = row.AddComponent<LayoutElement>();
            var creditTmp = row.AddComponent<TextMeshProUGUI>();
            creditTmp.text = formattedCredit;
            creditTmp.color = creditsTextColor;
            creditTmp.fontSize = creditsFontSize;
            creditTmp.alignment = TextAlignmentOptions.Center;
            creditTmp.enableWordWrapping = true;
            creditTmp.overflowMode = TextOverflowModes.Overflow;
            ApplyFont(creditTmp);
            creditTmp.ForceMeshUpdate();
            layoutElement.preferredHeight = Mathf.Max(creditTmp.GetPreferredValues(estimatedCreditsWidth, 0f).y + 6f, 36f);
            var group = row.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            _creditsGroups.Add(group);
        }

        var endPhotoGo = new GameObject("EndPhoto");
        endPhotoGo.transform.SetParent(_canvasRoot.transform, false);
        var endPhotoRect = endPhotoGo.AddComponent<RectTransform>();
        endPhotoRect.anchorMin = Vector2.zero;
        endPhotoRect.anchorMax = Vector2.one;
        endPhotoRect.offsetMin = Vector2.zero;
        endPhotoRect.offsetMax = Vector2.zero;
        var endPhotoImage = endPhotoGo.AddComponent<Image>();
        endPhotoImage.sprite = endPhotoSprite;
        endPhotoImage.preserveAspect = photoPreserveAspect;
        endPhotoImage.color = Color.white;
        endPhotoImage.raycastTarget = false;
        _endPhotoGroup = endPhotoGo.AddComponent<CanvasGroup>();
        _endPhotoGroup.alpha = 0f;

        var skipHintGo = new GameObject("SkipHint");
        skipHintGo.transform.SetParent(_canvasRoot.transform, false);
        var skipHintRect = skipHintGo.AddComponent<RectTransform>();
        skipHintRect.anchorMin = new Vector2(0.5f, 0.05f);
        skipHintRect.anchorMax = new Vector2(0.5f, 0.05f);
        skipHintRect.pivot = new Vector2(0.5f, 0f);
        skipHintRect.sizeDelta = new Vector2(920f, 44f);
        var skipHintTmp = skipHintGo.AddComponent<TextMeshProUGUI>();
        skipHintTmp.text = string.IsNullOrWhiteSpace(skipHintText) ? "Press Space or Esc to skip video" : skipHintText;
        skipHintTmp.color = skipHintColor;
        skipHintTmp.fontSize = skipHintFontSize;
        skipHintTmp.alignment = TextAlignmentOptions.Bottom;
        skipHintTmp.enableWordWrapping = true;
        skipHintTmp.overflowMode = TextOverflowModes.Overflow;
        ApplyFont(skipHintTmp);
        _skipHintGroup = skipHintGo.AddComponent<CanvasGroup>();
        _skipHintGroup.alpha = 0f;
    }

    private void ConfigureVideoPlayer()
    {
        if (_videoPlayer != null)
        {
            return;
        }

        _videoPlayer = gameObject.AddComponent<VideoPlayer>();
        _videoPlayer.playOnAwake = false;
        _videoPlayer.waitForFirstFrame = true;
        _videoPlayer.skipOnDrop = false;
        _videoPlayer.isLooping = false;
        _videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        _videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
        _videoPlayer.prepareCompleted += OnVideoPrepareCompleted;
        _videoPlayer.loopPointReached += OnVideoLoopPointReached;
        _videoPlayer.errorReceived += OnVideoErrorReceived;
    }

    private void BeginVideoPreparationIfPossible()
    {
        if (_videoPreparationStarted || _videoPreparationFailed || _videoPrepared || _videoPlayer == null)
        {
            return;
        }

        _resolvedVideoPath = ResolveVideoPath();
        if (string.IsNullOrWhiteSpace(_resolvedVideoPath) || !File.Exists(_resolvedVideoPath))
        {
            _videoPreparationFailed = true;
            _videoPreparationError = string.IsNullOrWhiteSpace(_resolvedVideoPath)
                ? "video path is empty"
                : "video file was not found at '" + _resolvedVideoPath + "'";
            Debug.LogWarning(nameof(Level3EndingVideoController) + " skipped the video because " + _videoPreparationError + ".", this);
            return;
        }

        _videoPreparationStarted = true;
        _videoPrepared = false;
        _videoPreparationFailed = false;
        _videoPreparationError = null;
        _videoLoopReached = false;

        _videoPlayer.source = VideoSource.Url;
        _videoPlayer.url = _resolvedVideoPath;

        try
        {
            _videoPlayer.Prepare();
        }
        catch (Exception exception)
        {
            _videoPreparationFailed = true;
            _videoPreparationError = exception.Message;
            Debug.LogWarning(nameof(Level3EndingVideoController) + " skipped the video because prepare threw an exception. " + exception.Message, this);
        }
    }

    private string ResolveVideoPath()
    {
        if (string.IsNullOrWhiteSpace(videoRelativePath))
        {
            return string.Empty;
        }

        var normalized = videoRelativePath.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
        return Path.GetFullPath(Path.Combine(Application.streamingAssetsPath, normalized));
    }

    private void OnVideoPrepareCompleted(VideoPlayer source)
    {
        _videoPrepared = true;
        _videoPreparationFailed = false;
        _videoPreparationError = null;
        UpdateVideoTargetTexture();
    }

    private void OnVideoLoopPointReached(VideoPlayer source)
    {
        _videoLoopReached = true;
    }

    private void OnVideoErrorReceived(VideoPlayer source, string message)
    {
        _videoPreparationFailed = true;
        _videoPreparationError = message;
        _videoLoopReached = true;
        Debug.LogWarning(nameof(Level3EndingVideoController) + " video playback failed and the video segment was skipped. " + message, this);
    }

    private void UpdateVideoTargetTexture()
    {
        if (_videoPlayer == null || _videoRawImage == null)
        {
            return;
        }

        var sourceTexture = _videoPlayer.texture;
        var width = sourceTexture != null && sourceTexture.width > 0 ? sourceTexture.width : 1920;
        var height = sourceTexture != null && sourceTexture.height > 0 ? sourceTexture.height : 1080;

        if (_videoTexture != null && (_videoTexture.width != width || _videoTexture.height != height))
        {
            _videoTexture.Release();
            Destroy(_videoTexture);
            _videoTexture = null;
        }

        if (_videoTexture == null)
        {
            _videoTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
            {
                name = "Level3EndingVideo"
            };
            _videoTexture.Create();
        }

        _videoPlayer.targetTexture = _videoTexture;
        _videoRawImage.texture = _videoTexture;

        if (_videoAspectFitter != null && height > 0)
        {
            _videoAspectFitter.aspectRatio = (float)width / height;
        }
    }

    private void ResetVisualState()
    {
        if (_blackGroup != null)
        {
            _blackGroup.alpha = 1f;
        }

        if (_whiteGroup != null)
        {
            _whiteGroup.alpha = 0f;
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

        foreach (var paragraphGroup in _paragraphGroups)
        {
            if (paragraphGroup != null)
            {
                paragraphGroup.alpha = 0f;
            }
        }

        if (_videoGroup != null)
        {
            _videoGroup.alpha = 0f;
        }

        if (_creditsRootGroup != null)
        {
            _creditsRootGroup.alpha = 1f;
        }

        foreach (var creditGroup in _creditsGroups)
        {
            if (creditGroup != null)
            {
                creditGroup.alpha = 0f;
            }
        }

        if (_endPhotoGroup != null)
        {
            _endPhotoGroup.alpha = 0f;
        }

        if (_skipHintGroup != null)
        {
            _skipHintGroup.alpha = 0f;
        }

        _skipVideoRequested = false;
        _acceptVideoSkip = false;
        _videoLoopReached = false;
    }
    private void SetPhotoFlowAlpha(float alpha)
    {
        if (_whiteGroup != null)
        {
            _whiteGroup.alpha = alpha;
        }

        if (_photoGroup != null)
        {
            _photoGroup.alpha = alpha;
        }

        if (_paragraphsRootGroup != null)
        {
            _paragraphsRootGroup.alpha = alpha;
        }

        if (_introGroup != null)
        {
            _introGroup.alpha = alpha;
        }
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

    private static CanvasGroup CreateFullScreenImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        var image = go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return go.AddComponent<CanvasGroup>();
    }

    private static string FormatMultiline(string raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return raw;
        }

        var formatted = raw.Trim();
        formatted = formatted.Replace("\r\n", "\n").Replace('\r', '\n');
        formatted = formatted.Replace("\\n", "\n");
        return formatted;
    }

    private static IEnumerator FadeCanvasGroup(CanvasGroup group, float from, float to, float duration)
    {
        if (group == null)
        {
            yield break;
        }

        if (duration <= 0f)
        {
            group.alpha = to;
            yield break;
        }

        var elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        group.alpha = to;
    }

    private static IEnumerator WaitUnscaled(float seconds)
    {
        if (seconds <= 0f)
        {
            yield break;
        }

        var elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.unscaledDeltaTime;
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

        if (_videoPlayer != null)
        {
            _videoPlayer.prepareCompleted -= OnVideoPrepareCompleted;
            _videoPlayer.loopPointReached -= OnVideoLoopPointReached;
            _videoPlayer.errorReceived -= OnVideoErrorReceived;
            _videoPlayer.targetTexture = null;
        }

        if (_videoTexture != null)
        {
            _videoTexture.Release();
            Destroy(_videoTexture);
            _videoTexture = null;
        }
    }
}
