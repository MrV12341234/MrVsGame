using UnityEngine;

public class RoomItemButton : MonoBehaviour
{
    public string RoomName;

    public void OnButtonPressed()
    {
        RoomList.Instance.JoinRoomByName(RoomName);
    }
}
