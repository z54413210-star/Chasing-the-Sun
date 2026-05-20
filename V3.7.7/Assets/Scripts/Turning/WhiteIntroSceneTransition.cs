using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 轻量转场：渐亮至全白 → 居中显示一段引导文案 → 进入指定场景。
/// 由 <see cref="WhiteIntroSceneTransitionTrigger2D"/> 或脚本调用 <see cref="Play"/> 触发。
/// </summary>
[DisallowMultipleComponent]
public class WhiteIntroSceneTransition : MonoBehaviour
{
    private const string DefaultMessage =
        "Hello! I know it's your first time to come here, a totally different and magical world. Don't be afraid. I will accompany you";

    [Header("Content")]
    [TextArea(4, 10)]
    [SerializeField] private string message = DefaultMessage;
    [Tooltip("必须在 Build Settings 中已加入该场景。")]
    [SerializeField] private string nextSceneName;
    [SerializeField] private TMP_FontAsset fontOverride;

    [Header("Timing (unscaled)")]
    [SerializeField] private float brightenDuration = 1.25f;
    [SerializeField] private float textFadeInDuration = 0.9f;
    [SerializeField] private float textHoldDuration = 2.8f;

    [Header("Typography")]
    [SerializeField] private float fontSize = 30f;
    [SerializeField] private Color textColor = new Color(0.14f, 0.14f, 0.16f, 1f);

    [Header("Layout")]
    [SerializeField] private int canvasSortOrder = 590;

    [Header("Player / time")]
    [SerializeField] private bool freezeTimeDuringTransition = true;
    [SerializeField] private bool disablePlayerWhilePlaying = true;

    [Header("End")]
    [SerializeField] private UnityEvent onBeforeLoadScene;

    private CanvasGroup _whiteGroup;
    private CanvasGroup _textGroup;
    private GameObject _canvasRoot;
    private Coroutine _routine;
    private bool _isPlaying;
    private float _savedTimeScale = 1f;
    private PlayerController2D _cachedPlayer;
    private bool _hadPlayerEnabled;

    public bool IsPlaying => _isPlaying;

    private void Awake()
    {
        BuildUi();
        if (_canvasRoot != null)
        {
            _canvasRoot.SetActive(false);
        }
    }

    public void Play()
    {
        if (_isPlaying)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(nextSceneName))
        {
            Debug.LogWarning($"{nameof(WhiteIntroSceneTransition)}: Next Scene Name is empty.", this);
            return;
        }

        if (_canvasRoot == null)
        {
            BuildUi();
        }

        _routine = StartCoroutine(PlayRoutine());
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

        ResetVisuals();
        _canvasRoot.SetActive(true);

        yield return FadeCanvasGroup(_whiteGroup, 0f, 1f, brightenDuration);
        yield return FadeCanvasGroup(_textGroup, 0f, 1f, textFadeInDuration);
        yield return WaitUnscaled(textHoldDuration);

        if (disablePlayerWhilePlaying && _cachedPlayer != null)
        {
            _cachedPlayer.enabled = _hadPlayerEnabled;
            _cachedPlayer = null;
        }

        if (freezeTimeDuringTransition)
        {
            Time.timeScale = _savedTimeScale;
        }

        _isPlaying = false;
        onBeforeLoadScene?.Invoke();
        SceneManager.LoadScene(nextSceneName);
        _routine = null;
    }

    private void ResetVisuals()
    {
        if (_whiteGroup != null)
        {
            _whiteGroup.alpha = 0f;
        }

        if (_textGroup != null)
        {
            _textGroup.alpha = 0f;
        }
    }

    private void BuildUi()
    {
        if (_canvasRoot != null)
        {
            return;
        }

        _canvasRoot = new GameObject("WhiteIntroSceneTransitionCanvas");
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

        var whiteGo = new GameObject("White");
        whiteGo.transform.SetParent(_canvasRoot.transform, false);
        var whiteRt = whiteGo.AddComponent<RectTransform>();
        whiteRt.anchorMin = Vector2.zero;
        whiteRt.anchorMax = Vector2.one;
        whiteRt.offsetMin = Vector2.zero;
        whiteRt.offsetMax = Vector2.zero;
        var whiteImg = whiteGo.AddComponent<Image>();
        whiteImg.color = Color.white;
        whiteImg.raycastTarget = false;
        _whiteGroup = whiteGo.AddComponent<CanvasGroup>();
        _whiteGroup.alpha = 0f;
        _whiteGroup.blocksRaycasts = false;

        var textRoot = new GameObject("Message");
        textRoot.transform.SetParent(_canvasRoot.transform, false);
        var textRt = textRoot.AddComponent<RectTransform>();
        textRt.anchorMin = new Vector2(0.08f, 0.28f);
        textRt.anchorMax = new Vector2(0.92f, 0.72f);
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;
        _textGroup = textRoot.AddComponent<CanvasGroup>();
        _textGroup.alpha = 0f;
        _textGroup.blocksRaycasts = false;

        var tmpGo = new GameObject("TMP");
        tmpGo.transform.SetParent(textRoot.transform, false);
        var tmpRt = tmpGo.AddComponent<RectTransform>();
        tmpRt.anchorMin = Vector2.zero;
        tmpRt.anchorMax = Vector2.one;
        tmpRt.offsetMin = Vector2.zero;
        tmpRt.offsetMax = Vector2.zero;

        var tmp = tmpGo.AddComponent<TextMeshProUGUI>();
        tmp.text = FormatMultilineForTmp(string.IsNullOrWhiteSpace(message) ? DefaultMessage : message);
        tmp.color = textColor;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = true;
        tmp.overflowMode = TextOverflowModes.Overflow;
        ApplyFont(tmp);
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
