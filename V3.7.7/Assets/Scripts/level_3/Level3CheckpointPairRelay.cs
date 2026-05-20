using UnityEngine;

[DisallowMultipleComponent]
public class Level3CheckpointPairRelay : MonoBehaviour
{
    [SerializeField] private Level3TeamRespawnCoordinator coordinator;
    [SerializeField] private Level3Checkpoint leftCheckpoint;
    [SerializeField] private Level3Checkpoint rightCheckpoint;

    private void Awake()
    {
        CacheReferences();
    }

    private void OnValidate()
    {
        CacheReferences();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        var avatar = other.GetComponentInParent<Level3PlayerAvatar>();
        if (avatar == null)
        {
            return;
        }

        CacheReferences();
        if (coordinator == null)
        {
            return;
        }

        coordinator.ActivateCheckpointPair(leftCheckpoint, rightCheckpoint);

        var leftAvatar = coordinator.LeftAvatar;
        if (leftAvatar != null && leftCheckpoint != null)
        {
            leftAvatar.SetCheckpoint(leftCheckpoint);
        }

        var rightAvatar = coordinator.RightAvatar;
        if (rightAvatar != null && rightCheckpoint != null)
        {
            rightAvatar.SetCheckpoint(rightCheckpoint);
        }
    }

    private void CacheReferences()
    {
        if (coordinator == null)
        {
            var coordinators = FindObjectsOfType<Level3TeamRespawnCoordinator>(true);
            if (coordinators.Length > 0)
            {
                coordinator = coordinators[0];
            }
        }
    }
}
