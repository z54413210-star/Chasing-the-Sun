using UnityEngine;

[DisallowMultipleComponent]
public class MirrorBoxPairController : MonoBehaviour
{
    [SerializeField] private float mirrorCenterX;
    [SerializeField] private MirrorBoxUnit leftBox;
    [SerializeField] private MirrorBoxUnit rightBox;
    [SerializeField] private float inputDeadZone = 0.0001f;

    private float _resolvedLeftX;
    private float _resolvedRightX;

    private void Awake()
    {
        CacheReferences();
        CanonicalizePair(true);
    }

    private void OnValidate()
    {
        CacheReferences();
    }

    private void FixedUpdate()
    {
        CacheReferences();
        if (leftBox == null || rightBox == null || leftBox.Body == null || rightBox.Body == null)
        {
            return;
        }

        var leftInputDelta = leftBox.Body.position.x - _resolvedLeftX;
        var rightInputDelta = rightBox.Body.position.x - _resolvedRightX;
        var commandDelta = (leftInputDelta - rightInputDelta) * 0.5f;
        if (Mathf.Abs(commandDelta) < inputDeadZone)
        {
            commandDelta = 0f;
        }

        var desiredLeftX = _resolvedLeftX + commandDelta;
        var desiredRightX = MirrorX(desiredLeftX);
        var leftTarget = new Vector2(desiredLeftX, leftBox.Body.position.y);
        var rightTarget = new Vector2(desiredRightX, rightBox.Body.position.y);

        if (leftBox.IsBlockedAt(leftTarget, rightBox) || rightBox.IsBlockedAt(rightTarget, leftBox))
        {
            desiredLeftX = _resolvedLeftX;
            desiredRightX = _resolvedRightX;
            leftTarget.x = desiredLeftX;
            rightTarget.x = desiredRightX;
        }

        leftBox.SnapTo(leftTarget);
        rightBox.SnapTo(rightTarget);
        _resolvedLeftX = desiredLeftX;
        _resolvedRightX = desiredRightX;
    }

    private void CacheReferences()
    {
        if (leftBox == null || rightBox == null)
        {
            var boxes = GetComponentsInChildren<MirrorBoxUnit>(true);
            for (var i = 0; i < boxes.Length; i++)
            {
                if (boxes[i].Side == Level3Side.Left)
                {
                    leftBox = boxes[i];
                }
                else
                {
                    rightBox = boxes[i];
                }
            }
        }

        leftBox?.Configure(this, Level3Side.Left);
        rightBox?.Configure(this, Level3Side.Right);
    }

    private void CanonicalizePair(bool snapBodies)
    {
        if (leftBox == null || rightBox == null || leftBox.Body == null || rightBox.Body == null)
        {
            return;
        }

        var canonicalLeft = 0.5f * (leftBox.Body.position.x + MirrorX(rightBox.Body.position.x));
        _resolvedLeftX = canonicalLeft;
        _resolvedRightX = MirrorX(canonicalLeft);

        if (!snapBodies)
        {
            return;
        }

        leftBox.SnapTo(new Vector2(_resolvedLeftX, leftBox.Body.position.y));
        rightBox.SnapTo(new Vector2(_resolvedRightX, rightBox.Body.position.y));
    }

    private float MirrorX(float xPosition)
    {
        return (mirrorCenterX * 2f) - xPosition;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.1f, 0.8f, 1f, 0.8f);
        Gizmos.DrawLine(new Vector3(mirrorCenterX, -20f, 0f), new Vector3(mirrorCenterX, 20f, 0f));
    }
}
