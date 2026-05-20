using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public class MirrorBoxUnit : PushableBox
{
    [SerializeField] private MirrorBoxPairController pairController;
    [SerializeField] private Level3Side side = Level3Side.Left;
    [SerializeField] private BoxCollider2D boxCollider;

    public MirrorBoxPairController PairController => pairController;
    public Level3Side Side => side;
    public Rigidbody2D Body => attachedBody;
    public BoxCollider2D BoxCollider => boxCollider;

    protected override void Awake()
    {
        base.Awake();
        CacheReferences();
    }

    private void OnValidate()
    {
        CacheReferences();
    }

    public void Configure(MirrorBoxPairController owner, Level3Side boxSide)
    {
        pairController = owner;
        side = boxSide;
        CacheReferences();
    }

    public void SnapTo(Vector2 targetPosition)
    {
        CacheReferences();
        transform.position = new Vector3(targetPosition.x, targetPosition.y, transform.position.z);
        if (attachedBody != null)
        {
            attachedBody.position = targetPosition;
            attachedBody.velocity = new Vector2(0f, attachedBody.velocity.y);
            attachedBody.angularVelocity = 0f;
        }
    }

    public bool IsBlockedAt(Vector2 targetPosition, MirrorBoxUnit pairedBox)
    {
        CacheReferences();
        if (boxCollider == null)
        {
            return false;
        }

        var delta = targetPosition - (Vector2)transform.position;
        var center = (Vector2)boxCollider.bounds.center + delta;
        var size = boxCollider.bounds.size * 0.98f;
        var hits = Physics2D.OverlapBoxAll(center, size, 0f);
        for (var i = 0; i < hits.Length; i++)
        {
            var hit = hits[i];
            if (hit == null || hit == boxCollider || hit == pairedBox?.boxCollider || hit.isTrigger)
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

    private void CacheReferences()
    {
        if (attachedBody == null)
        {
            attachedBody = GetComponent<Rigidbody2D>();
        }

        if (boxCollider == null)
        {
            boxCollider = GetComponent<BoxCollider2D>();
        }
    }
}
