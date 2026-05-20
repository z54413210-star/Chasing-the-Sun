using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
[DisallowMultipleComponent]
public class CameraBounds2D : MonoBehaviour
{
    [Header("便捷设置")]
    [Tooltip("主背景 SpriteRenderer。延长地图后可在下方 Extra 中拖入拼接段，再执行右键菜单「自动对齐边界到背景」。")]
    [SerializeField] private SpriteRenderer backgroundReference;

    [Tooltip("横向或纵向接长的每一块背景（复制出来的 SpriteRenderer）。会与 Background Reference 合并计算相机可移动范围。")]
    [SerializeField] private SpriteRenderer[] extraBackgroundPieces;

    [HideInInspector]
    [SerializeField] private BoxCollider2D boundsCollider;

    private void Reset()
    {
        CacheCollider();
        boundsCollider.isTrigger = true;
        boundsCollider.size = new Vector2(24f, 12f);
    }

    // 这是一个非常实用的编辑器魔法，可以在脚本的右键菜单里多出一个选项
    [ContextMenu("自动对齐边界到背景")]
    public void FitToBackground()
    {
        CacheCollider();

        var pieces = new List<SpriteRenderer>();
        if (backgroundReference != null)
        {
            pieces.Add(backgroundReference);
        }

        if (extraBackgroundPieces != null)
        {
            foreach (var s in extraBackgroundPieces)
            {
                if (s != null && !pieces.Contains(s))
                {
                    pieces.Add(s);
                }
            }
        }

        if (pieces.Count == 0)
        {
            Debug.LogWarning("⚠️ 请至少将一张背景图拖入 Background Reference，或在 Extra Background Pieces 中加入拼接段。");
            return;
        }

        var worldBounds = pieces[0].bounds;
        for (var i = 1; i < pieces.Count; i++)
        {
            worldBounds.Encapsulate(pieces[i].bounds);
        }

        var z = transform.position.z;
        transform.position = new Vector3(worldBounds.center.x, worldBounds.center.y, z);
        boundsCollider.offset = Vector2.zero;
        boundsCollider.size = new Vector2(worldBounds.size.x, worldBounds.size.y);

        Debug.Log(pieces.Count > 1
            ? $"✅ 相机边界已对齐到 {pieces.Count} 张背景图的整体范围。"
            : "✅ 相机边界已成功对齐到背景图大小！");
    }

    public Vector3 ClampPosition(Camera cameraComponent, Vector3 desiredPosition)
    {
        CacheCollider();

        if (cameraComponent == null || !cameraComponent.orthographic || boundsCollider == null)
        {
            return desiredPosition;
        }

        var bounds = boundsCollider.bounds;
        var verticalHalf = cameraComponent.orthographicSize;
        var horizontalHalf = verticalHalf * cameraComponent.aspect;

        // 防止相机视口比边界还大导致的锁死卡顿（上一回合修复的核心）
        if (bounds.size.x < horizontalHalf * 2f || bounds.size.y < verticalHalf * 2f)
        {
            return desiredPosition;
        }

        // 计算相机的运动极限坐标
        var minX = bounds.min.x + horizontalHalf;
        var maxX = bounds.max.x - horizontalHalf;
        var minY = bounds.min.y + verticalHalf;
        var maxY = bounds.max.y - verticalHalf;

        // 核心逻辑：将相机的目标坐标“钳制”在这个计算好的最大/最小范围内
        desiredPosition.x = Mathf.Clamp(desiredPosition.x, minX, maxX);
        desiredPosition.y = Mathf.Clamp(desiredPosition.y, minY, maxY);

        return desiredPosition;
    }

    private void OnDrawGizmosSelected()
    {
        CacheCollider();
        if (boundsCollider == null) return;

        // 在 Scene 视图里画出一个半透明的青色框，方便你直观看到相机的活动范围
        Gizmos.color = new Color(0.1f, 0.8f, 0.8f, 0.35f);
        Gizmos.DrawCube(boundsCollider.bounds.center, boundsCollider.bounds.size);
        Gizmos.color = new Color(0.1f, 0.8f, 0.8f, 0.95f);
        Gizmos.DrawWireCube(boundsCollider.bounds.center, boundsCollider.bounds.size);
    }

    private void CacheCollider()
    {
        if (boundsCollider == null)
        {
            boundsCollider = GetComponent<BoxCollider2D>();
        }
    }
}