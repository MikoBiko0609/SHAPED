using UnityEngine;

public class DoorController : MonoBehaviour
{
    public Animator anim;
    public string openState = "DoorOpen";  
    public string closedState = "DoorClosed";

    public void OpenDoor()
    {
        if (anim == null) return;

        anim.CrossFadeInFixedTime(openState, 0.1f);

   }

    public void CloseDoor()
    {
        if (anim == null) return;

        anim.CrossFadeInFixedTime(closedState, 0.1f);
    }
}
