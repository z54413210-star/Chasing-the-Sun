using System.Collections;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class PasswordDoorWall : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField] private float inRangeGlowLerpSpeed = 8f;
    [SerializeField] private float errorShakeDuration = 0.22f;
    [SerializeField] private float errorCooldownAfter = 0.35f;

    [Header("Password")]
    [SerializeField] private string correctCode = "1234";
    [SerializeField] private int requiredDigits = 4;

    [Header("World Embedded Visuals")]
    [Tooltip("玩家靠近时的柔和光效（可选），例如门的发光贴图、粒子等。")]
    [SerializeField] private GameObject surfaceGlowRoot;
    [Tooltip("承载 [ _ _ _ _ ] 的世界空间结构（通常挂在一个 World Space Canvas 上）。")]
    [SerializeField] private CanvasGroup inputStructureCanvasGroup;
    [Tooltip("四个数字位对应的文本（TextMeshProUGUI），顺序从左到右。")]
    [SerializeField] private TextMeshProUGUI[] digitSlotTexts;
    [Tooltip("当前可输入位的颜色。")]
    [SerializeField] private Color currentSlotColor = new Color(0.35f, 1f, 0.95f, 1f);
    [Tooltip("未输入位的颜色。")]
    [SerializeField] private Color idleSlotColor = new Color(0.7f, 0.75f, 0.85f, 0.85f);
    [Tooltip("错误反馈颜色。")]
    [SerializeField] private Color errorSlotColor = new Color(1f, 0.25f, 0.25f, 1f);
    [SerializeField] private char emptyChar = '_';
    [SerializeField] private float inRangeAlpha = 0.65f;

    [Header("Door Open Result")]
    [Tooltip("密码正确后，显示的开启门图层/开启效果根节点。")]
    [SerializeField] private GameObject openVisualRoot;
    [Tooltip("解锁前要显示的“关门/上锁”贴图根节点；密码正确后会自动隐藏。若不指定，仅启用 Open Visual，若两张贴图重叠且同 Sorting，关门图可能仍挡在上面。")]
    [SerializeField] private GameObject lockedVisualRoot;
    [Tooltip("门/墙的阻挡碰撞器（可选）。正确后会禁用它，让玩家能通过。")]
    [SerializeField] private Collider2D blockingCollider;

    [Header("Door Motion/Audio (Optional)")]
    [SerializeField] private Animator doorAnimator;
    [SerializeField] private string openAnimatorTrigger = "Open";
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip openClip;
    [SerializeField] private AudioClip errorClip;
    [SerializeField] private bool disableInputAfterSolve = true;

    // Global flag so PresentPastSceneManager can avoid switching worlds while typing.
    public static bool IsAnyDoorInputActive => _activeDoorInputCount > 0;
    private static int _activeDoorInputCount = 0;

    private PlayerController2D _player;
    private bool _isPlayerInRange;
    private bool _isSolved;
    private bool _isInputting;
    private int _cursorIndex;
    private int[] _inputDigits;

    private Rigidbody2D _playerBody;
    private float _savedGravityScale;
    private RigidbodyType2D _savedBodyType;
    private bool _hasSavedRigidState;

    private Vector3 _basePosition;

    private Coroutine _wrongRoutine;

    private void Awake()
    {
        _basePosition = transform.localPosition;

        if (requiredDigits <= 0) requiredDigits = 4;
        if (string.IsNullOrEmpty(correctCode)) correctCode = new string('0', requiredDigits);

        _inputDigits = new int[requiredDigits];
        for (int i = 0; i < _inputDigits.Length; i++) _inputDigits[i] = -1;

        if (digitSlotTexts != null && digitSlotTexts.Length > 0)
        {
            // If provided digits length mismatches, we still allow but highlight/clear by index bounds.
        }

        if (inputStructureCanvasGroup != null)
        {
            inputStructureCanvasGroup.alpha = 0f;
        }

        if (openVisualRoot != null)
        {
            openVisualRoot.SetActive(false);
        }

        if (lockedVisualRoot != null)
        {
            lockedVisualRoot.SetActive(true);
        }

        if (blockingCollider != null)
        {
            // Ensure it starts enabled.
            blockingCollider.enabled = true;
        }

        if (surfaceGlowRoot != null)
        {
            surfaceGlowRoot.SetActive(false);
        }

        ResetSlotsToEmpty();
        SetInputHighlight(-1, forceIdle: true);
    }

    private void Update()
    {
        if (_isSolved) return;

        if (_isPlayerInRange)
        {
            UpdateInRangeVisuals();
        }
        else
        {
            UpdateOutOfRangeVisuals();
        }

        if (!_isInputting)
        {
            if (_isPlayerInRange && Input.GetKeyDown(interactKey))
            {
                TryStartInput();
            }

            return;
        }

        // Input mode: do not allow back-to-back interactions.
        HandleDigitInput();
        HandleBackspaceInput();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        var player = other.GetComponentInParent<PlayerController2D>();
        if (player == null) return;
        if (_isSolved) return;

        _player = player;
        _playerBody = player.GetComponent<Rigidbody2D>();
        _isPlayerInRange = true;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        var player = other.GetComponentInParent<PlayerController2D>();
        if (player == null) return;

        if (_player == player)
        {
            _isPlayerInRange = false;
            _player = null;
            _playerBody = null;
        }
    }

    private void TryStartInput()
    {
        if (_wrongRoutine != null) return;
        if (_isInputting) return;
        if (_player == null) return;

        _isInputting = true;
        _cursorIndex = 0;
        for (int i = 0; i < _inputDigits.Length; i++) _inputDigits[i] = -1;
        ResetSlotsToEmpty();
        SetInputHighlight(0, forceIdle: false);

        EnterTypingFreeze();
        IncrementDoorInputFlag();
    }

    private void HandleDigitInput()
    {
        if (_cursorIndex >= requiredDigits) return;

        int digit = GetPressedDigit();
        if (digit < 0) return;

        _inputDigits[_cursorIndex] = digit;
        SetSlotText(_cursorIndex, digit.ToString());

        _cursorIndex++;
        if (_cursorIndex >= requiredDigits)
        {
            // Completed: validate instantly.
            var input = BuildInputString();
            TrySubmit(input);
            return;
        }

        SetInputHighlight(_cursorIndex, forceIdle: false);
    }

    private void HandleBackspaceInput()
    {
        if (Input.GetKeyDown(KeyCode.Backspace))
        {
            if (_cursorIndex <= 0) return;

            _cursorIndex--;
            _inputDigits[_cursorIndex] = -1;
            SetSlotText(_cursorIndex, emptyChar.ToString());

            SetInputHighlight(_cursorIndex, forceIdle: false);
        }
    }

    private void TrySubmit(string input)
    {
        bool ok = input == correctCode;
        if (ok)
        {
            StartCoroutine(SolveRoutine());
        }
        else
        {
            if (_wrongRoutine != null) StopCoroutine(_wrongRoutine);
            _wrongRoutine = StartCoroutine(WrongRoutine());
        }
    }

    private IEnumerator SolveRoutine()
    {
        _isInputting = false;
        DecrementDoorInputFlag();

        // Open visual / result.
        if (lockedVisualRoot != null)
        {
            lockedVisualRoot.SetActive(false);
        }

        if (openVisualRoot != null)
        {
            openVisualRoot.SetActive(true);
        }

        if (doorAnimator != null && !string.IsNullOrEmpty(openAnimatorTrigger))
        {
            doorAnimator.SetTrigger(openAnimatorTrigger);
        }

        if (blockingCollider != null)
        {
            blockingCollider.enabled = false;
        }

        if (audioSource != null && openClip != null)
        {
            audioSource.PlayOneShot(openClip);
        }

        if (disableInputAfterSolve)
        {
            ExitTypingFreeze();
        }
        else
        {
            // If you want to keep player typing-freeze after solve, set disableInputAfterSolve=false.
            ExitTypingFreeze();
        }

        _isSolved = true;
        if (inputStructureCanvasGroup != null)
        {
            inputStructureCanvasGroup.alpha = 0f;
        }

        if (surfaceGlowRoot != null)
        {
            surfaceGlowRoot.SetActive(false);
        }

        yield return null;
    }

    private IEnumerator WrongRoutine()
    {
        _isInputting = false;
        DecrementDoorInputFlag();

        // Feedback: color + small shake.
        if (audioSource != null && errorClip != null)
        {
            audioSource.PlayOneShot(errorClip);
        }

        var duration = Mathf.Max(0.01f, errorShakeDuration);
        float t = 0f;
        var localPos = transform.localPosition;

        while (t < duration)
        {
            t += Time.deltaTime;
            float k = 1f - (t / duration);
            var shake = Random.insideUnitCircle * (0.03f * k);
            transform.localPosition = _basePosition + new Vector3(shake.x, shake.y, 0f);
            yield return null;
        }

        transform.localPosition = _basePosition;

        ApplyErrorSlotColors();
        yield return new WaitForSeconds(0.10f);

        // Reset back to initial state (clear input).
        ExitTypingFreeze();
        _cursorIndex = 0;
        for (int i = 0; i < _inputDigits.Length; i++) _inputDigits[i] = -1;
        ResetSlotsToEmpty();
        SetInputHighlight(-1, forceIdle: true);

        _wrongRoutine = null;
        yield return new WaitForSeconds(errorCooldownAfter);
    }

    private void EnterTypingFreeze()
    {
        if (_player == null) return;
        if (_playerBody == null) _playerBody = _player.GetComponent<Rigidbody2D>();

        var controller = _player.GetComponent<PlayerController2D>();
        if (controller != null) controller.enabled = false;

        if (_playerBody != null)
        {
            _savedGravityScale = _playerBody.gravityScale;
            _savedBodyType = _playerBody.bodyType;
            _hasSavedRigidState = true;

            _playerBody.velocity = Vector2.zero;
            _playerBody.gravityScale = 0f;
        }
    }

    private void ExitTypingFreeze()
    {
        if (_player == null) return;

        var controller = _player.GetComponent<PlayerController2D>();
        if (controller != null) controller.enabled = true;

        if (_playerBody != null && _hasSavedRigidState)
        {
            _playerBody.gravityScale = _savedGravityScale;
            _hasSavedRigidState = false;
        }
    }

    private void IncrementDoorInputFlag() => _activeDoorInputCount++;
    private void DecrementDoorInputFlag()
    {
        _activeDoorInputCount = Mathf.Max(0, _activeDoorInputCount - 1);
    }

    private void UpdateInRangeVisuals()
    {
        if (surfaceGlowRoot != null)
        {
            if (!surfaceGlowRoot.activeSelf) surfaceGlowRoot.SetActive(true);
        }

        if (inputStructureCanvasGroup != null)
        {
            inputStructureCanvasGroup.alpha =
                Mathf.Lerp(inputStructureCanvasGroup.alpha, inRangeAlpha, Time.deltaTime * inRangeGlowLerpSpeed);
        }
    }

    private void UpdateOutOfRangeVisuals()
    {
        if (surfaceGlowRoot != null)
        {
            if (surfaceGlowRoot.activeSelf) surfaceGlowRoot.SetActive(false);
        }

        if (inputStructureCanvasGroup != null)
        {
            inputStructureCanvasGroup.alpha =
                Mathf.Lerp(inputStructureCanvasGroup.alpha, 0f, Time.deltaTime * inRangeGlowLerpSpeed);
        }
    }

    private void ResetSlotsToEmpty()
    {
        if (digitSlotTexts == null) return;

        for (int i = 0; i < digitSlotTexts.Length; i++)
        {
            if (digitSlotTexts[i] == null) continue;
            // Only touch indices that are in the required digit range.
            if (i >= requiredDigits) continue;

            digitSlotTexts[i].text = emptyChar.ToString();
            digitSlotTexts[i].color = idleSlotColor;
        }
    }

    private void ApplyErrorSlotColors()
    {
        if (digitSlotTexts == null) return;
        for (int i = 0; i < digitSlotTexts.Length; i++)
        {
            if (digitSlotTexts[i] == null) continue;
            if (i >= requiredDigits) continue;
            digitSlotTexts[i].color = errorSlotColor;
        }
    }

    private void SetInputHighlight(int index, bool forceIdle)
    {
        if (digitSlotTexts == null) return;

        for (int i = 0; i < digitSlotTexts.Length; i++)
        {
            if (digitSlotTexts[i] == null) continue;
            if (i >= requiredDigits) continue;

            if (forceIdle)
            {
                digitSlotTexts[i].color = idleSlotColor;
                continue;
            }

            if (i == index)
            {
                digitSlotTexts[i].color = currentSlotColor;
            }
            else
            {
                // Already filled slots keep a slightly dimmer tone so player can read typed digits.
                digitSlotTexts[i].color = idleSlotColor;
            }
        }
    }

    private void SetSlotText(int index, string text)
    {
        if (digitSlotTexts == null) return;
        if (index < 0 || index >= digitSlotTexts.Length) return;
        digitSlotTexts[index].text = text;
    }

    private string BuildInputString()
    {
        // Always returns exactly requiredDigits length.
        var chars = new char[requiredDigits];
        for (int i = 0; i < requiredDigits; i++)
        {
            int d = _inputDigits[i];
            chars[i] = d >= 0 ? (char)('0' + d) : emptyChar;
        }

        return new string(chars);
    }

    private int GetPressedDigit()
    {
        // Works for both main keyboard digits and numeric keypad.
        if (Input.GetKeyDown(KeyCode.Alpha0) || Input.GetKeyDown(KeyCode.Keypad0)) return 0;
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) return 1;
        if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) return 2;
        if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) return 3;
        if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4)) return 4;
        if (Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5)) return 5;
        if (Input.GetKeyDown(KeyCode.Alpha6) || Input.GetKeyDown(KeyCode.Keypad6)) return 6;
        if (Input.GetKeyDown(KeyCode.Alpha7) || Input.GetKeyDown(KeyCode.Keypad7)) return 7;
        if (Input.GetKeyDown(KeyCode.Alpha8) || Input.GetKeyDown(KeyCode.Keypad8)) return 8;
        if (Input.GetKeyDown(KeyCode.Alpha9) || Input.GetKeyDown(KeyCode.Keypad9)) return 9;
        return -1;
    }
    
    private void OnDrawGizmosSelected()
    {
        // Helps you see where the script is attached (door root).
        Gizmos.color = new Color(1f, 1f, 0.3f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, 0.25f);
    }
}

