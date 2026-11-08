using UnityEngine;

public class RoomPanelToggle : MonoBehaviour
{
    public GameObject roomPanel;

    public void OpenRoomPanel()
    {
        roomPanel.SetActive(true);
    }

    public void CloseRoomPanel()
    {
        roomPanel.SetActive(false);
    }
}