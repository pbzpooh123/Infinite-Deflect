using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager instance;

    [SerializeField]
    private GameObject InMenuPanel;

    private void Awake()
    {
        instance = this;
    }
    public void ToggleInMenuPanel()
    {
        if (!InMenuPanel.activeInHierarchy)
        {
            InMenuPanel.SetActive(true);
            Debug.Log("Active InMenu");
        }
        else
        {
            InMenuPanel.SetActive(false);
        }
    }
}
