using UnityEngine;
using UnityEngine.UI;

public sealed class CycloneMapDropZone : MonoBehaviour
{
    private static readonly Color PlacedColor = new Color(0.9f, 0.06f, 0.04f, 1f);

    private Image image;
    private Color originalColor;
    private bool isProneArea;
    private bool isPlaced;

    public bool CanAcceptMarker => isProneArea && !isPlaced;

    public void Initialize(bool proneArea, Image areaImage)
    {
        isProneArea = proneArea;
        image = areaImage;
        originalColor = image != null ? image.color : Color.white;
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
