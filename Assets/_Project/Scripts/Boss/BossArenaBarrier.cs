using UnityEngine;

[DisallowMultipleComponent]
public sealed class BossArenaBarrier : MonoBehaviour
{
    [SerializeField] private Collider2D[] blockerColliders;
    [SerializeField] private GameObject openPresentationRoot;
    [SerializeField] private GameObject closedPresentationRoot;

    public bool IsOpen { get; private set; } = true;
    public Collider2D[] BlockerColliders => blockerColliders;
    public GameObject OpenPresentationRoot => openPresentationRoot;
    public GameObject ClosedPresentationRoot => closedPresentationRoot;

    public void SetOpen(bool open, bool immediate)
    {
        IsOpen = open;

        if (blockerColliders != null)
        {
            for (int i = 0; i < blockerColliders.Length; i++)
            {
                if (blockerColliders[i] != null)
                {
                    blockerColliders[i].enabled = !open;
                }
            }
        }

        if (openPresentationRoot != null)
        {
            openPresentationRoot.SetActive(open);
        }

        if (closedPresentationRoot != null)
        {
            closedPresentationRoot.SetActive(!open);
        }
    }
}
