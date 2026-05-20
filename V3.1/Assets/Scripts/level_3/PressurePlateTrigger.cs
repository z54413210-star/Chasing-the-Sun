using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class PressurePlateTrigger : MonoBehaviour
{
    [SerializeField] private bool pressed;

    private readonly HashSet<Collider2D> _occupants = new HashSet<Collider2D>();

    public bool IsPressed => pressed;
    public event Action<bool> StateChanged;

    private void Reset()
    {
        var collider2D = GetComponent<Collider2D>();
        collider2D.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!CanPressPlate(other))
        {
            return;
        }

        _occupants.Add(other);
        RefreshPressedState();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!CanPressPlate(other))
        {
            return;
        }

        _occupants.Remove(other);
        RefreshPressedState();
    }

    private void RefreshPressedState()
    {
        var nextPressed = false;
        foreach (var occupant in _occupants)
        {
            if (occupant != null)
            {
                nextPressed = true;
                break;
            }
        }

        if (nextPressed == pressed)
        {
            return;
        }

        pressed = nextPressed;
        StateChanged?.Invoke(pressed);
    }

    private static bool CanPressPlate(Collider2D other)
    {
        if (other == null)
        {
            return false;
        }

        if (other.GetComponentInParent<Level3PlayerAvatar>() != null)
        {
            return true;
        }

        return other.GetComponentInParent<PushableBox>() != null;
    }
}
