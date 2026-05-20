using UnityEngine;

[DisallowMultipleComponent]
public class Level3MirrorGhostVisualContactKill : MonoBehaviour
{
    [SerializeField] private Level3PlayerAvatar leftAvatar;
    [SerializeField] private Level3PlayerAvatar rightAvatar;
    [SerializeField] private SpriteRenderer leftGhostRenderer;
    [SerializeField] private SpriteRenderer rightGhostRenderer;
    [SerializeField] private Collider2D leftFallbackCollider;
    [SerializeField] private Collider2D rightFallbackCollider;
    [SerializeField] private Vector2 visualBoundsInset = Vector2.zero;

    private void Awake()
    {
        CacheReferences();
    }

    private void OnValidate()
    {
        CacheReferences();
    }

    private void LateUpdate()
    {
        CacheReferences();
        TryKillOnContact(leftAvatar, leftGhostRenderer, leftFallbackCollider);
        TryKillOnContact(rightAvatar, rightGhostRenderer, rightFallbackCollider);
    }

    private void TryKillOnContact(Level3PlayerAvatar avatar, SpriteRenderer ghostRenderer, Collider2D fallbackCollider)
    {
        if (avatar == null || avatar.BodyCollider == null || !avatar.BodyCollider.enabled)
        {
            return;
        }

        var killBounds = ResolveKillBounds(ghostRenderer, fallbackCollider);
        if (!killBounds.HasValue)
        {
            return;
        }

        if (killBounds.Value.Intersects(avatar.BodyCollider.bounds))
        {
            avatar.Kill();
        }
    }

    private Bounds? ResolveKillBounds(SpriteRenderer ghostRenderer, Collider2D fallbackCollider)
    {
        if (ghostRenderer != null)
        {
            return InsetBounds(ghostRenderer.bounds, visualBoundsInset);
        }

        if (fallbackCollider != null)
        {
            return InsetBounds(fallbackCollider.bounds, visualBoundsInset);
        }

        return null;
    }

    private static Bounds InsetBounds(Bounds sourceBounds, Vector2 inset)
    {
        var size = sourceBounds.size;
        size.x = Mathf.Max(0.001f, size.x - (Mathf.Max(0f, inset.x) * 2f));
        size.y = Mathf.Max(0.001f, size.y - (Mathf.Max(0f, inset.y) * 2f));
        return new Bounds(sourceBounds.center, size);
    }

    private void CacheReferences()
    {
        if (leftAvatar == null || rightAvatar == null)
        {
            var avatars = FindObjectsOfType<Level3PlayerAvatar>(true);
            for (var i = 0; i < avatars.Length; i++)
            {
                var avatar = avatars[i];
                if (avatar == null)
                {
                    continue;
                }

                if (avatar.Side == Level3Side.Left && leftAvatar == null)
                {
                    leftAvatar = avatar;
                }
                else if (avatar.Side == Level3Side.Right && rightAvatar == null)
                {
                    rightAvatar = avatar;
                }
            }
        }

        if (leftGhostRenderer == null)
        {
            leftGhostRenderer = FindChildComponent<SpriteRenderer>("LeftGhost");
        }

        if (rightGhostRenderer == null)
        {
            rightGhostRenderer = FindChildComponent<SpriteRenderer>("RightGhost");
        }

        if (leftFallbackCollider == null)
        {
            leftFallbackCollider = FindChildComponent<Collider2D>("LeftGhost");
        }

        if (rightFallbackCollider == null)
        {
            rightFallbackCollider = FindChildComponent<Collider2D>("RightGhost");
        }
    }

    private T FindChildComponent<T>(string childName) where T : Component
    {
        var transforms = GetComponentsInChildren<Transform>(true);
        for (var i = 0; i < transforms.Length; i++)
        {
            var child = transforms[i];
            if (child == null || child.name != childName)
            {
                continue;
            }

            return child.GetComponent<T>();
        }

        return null;
    }
}
