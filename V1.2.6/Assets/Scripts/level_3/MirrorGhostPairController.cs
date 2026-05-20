using UnityEngine;

[DisallowMultipleComponent]
public class MirrorGhostPairController : MonoBehaviour
{
    [Header("Mirror")]
    [SerializeField] private float mirrorCenterX;

    [Header("Ghosts")]
    [SerializeField] private Transform leftGhost;
    [SerializeField] private Transform rightGhost;
    [SerializeField] private BoxCollider2D leftGhostCollider;
    [SerializeField] private BoxCollider2D rightGhostCollider;

    [Header("Sensors")]
    [SerializeField] private GhostPlatformSensor leftSensor;
    [SerializeField] private GhostPlatformSensor rightSensor;
    [SerializeField] private bool useSensorBoundsForMovement = true;

    [Header("Bounds")]
    [SerializeField] private float leftMinX = -4f;
    [SerializeField] private float leftMaxX = -1f;
    [SerializeField] private float rightMinX = 1f;
    [SerializeField] private float rightMaxX = 4f;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private bool useBlockingDetection = false;

    private Level3Side _lastLeadingSide = Level3Side.Left;

    private void Awake()
    {
        CacheReferences();
    }

    private void OnValidate()
    {
        CacheReferences();
        RefreshMovementBoundsFromSensors();
    }

    private void Update()
    {
        CacheReferences();
        if (leftGhost == null || rightGhost == null || leftSensor == null || rightSensor == null)
        {
            return;
        }

        RefreshMovementBoundsFromSensors();

        var leftTarget = leftSensor.GetClosestAvatar(leftGhost.position);
        var rightTarget = rightSensor.GetClosestAvatar(rightGhost.position);
        if (leftTarget == null && rightTarget == null)
        {
            CheckGhostContact();
            return;
        }

        var leadingSide = ResolveLeadingSide(leftTarget, rightTarget);
        float nextLeftX;
        float nextRightX;

        if (leadingSide == Level3Side.Left)
        {
            nextLeftX = Mathf.MoveTowards(leftGhost.position.x, leftTarget.FootPosition.x, moveSpeed * Time.deltaTime);
            nextLeftX = Mathf.Clamp(nextLeftX, leftMinX, leftMaxX);
            nextRightX = Mathf.Clamp(MirrorX(nextLeftX), rightMinX, rightMaxX);
            nextLeftX = MirrorX(nextRightX);
        }
        else
        {
            nextRightX = Mathf.MoveTowards(rightGhost.position.x, rightTarget.FootPosition.x, moveSpeed * Time.deltaTime);
            nextRightX = Mathf.Clamp(nextRightX, rightMinX, rightMaxX);
            nextLeftX = Mathf.Clamp(MirrorX(nextRightX), leftMinX, leftMaxX);
            nextRightX = MirrorX(nextLeftX);
        }

        var leftTargetPosition = new Vector2(nextLeftX, leftGhost.position.y);
        var rightTargetPosition = new Vector2(nextRightX, rightGhost.position.y);
        if ((!useBlockingDetection || !IsBlocked(leftGhostCollider, leftTargetPosition, rightGhostCollider)) &&
            (!useBlockingDetection || !IsBlocked(rightGhostCollider, rightTargetPosition, leftGhostCollider)))
        {
            leftGhost.position = new Vector3(leftTargetPosition.x, leftGhost.position.y, leftGhost.position.z);
            rightGhost.position = new Vector3(rightTargetPosition.x, rightGhost.position.y, rightGhost.position.z);
        }

        _lastLeadingSide = leadingSide;
        CheckGhostContact();
    }

    private Level3Side ResolveLeadingSide(Level3PlayerAvatar leftTarget, Level3PlayerAvatar rightTarget)
    {
        if (leftTarget != null && rightTarget == null)
        {
            return Level3Side.Left;
        }

        if (leftTarget == null && rightTarget != null)
        {
            return Level3Side.Right;
        }

        var leftDistance = Mathf.Abs(leftGhost.position.x - leftTarget.FootPosition.x);
        var rightDistance = Mathf.Abs(rightGhost.position.x - rightTarget.FootPosition.x);
        if (!Mathf.Approximately(leftDistance, rightDistance))
        {
            return leftDistance < rightDistance ? Level3Side.Left : Level3Side.Right;
        }

        return _lastLeadingSide;
    }

    private bool IsBlocked(BoxCollider2D ghostCollider, Vector2 targetPosition, BoxCollider2D otherGhostCollider)
    {
        if (ghostCollider == null)
        {
            return false;
        }

        var delta = targetPosition - (Vector2)ghostCollider.transform.position;
        var center = (Vector2)ghostCollider.bounds.center + delta;
        var size = ghostCollider.bounds.size * 0.95f;
        var hits = Physics2D.OverlapBoxAll(center, size, 0f);
        for (var i = 0; i < hits.Length; i++)
        {
            var hit = hits[i];
            if (hit == null || hit == ghostCollider || hit == otherGhostCollider || hit.isTrigger)
            {
                continue;
            }

            if (hit.GetComponentInParent<Level3PlayerAvatar>() != null)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private void CheckGhostContact()
    {
        TryKillContacts(leftGhostCollider, leftSensor);
        TryKillContacts(rightGhostCollider, rightSensor);
    }

    private static void TryKillContacts(BoxCollider2D ghostCollider, GhostPlatformSensor sensor)
    {
        if (ghostCollider == null || sensor == null)
        {
            return;
        }

        foreach (var occupant in sensor.GetOccupants())
        {
            if (occupant == null || occupant.BodyCollider == null)
            {
                continue;
            }

            if (ghostCollider.bounds.Intersects(occupant.BodyCollider.bounds))
            {
                occupant.Kill();
            }
        }
    }

    private void CacheReferences()
    {
        if (leftGhostCollider == null && leftGhost != null)
        {
            leftGhostCollider = leftGhost.GetComponent<BoxCollider2D>();
        }

        if (rightGhostCollider == null && rightGhost != null)
        {
            rightGhostCollider = rightGhost.GetComponent<BoxCollider2D>();
        }
    }

    private void RefreshMovementBoundsFromSensors()
    {
        if (!useSensorBoundsForMovement)
        {
            return;
        }

        var leftSensorCollider = leftSensor != null ? leftSensor.GetComponent<Collider2D>() : null;
        if (leftSensorCollider != null)
        {
            leftMinX = leftSensorCollider.bounds.min.x;
            leftMaxX = leftSensorCollider.bounds.max.x;
        }

        var rightSensorCollider = rightSensor != null ? rightSensor.GetComponent<Collider2D>() : null;
        if (rightSensorCollider != null)
        {
            rightMinX = rightSensorCollider.bounds.min.x;
            rightMaxX = rightSensorCollider.bounds.max.x;
        }
    }

    private float MirrorX(float xPosition)
    {
        return (mirrorCenterX * 2f) - xPosition;
    }

    private void OnDrawGizmosSelected()
    {
        RefreshMovementBoundsFromSensors();
        Gizmos.color = new Color(0.8f, 0.2f, 1f, 0.8f);
        Gizmos.DrawLine(new Vector3(mirrorCenterX, -20f, 0f), new Vector3(mirrorCenterX, 20f, 0f));
        Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.45f);
        Gizmos.DrawLine(new Vector3(leftMinX, transform.position.y, 0f), new Vector3(leftMaxX, transform.position.y, 0f));
        Gizmos.DrawLine(new Vector3(rightMinX, transform.position.y, 0f), new Vector3(rightMaxX, transform.position.y, 0f));
    }
}



