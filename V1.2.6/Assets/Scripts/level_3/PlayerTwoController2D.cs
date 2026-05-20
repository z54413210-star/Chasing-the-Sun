using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CapsuleCollider2D))]
[DisallowMultipleComponent]
public class PlayerTwoController2D : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 4.5f;
    [SerializeField] private float pushSpeed = 2.5f;
    [SerializeField] private float climbSpeed = 2.5f;
    [SerializeField] private float jumpHeight = 2.6f;
    [SerializeField] private float baseGravityScale = 4f;
    [SerializeField] private float fallGravityMultiplier = 2.3f;
    [SerializeField] private float lowJumpGravityMultiplier = 1.8f;
    [SerializeField] private float maxFallSpeed = 14f;
    [SerializeField] private float ladderDetachHorizontalSpeed = 4f;

    [Header("Detection")]
    [SerializeField] private Vector2 groundCheckSize = new Vector2(0.68f, 0.10f);
    [SerializeField] private float groundCheckDistance = 0.08f;
    [SerializeField] private Vector2 pushCheckSize = new Vector2(0.30f, 1.00f);
    [SerializeField] private Vector2 pushCheckOffset = new Vector2(0.55f, 0.85f);
    [SerializeField] private float ladderAttachThreshold = 0.35f;
    [SerializeField] private float ladderTopExitThreshold = 0.1f;
    [SerializeField] private float ladderTopRaycastHeight = 1.0f;
    [SerializeField] private float ladderTopSnapOffset = 0.01f;
    [SerializeField] private float ladderTopReattachHorizontalTolerance = 0.45f;
    [SerializeField] private float ladderTopReattachVerticalTolerance = 1.2f;
    [SerializeField] private float ladderTopReattachSearchDistance = 1.8f;
    [SerializeField] private float apexVelocityThreshold = 0.25f;

    [Header("State Timing")]
    [SerializeField] private float jumpStartDuration = 0.14f;
    [SerializeField] private float landDuration = 0.08f;

    [Header("References")]
    [SerializeField] private Rigidbody2D body;
    [SerializeField] private CapsuleCollider2D bodyCollider;
    [SerializeField] private PlayerAnimationDriver animationDriver;

    [Header("Layer Masks")]
    [SerializeField] private LayerMask groundLayers;
    [SerializeField] private LayerMask pushableLayers;
    [SerializeField] private LayerMask climbableLayers;

    private readonly List<PlayerTwoLadderZone> _ladderZones = new List<PlayerTwoLadderZone>();
    private readonly Collider2D[] _topReattachSearchResults = new Collider2D[16];

    private Vector3 _initialSpawnPosition;
    private Quaternion _initialSpawnRotation;
    private float _horizontalInput;
    private float _verticalInput;
    private bool _jumpPressed;
    private bool _jumpHeld;
    private bool _wasGrounded;
    private bool _isGrounded;
    private bool _isClimbing;
    private bool _isDead;
    private int _facingDirection = 1;
    private float _jumpStartTimer;
    private float _landTimer;
    private PlayerTwoLadderZone _activeLadder;
    private PlayerTwoLadderZone _lastTopExitedLadder;
    private PushableBox _currentPushTarget;

    public Vector3 InitialSpawnPosition => _initialSpawnPosition;
    public bool IsClimbing => _isClimbing;

    private void Awake()
    {
        CacheReferences();
        ConfigureDefaults();

        if (GetComponent<PlayerTwoLadderTraversalAssist>() == null)
        {
            gameObject.AddComponent<PlayerTwoLadderTraversalAssist>();
        }

        _initialSpawnPosition = transform.position;
        _initialSpawnRotation = transform.rotation;

    }

    private void Start()
    {

    }

    private void OnValidate()
    {
        CacheReferences();
        ConfigureDefaults();
    }

    private void Update()
    {
        if (_isDead)
        {
            _horizontalInput = 0f;
            _verticalInput = 0f;
            _jumpPressed = false;
            _jumpHeld = false;
            return;
        }

        _horizontalInput = 0f;
        if (Input.GetKey(KeyCode.LeftArrow))
        {
            _horizontalInput -= 1f;
        }

        if (Input.GetKey(KeyCode.RightArrow))
        {
            _horizontalInput += 1f;
        }

        _verticalInput = 0f;
        if (Input.GetKey(KeyCode.UpArrow))
        {
            _verticalInput += 1f;
        }

        if (Input.GetKey(KeyCode.DownArrow))
        {
            _verticalInput -= 1f;
        }

        _jumpPressed |= Input.GetKeyDown(KeyCode.M);
        _jumpHeld = Input.GetKey(KeyCode.M);

        if (Mathf.Abs(_horizontalInput) > 0.01f && !_isClimbing)
        {
            _facingDirection = _horizontalInput > 0f ? 1 : -1;
        }

        if (_jumpStartTimer > 0f)
        {
            _jumpStartTimer -= Time.deltaTime;
        }

        if (_landTimer > 0f)
        {
            _landTimer -= Time.deltaTime;
        }
    }

    private void FixedUpdate()
    {
        UpdateGroundedState();

        if (_isDead)
        {
            body.velocity = Vector2.zero;
            ApplyAnimation(PlayerState.Dead);
            _wasGrounded = _isGrounded;
            return;
        }

        if (_isClimbing)
        {
            if (Mathf.Abs(_horizontalInput) > 0.01f)
            {
                DetachFromLadder(_horizontalInput * ladderDetachHorizontalSpeed);
            }
            else
            {
                HandleClimbing();
                ApplyAnimation(PlayerState.Climb);
                _wasGrounded = _isGrounded;
                _jumpPressed = false;
                return;
            }
        }

        TryAttachToLadder();
        if (_isClimbing)
        {
            HandleClimbing();
            ApplyAnimation(PlayerState.Climb);
            _wasGrounded = _isGrounded;
            _jumpPressed = false;
            return;
        }

        HandleMovement();
        HandleJump();
        ApplyGravityModifiers();
        ApplyAnimation(ResolveCurrentState());

        _wasGrounded = _isGrounded;
        _jumpPressed = false;
    }

    public void NotifyEnterLadder(PlayerTwoLadderZone ladderZone)
    {
        if (ladderZone != null && !_ladderZones.Contains(ladderZone))
        {
            _ladderZones.Add(ladderZone);
        }
    }

    public void NotifyExitLadder(PlayerTwoLadderZone ladderZone)
    {
        if (ladderZone == null)
        {
            return;
        }

        _ladderZones.Remove(ladderZone);
    }

    public void HandleDeathStarted()
    {
        var ladderAssist = GetComponent<PlayerTwoLadderTraversalAssist>();
        if (ladderAssist != null)
        {
            ladderAssist.RestoreImmediately();
        }

        _isDead = true;
        _isClimbing = false;
        _activeLadder = null;
        _lastTopExitedLadder = null;
        _currentPushTarget = null;
        body.velocity = Vector2.zero;
        body.gravityScale = baseGravityScale;
        body.angularVelocity = 0f;

        if (animationDriver != null)
        {
            animationDriver.ForceState(PlayerState.Dead, _facingDirection);
        }
    }

    public void HandleDeathFinished()
    {
        _isDead = false;
    }

    public void RespawnAt(Vector3 worldPosition)
    {
        var ladderAssist = GetComponent<PlayerTwoLadderTraversalAssist>();
        if (ladderAssist != null)
        {
            ladderAssist.RestoreImmediately();
        }

        transform.position = worldPosition;
        transform.rotation = _initialSpawnRotation;

        body.position = worldPosition;
        body.rotation = _initialSpawnRotation.eulerAngles.z;
        body.velocity = Vector2.zero;
        body.angularVelocity = 0f;
        body.gravityScale = baseGravityScale;

        _isDead = false;
        _isClimbing = false;
        _activeLadder = null;
        _lastTopExitedLadder = null;
        _currentPushTarget = null;
        _jumpPressed = false;
        _jumpHeld = false;
        _jumpStartTimer = 0f;
        _landTimer = 0f;
        _wasGrounded = false;
        _isGrounded = false;

        if (animationDriver != null)
        {
            animationDriver.ForceState(PlayerState.Idle, _facingDirection);
        }
    }

    private void HandleMovement()
    {
        _currentPushTarget = FindPushTarget(_horizontalInput);
        var isPushing = Mathf.Abs(_horizontalInput) > 0.01f && _isGrounded && _currentPushTarget != null;
        var speed = isPushing ? pushSpeed : moveSpeed;
        var velocity = body.velocity;
        velocity.x = _horizontalInput * speed;
        body.velocity = velocity;
    }

    private void HandleJump()
    {
        if (!_jumpPressed || !_isGrounded)
        {
            return;
        }

        var jumpVelocity = Mathf.Sqrt(2f * Physics2D.gravity.magnitude * baseGravityScale * jumpHeight);
        var velocity = body.velocity;
        velocity.y = jumpVelocity;
        body.velocity = velocity;

        _isGrounded = false;
        _jumpStartTimer = jumpStartDuration;
        _landTimer = 0f;
    }

    private void HandleClimbing()
    {
        if (_activeLadder == null)
        {
            DetachFromLadder(0f);
            return;
        }

        var position = transform.position;
        position.x = _activeLadder.AttachX;
        transform.position = position;
        body.position = position;

        body.velocity = new Vector2(0f, _verticalInput * climbSpeed);
        body.gravityScale = 0f;

        if (_verticalInput > 0f && transform.position.y >= _activeLadder.TopY - ladderTopExitThreshold)
        {
            ExitFromLadderTop();
            return;
        }

        if (_verticalInput < 0f && transform.position.y <= _activeLadder.BottomY + 0.02f)
        {
            var bottomExit = transform.position;
            bottomExit.y = _activeLadder.BottomY;

            transform.position = bottomExit;
            body.position = bottomExit;

            DetachFromLadder(0f);
            body.velocity = Vector2.zero;
            UpdateGroundedState();
            _wasGrounded = _isGrounded;
            return;
        }
    }

    private void ExitFromLadderTop()
    {
        var topExit = transform.position;
        topExit.y = ResolveTopExitFeetY();
        transform.position = topExit;
        body.position = topExit;

        DetachFromLadder(0f, true);
        body.velocity = Vector2.zero;

        Physics2D.SyncTransforms();
        UpdateGroundedState();
        _wasGrounded = _isGrounded;

        if (_isGrounded)
        {
            _landTimer = landDuration;
        }
    }

    private float ResolveTopExitFeetY()
    {
        if (_activeLadder == null)
        {
            return transform.position.y;
        }

        var rayOrigin = new Vector2(_activeLadder.AttachX, _activeLadder.TopY + ladderTopRaycastHeight);
        var rayDistance = ladderTopRaycastHeight * 2f;
        var hit = Physics2D.Raycast(rayOrigin, Vector2.down, rayDistance, groundLayers);

        if (hit.collider != null && !hit.collider.isTrigger)
        {
            return hit.point.y + ladderTopSnapOffset;
        }

        return _activeLadder.TopY + ladderTopSnapOffset;
    }

    private void ApplyGravityModifiers()
    {
        if (_isClimbing)
        {
            body.gravityScale = 0f;
            return;
        }

        if (body.velocity.y < -0.01f)
        {
            body.gravityScale = baseGravityScale * fallGravityMultiplier;
        }
        else if (body.velocity.y > 0.01f && !_jumpHeld)
        {
            body.gravityScale = baseGravityScale * lowJumpGravityMultiplier;
        }
        else
        {
            body.gravityScale = baseGravityScale;
        }

        if (body.velocity.y < -maxFallSpeed)
        {
            body.velocity = new Vector2(body.velocity.x, -maxFallSpeed);
        }
    }

    private void TryAttachToLadder()
    {
        if (Mathf.Abs(_verticalInput) < 0.01f)
        {
            return;
        }

        var isPressingUp = _verticalInput > 0f;
        var isPressingDown = _verticalInput < 0f;

        if (_ladderZones.Count == 0 && !isPressingDown)
        {
            return;
        }

        PlayerTwoLadderZone bestLadder = null;
        var bestScore = 0f;
        var playerBounds = bodyCollider.bounds;

        for (var i = _ladderZones.Count - 1; i >= 0; i--)
        {
            if (_ladderZones[i] == null)
            {
                _ladderZones.RemoveAt(i);
                continue;
            }

            var overlapScore = _ladderZones[i].GetOverlapScore(playerBounds);
            if (overlapScore > bestScore)
            {
                bestScore = overlapScore;
                bestLadder = _ladderZones[i];
            }
        }

        var isTopReattach = false;
        if (bestLadder == null || bestScore < ladderAttachThreshold)
        {
            if (isPressingDown && TryGetTopReattachLadder(out var topReattachLadder))
            {
                bestLadder = topReattachLadder;
                isTopReattach = true;
            }
            else
            {
                return;
            }
        }

        if (isPressingUp && transform.position.y >= bestLadder.TopY - 0.1f)
        {
            return;
        }

        if (!isTopReattach && isPressingDown && transform.position.y <= bestLadder.BottomY + 0.1f)
        {
            return;
        }

        _activeLadder = bestLadder;
        _lastTopExitedLadder = null;
        _isClimbing = true;
        _currentPushTarget = null;
        body.velocity = Vector2.zero;
        body.gravityScale = 0f;

        var snappedPosition = transform.position;
        snappedPosition.x = bestLadder.AttachX;
        transform.position = snappedPosition;
        body.position = snappedPosition;
    }

    private bool TryGetTopReattachLadder(out PlayerTwoLadderZone ladder)
    {
        ladder = null;
        if (!IsNearTopReattachWindow())
        {
            return false;
        }

        if (IsTopReattachCandidate(_lastTopExitedLadder))
        {
            ladder = _lastTopExitedLadder;
            return true;
        }

        var searchCenter = new Vector2(transform.position.x, transform.position.y - (ladderTopReattachSearchDistance * 0.5f));
        var searchSize = new Vector2(ladderTopReattachHorizontalTolerance * 2f, ladderTopReattachSearchDistance);
        var hitCount = Physics2D.OverlapBoxNonAlloc(searchCenter, searchSize, 0f, _topReattachSearchResults, climbableLayers);
        var bestXDistance = float.MaxValue;

        for (var i = 0; i < hitCount; i++)
        {
            var hit = _topReattachSearchResults[i];
            if (hit == null)
            {
                continue;
            }

            var candidate = hit.GetComponent<PlayerTwoLadderZone>();
            if (candidate == null)
            {
                candidate = hit.GetComponentInParent<PlayerTwoLadderZone>();
            }

            if (!IsTopReattachCandidate(candidate))
            {
                continue;
            }

            var xDistance = Mathf.Abs(transform.position.x - candidate.AttachX);
            if (xDistance < bestXDistance)
            {
                bestXDistance = xDistance;
                ladder = candidate;
            }
        }

        if (ladder != null)
        {
            _lastTopExitedLadder = ladder;
            return true;
        }

        return false;
    }

    private bool IsNearTopReattachWindow()
    {
        return _isGrounded && _verticalInput < -0.01f;
    }

    private bool IsTopReattachCandidate(PlayerTwoLadderZone ladder)
    {
        if (ladder == null)
        {
            return false;
        }

        var xDistance = Mathf.Abs(transform.position.x - ladder.AttachX);
        if (xDistance > ladderTopReattachHorizontalTolerance)
        {
            return false;
        }

        var topY = ladder.TopY;
        var feetY = transform.position.y;
        return feetY >= topY - ladderTopExitThreshold && feetY <= topY + ladderTopReattachVerticalTolerance;
    }

    private void DetachFromLadder(float horizontalVelocity, bool rememberTopExit = false)
    {
        if (rememberTopExit && _activeLadder != null)
        {
            _lastTopExitedLadder = _activeLadder;
        }
        else if (!rememberTopExit)
        {
            _lastTopExitedLadder = null;
        }

        _isClimbing = false;
        _activeLadder = null;
        body.gravityScale = baseGravityScale;
        body.velocity = new Vector2(horizontalVelocity, body.velocity.y);

        var ladderAssist = GetComponent<PlayerTwoLadderTraversalAssist>();
        if (ladderAssist != null)
        {
            ladderAssist.RestoreImmediately();
        }
    }

    private PushableBox FindPushTarget(float horizontalAxis)
    {
        if (Mathf.Abs(horizontalAxis) < 0.01f)
        {
            return null;
        }

        var direction = horizontalAxis > 0f ? 1f : -1f;
        var origin = (Vector2)transform.position + new Vector2(pushCheckOffset.x * direction, pushCheckOffset.y);
        var hit = Physics2D.OverlapBox(origin, pushCheckSize, 0f, pushableLayers);
        return hit != null ? hit.GetComponent<PushableBox>() : null;
    }

    private void UpdateGroundedState()
    {
        var center = (Vector2)transform.position + new Vector2(0f, (groundCheckSize.y * 0.5f) - groundCheckDistance);
        _isGrounded = Physics2D.OverlapBox(center, groundCheckSize, 0f, groundLayers);

        if (!_wasGrounded && _isGrounded)
        {
            _landTimer = landDuration;
        }
    }

    private PlayerState ResolveCurrentState()
    {
        if (_isDead)
        {
            return PlayerState.Dead;
        }

        if (_isClimbing)
        {
            return PlayerState.Climb;
        }

        if (_landTimer > 0f && _isGrounded)
        {
            return PlayerState.Land;
        }

        if (!_isGrounded)
        {
            if (_jumpStartTimer > 0f)
            {
                return PlayerState.JumpStart;
            }

            if (body.velocity.y > apexVelocityThreshold)
            {
                return PlayerState.JumpRise;
            }

            if (Mathf.Abs(body.velocity.y) <= apexVelocityThreshold)
            {
                return PlayerState.JumpApex;
            }

            return PlayerState.JumpFall;
        }

        if (_currentPushTarget != null && Mathf.Abs(_horizontalInput) > 0.01f)
        {
            return _horizontalInput > 0f ? PlayerState.PushRight : PlayerState.PushLeft;
        }

        if (Mathf.Abs(body.velocity.x) > 0.05f)
        {
            return PlayerState.Run;
        }

        return PlayerState.Idle;
    }

    private void ApplyAnimation(PlayerState state)
    {
        if (animationDriver != null)
        {
            animationDriver.Apply(state, _facingDirection);
        }
    }

    private void ConfigureDefaults()
    {
        if (groundLayers == 0)
        {
            groundLayers = ChaseTheSunProjectSettings.MaskFromNames(
                ChaseTheSunProjectSettings.GroundLayer,
                ChaseTheSunProjectSettings.PushableLayer);
        }

        if (pushableLayers == 0)
        {
            pushableLayers = ChaseTheSunProjectSettings.MaskFromNames(ChaseTheSunProjectSettings.PushableLayer);
        }

        if (climbableLayers == 0)
        {
            climbableLayers = ChaseTheSunProjectSettings.MaskFromNames(ChaseTheSunProjectSettings.ClimbableLayer);
        }
    }

    private void CacheReferences()
    {
        if (body == null)
        {
            body = GetComponent<Rigidbody2D>();
        }

        if (bodyCollider == null)
        {
            bodyCollider = GetComponent<CapsuleCollider2D>();
        }

        if (animationDriver == null)
        {
            animationDriver = GetComponent<PlayerAnimationDriver>();
        }

    }

    private void Reset()
    {
        CacheReferences();
        ConfigureDefaults();

        gameObject.layer = LayerMask.NameToLayer(ChaseTheSunProjectSettings.PlayerLayer);

        if (body != null)
        {
            body.gravityScale = baseGravityScale;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        if (bodyCollider != null)
        {
            bodyCollider.direction = CapsuleDirection2D.Vertical;
            bodyCollider.offset = new Vector2(0f, 0.875f);
            bodyCollider.size = new Vector2(0.75f, 1.75f);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.1f, 1f, 0.1f, 0.35f);
        var groundCenter = transform.position + new Vector3(0f, (groundCheckSize.y * 0.5f) - groundCheckDistance, 0f);
        Gizmos.DrawCube(groundCenter, groundCheckSize);

        Gizmos.color = new Color(1f, 0.8f, 0.1f, 0.35f);
        var direction = _facingDirection >= 0 ? 1f : -1f;
        var pushCenter = transform.position + new Vector3(pushCheckOffset.x * direction, pushCheckOffset.y, 0f);
        Gizmos.DrawCube(pushCenter, pushCheckSize);
    }
}


