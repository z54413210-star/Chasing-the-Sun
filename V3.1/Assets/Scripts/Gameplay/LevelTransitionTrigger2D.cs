using UnityEngine;

/// <summary>
/// 玩家进入触发器后播放 <see cref="LevelTransitionSequence"/>（关卡间长转场）。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class LevelTransitionTrigger2D : MonoBehaviour
{
    [SerializeField] private LevelTransitionSequence transition;
    [SerializeField] private bool playOnce = true;
    [SerializeField] private bool disableColliderAfterPlay = true;

    private bool _played;

    private void Reset()
    {
        var col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_played && playOnce)
        {
            return;
        }

        if (other.GetComponentInParent<PlayerController2D>() == null)
        {
            return;
        }

        var t = transition != null ? transition : FindObjectOfType<LevelTransitionSequence>();
        if (t == null || t.IsPlaying)
        {
            return;
        }

        _played = true;
        t.Play();

        if (disableColliderAfterPlay && playOnce)
        {
            var col = GetComponent<Collider2D>();
            if (col != null)
            {
                col.enabled = false;
            }
        }
    }
}
