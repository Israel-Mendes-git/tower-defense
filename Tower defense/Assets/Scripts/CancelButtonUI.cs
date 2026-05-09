using UnityEngine;

public class CancelBuildUI : MonoBehaviour
{
    [SerializeField] private GameObject cancelButton; // arraste o GameObject do botão aqui

    private void Update()
    {
        if (BuildManager.main == null) return;

        bool show = BuildManager.main.CanBuild();
        if (cancelButton.activeSelf != show)
        {
            cancelButton.SetActive(show);
        }
    }

    public void Cancel()
    {
        if (BuildManager.main != null)
        {
            BuildManager.main.ClearSelection();
        }
    }
}