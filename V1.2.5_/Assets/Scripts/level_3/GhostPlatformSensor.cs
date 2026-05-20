using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class GhostPlatformSensor : MonoBehaviour
{
    [SerializeField] private Level3Side acceptedSide = Level3Side.Left;

    private readonly HashSet<Level3PlayerAvatar> _occupants = new HashSet<Level3PlayerAvatar>();

    private void Reset()
    {
        var collider2D = GetComponent<Collider2D>();
        collider2D.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        var avatar = other.GetComponentInParent<Level3PlayerAvatar>();
        if (avatar != null && avatar.Side == acceptedSide)
        {
            _occupants.Add(avatar);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        var avatar = other.GetComponentInParent<Level3PlayerAvatar>();
        if (avatar != null)
        {
            _occupants.Remove(avatar);
        }
    }

    public Level3PlayerAvatar GetClosestAvatar(Vector3 fromPosition)
    {
        Level3PlayerAvatar closest = null;
        var bestSqrDistance = float.MaxValue;

        foreach (var occupant in _occupants)
        {
            if (occupant == null || occupant.BodyCollider == null)
            {
                continue;
            }

            var sqrDistance = (occupant.Body.position - (Vector2)fromPosition).sqrMagnitude;
            if (sqrDistance < bestSqrDistance)
            {
                bestSqrDistance = sqrDistance;
                closest = occupant;
            }
        }

        return closest;
    }

    public IEnumerable<Level3PlayerAvatar> GetOccupants()
    {
        foreach (var occupant in _occupants)
        {
            if (occupant != null)
            {
                yield return occupant;
            }
        }
    }
}
