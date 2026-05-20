using UnityEngine;

[DisallowMultipleComponent]
public class Level3MirrorGhostBodyContactKill : MonoBehaviour
{
    [SerializeField] private Level3PlayerAvatar leftAvatar;
    [SerializeField] private Level3PlayerAvatar rightAvatar;
    [SerializeField] private BoxCollider2D leftGhostCollider;
    [SerializeField] private BoxCollider2D rightGhostCollider;
    [SerializeField] private float horizontalInset = 0.04f;
    [SerializeField] private float bottomInset = 0.02f;
    [SerializeField] private float topPadding = 0.45f;

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
        TryKillOnBodyContact(leftAvatar, leftGhostCollider);
        TryKillOnBodyContact(rightAvatar, rightGhostCollider);
    }

    private void TryKillOnBodyContact(Level3PlayerAvatar avatar, BoxCollider2D ghostCollider)
    {
        if (avatar == null || avatar.BodyCollider == null || !avatar.BodyCollider.enabled || ghostCollider == null)
        {
            return;
        }

        var killBounds = ResolveKillBounds(ghostCollider);
        if (killBounds.Intersects(avatar.BodyCollider.bounds))
        {
            avatar.Kill();
        }
    }

    private Bounds ResolveKillBounds(BoxCollider2D ghostCollider)
    {
        var sourceBounds = ghostCollider.bounds;
        var min = sourceBounds.min;
        var max = sourceBounds.max;

        min.x += Mathf.Max(0f, horizontalInset);
        max.x -= Mathf.Max(0f, horizontalInset);
        min.y += Mathf.Max(0f, bottomInset);
        max.y += Mathf.Max(0f, topPadding);

        if (min.x > max.x)
        {
            var centerX = sourceBounds.center.x;
            min.x = centerX;
            max.x = centerX;
        }

        if (min.y > max.y)
        {
            var centerY = sourceBounds.center.y;
            min.y = centerY;
            max.y = centerY;
        }

        var size = max - min;
        var center = min + (size * 0.5f);
        return new Bounds(center, size);
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

        if (leftGhostCollider == null)
        {
            leftGhostCollider = FindChildComponent<BoxCollider2D>("LeftGhost");
        }

        if (rightGhostCollider == null)
        {
            rightGhostCollider = FindChildComponent<BoxCollider2D>("RightGhost");
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
