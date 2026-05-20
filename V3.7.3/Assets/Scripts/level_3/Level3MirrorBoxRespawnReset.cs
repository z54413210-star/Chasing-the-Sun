using System.Reflection;
using UnityEngine;

[DisallowMultipleComponent]
public class Level3MirrorBoxRespawnReset : MonoBehaviour, ILevel3TeamRespawnResettable
{
    [SerializeField] private MirrorBoxPairController pairController;
    [SerializeField] private MirrorBoxUnit leftBox;
    [SerializeField] private MirrorBoxUnit rightBox;

    private static readonly BindingFlags PrivateInstanceFlags = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly MethodInfo CanonicalizePairMethod = typeof(MirrorBoxPairController).GetMethod("CanonicalizePair", PrivateInstanceFlags);
    private static readonly object[] CanonicalizeArguments = { true };

    private void Awake()
    {
        CacheReferences();
    }

    private void OnValidate()
    {
        CacheReferences();
    }

    public void RestoreForTeamRespawn()
    {
        CacheReferences();

        if (leftBox != null)
        {
            leftBox.RestoreRespawnState();
        }

        if (rightBox != null)
        {
            rightBox.RestoreRespawnState();
        }

        if (pairController != null && CanonicalizePairMethod != null)
        {
            CanonicalizePairMethod.Invoke(pairController, CanonicalizeArguments);
        }

        Physics2D.SyncTransforms();
    }

    private void CacheReferences()
    {
        if (pairController == null)
        {
            pairController = GetComponent<MirrorBoxPairController>();
        }

        if (leftBox == null || rightBox == null)
        {
            var boxes = GetComponentsInChildren<MirrorBoxUnit>(true);
            for (var i = 0; i < boxes.Length; i++)
            {
                if (boxes[i] == null)
                {
                    continue;
                }

                if (boxes[i].Side == Level3Side.Left && leftBox == null)
                {
                    leftBox = boxes[i];
                }
                else if (boxes[i].Side == Level3Side.Right && rightBox == null)
                {
                    rightBox = boxes[i];
                }
            }
        }
    }
}
