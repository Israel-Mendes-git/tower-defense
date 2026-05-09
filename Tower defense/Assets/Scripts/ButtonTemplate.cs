using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

public class ButtonTemplate : MonoBehaviour, IPointerEnterHandler,
    IPointerExitHandler
{
    [SerializeField] public GameObject pointer;

    private void Start()
    {
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
