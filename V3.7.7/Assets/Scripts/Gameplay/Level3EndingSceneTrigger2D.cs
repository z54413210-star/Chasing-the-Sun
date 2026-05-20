using System;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class Level3EndingSceneTrigger2D : MonoBehaviour
{
    [SerializeField] private string sceneName = "Level3EndingVideo";
    [SerializeField] private bool playOnce = true;
    [SerializeField] private bool disableColliderAfterTrigger = true;

    private bool _triggered;

    private void Reset()
    {
        var collider2D = GetComponent<Collider2D>();
        if (collider2D != null)
        {
            collider2D.isTrigger = true;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_triggered && playOnce)
        {
            return;
        }

        if (other.GetComponentInParent<PlayerController2D>() == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError(nameof(Level3EndingSceneTrigger2D) + " cannot load scene because the scene name is empty.", this);
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError(nameof(Level3EndingSceneTrigger2D) + " cannot load scene '" + sceneName + "'. Make sure it exists and is added to Build Settings.", this);
            return;
        }

        _triggered = true;

        try
        {
            SceneManager.LoadScene(sceneName);
        }
        catch (Exception exception)
        {
            _triggered = false;
            Debug.LogError(nameof(Level3EndingSceneTrigger2D) + " failed to load scene '" + sceneName + "'. " + exception.Message, this);
            return;
        }

        if (disableColliderAfterTrigger && playOnce)
        {
            var collider2D = GetComponent<Collider2D>();
            if (collider2D != null)
            {
                collider2D.enabled = false;
            }
        }
    }
}
