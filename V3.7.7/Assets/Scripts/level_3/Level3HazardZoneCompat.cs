using UnityEngine;

[DisallowMultipleComponent]
public class Level3HazardZoneCompat : MonoBehaviour
{
    [Header("Mode")]
    [SerializeField] private bool disableLegacyHazardZone;
    [SerializeField] private HazardZone legacyHazardZone;

    [Header("Targets")]
    [SerializeField] private bool affectLeftPlayer = true;
    [SerializeField] private bool affectRightPlayer = true;

    [Header("Safety")]
    [SerializeField] private bool useRepeatHitCooldown = true;
    [SerializeField] private float repeatHitCooldown = 0.08f;

    private float _lastHitTime = -999f;

    private void Awake()
    {
        if (legacyHazardZone == null)
        {
            legacyHazardZone = GetComponent<HazardZone>();
        }

        if (disableLegacyHazardZone && legacyHazardZone != null)
        {
            legacyHazardZone.enabled = false;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryKillLevel3Avatar(other.GetComponentInParent<Level3PlayerAvatar>());
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryKillLevel3Avatar(collision.collider.GetComponentInParent<Level3PlayerAvatar>());
    }

    private void TryKillLevel3Avatar(Level3PlayerAvatar avatar)
    {
        if (avatar == null)
        {
            return;
        }

        if (!CanAffectSide(avatar.Side))
        {
            return;
        }

        if (useRepeatHitCooldown && Time.unscaledTime < _lastHitTime + repeatHitCooldown)
        {
            return;
        }

        _lastHitTime = Time.unscaledTime;
        avatar.Kill();
    }

    private bool CanAffectSide(Level3Side side)
    {
        if (side == Level3Side.Left)
        {
            return affectLeftPlayer;
        }

        return affectRightPlayer;
    }
}
