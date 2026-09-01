using UnityEngine;
using UnityEngine.EventSystems;

public class ButtonTemplate : MonoBehaviour, IPointerEnterHandler,
    IPointerExitHandler
{
    [SerializeField] public GameObject pointer;

    private void Start()
    {
        // As outras três chamadas já checavam null; só esta não checava, e um botão sem
        // pointer atribuído no inspetor derrubava o Start com NullReferenceException.
        if (pointer != null)
            pointer.SetActive(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (pointer != null)
            pointer.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (pointer != null)
            pointer.SetActive(false);  
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (pointer != null)
            pointer.SetActive(false);
    }

}
