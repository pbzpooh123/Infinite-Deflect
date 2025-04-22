using UnityEngine;

public class UIManager : MonoBehaviour
{
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject quickCreateSessionWidget;
    [SerializeField] private GameObject sessionListWidget;
    [SerializeField] private GameObject canva;
    [SerializeField] private GameObject Mcanva;

    public void OnHostButtonClicked()
    {
        mainMenuPanel.SetActive(false);
        quickCreateSessionWidget.SetActive(true);
        canva.SetActive(false);
        Mcanva.SetActive(true);
    }

    public void OnJoinButtonClicked()
    {
        mainMenuPanel.SetActive(false);
        sessionListWidget.SetActive(true);
    }

    public void OnBackButtonClicked()
    {
        quickCreateSessionWidget.SetActive(false);
        sessionListWidget.SetActive(false);
        mainMenuPanel.SetActive(true);
    }
    
    public void OnjButtonClicked()
    {
        quickCreateSessionWidget.SetActive(false);
        sessionListWidget.SetActive(false);
        canva.SetActive(false);
        Mcanva.SetActive(true);
    }
}