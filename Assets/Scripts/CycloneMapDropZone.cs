using UnityEngine;
using UnityEngine.UI;

public sealed class CycloneMapDropZone : MonoBehaviour
{
    private static readonly Color PlacedColor = new Color(0.9f, 0.06f, 0.04f, 1f);

    [SerializeField] private Image image;
    [SerializeField] private bool proneArea;
    [SerializeField] private Sprite notificationSprite;

    private Color originalColor;
    private bool isPlaced;

    public bool CanAcceptMarker => proneArea && !isPlaced;
    public Sprite NotificationSprite => notificationSprite;

    public void Configure()
    {
        originalColor = image != null ? image.color : Color.white;

        if (image != null)
        {
            image.raycastTarget = true;
        }
    }

    public bool Contains(GameObject candidate)
    {
        return candidate != null && (candidate == gameObject || candidate.transform.IsChildOf(transform));
    }

    public void ResetZone()
    {
        isPlaced = false;

        if (image != null)
        {
            image.color = originalColor;
        }
    }

    public void MarkPlaced()
    {
        isPlaced = true;

        if (image != null)
        {
            image.color = PlacedColor;
        }
    }
}
