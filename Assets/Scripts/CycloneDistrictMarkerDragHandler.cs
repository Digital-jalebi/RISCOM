using UnityEngine;
using UnityEngine.EventSystems;

public sealed class CycloneDistrictMarkerDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private CycloneMissionController missionController;

    public void Initialize(CycloneMissionController controller)
    {
        missionController = controller;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        missionController?.BeginMarkerDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        missionController?.MoveMarker(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        missionController?.TryPlaceMarker(eventData);
    }
}
