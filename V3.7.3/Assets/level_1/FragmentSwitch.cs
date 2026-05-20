using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 碎片触发器。若与同物体上的 <see cref="LevelTransitionTrigger2D"/> 共用触发器，
/// 直接 <see cref="SceneManager.LoadScene"/> 会在同一帧卸载场景，导致 <see cref="LevelTransitionSequence"/> 无法播放。
/// 当场景中存在 <see cref="LevelTransitionSequence"/> 时，只走转场，不直接切场景。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class FragmentSwitch : MonoBehaviour
{
    [SerializeField] private string nextSceneName = "Level_1";

    [Tooltip("为 true：场景里有 LevelTransitionSequence 时只调用 Play()，绝不 LoadScene。")]
    [SerializeField] private bool deferToLevelTransitionSequence = true;

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
        if (!IsPlayer(other))
        {
            return;
        }

        if (deferToLevelTransitionSequence)
        {
            var seq = FindObjectOfType<LevelTransitionSequence>();
            if (seq != null)
            {
                if (!seq.IsPlaying)
                {
                    Debug.Log("碎片触发：使用 LevelTransitionSequence 转场", this);
                    seq.Play();
                }

                return;
            }
        }

        Debug.Log("拾取碎片，切换场景", this);
        if (!string.IsNullOrWhiteSpace(nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName);
        }
    }

    private static bool IsPlayer(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            return true;
        }

        return other.GetComponentInParent<PlayerController2D>() != null;
    }
}
