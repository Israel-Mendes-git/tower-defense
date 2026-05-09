using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
    }
    public void LoadScene(string scene)
    {
        SceneManager.LoadScene(scene);
    }

    public void ExitGameBtn()
    {
        Application.Quit();
    }

    public void ReturnBtn(GameObject menu)
    {
        menu.gameObject.SetActive(false);
    }

    public void EnableBtn(GameObject menu)
    {
        menu.gameObject.SetActive(true);
    }
    
}
