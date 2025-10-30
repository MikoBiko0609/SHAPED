using UnityEngine;

public class DoorController : MonoBehaviour
{
    public Animator anim;
    public string openState = "DoorOpen";   // your animation state name
    public string closedState = "DoorClosed";

    public void OpenDoor()
    {
        if (anim == null) return;

        anim.CrossFadeInFixedTime(openState, 0.1f);

        // Optional: ensure it stays open if player reloads next scene
        anim.SetBool("IsOpen", true);
    }

    public void CloseDoor()
    {
        if (anim == null) return;

        anim.CrossFadeInFixedTime(closedState, 0.1f);
        anim.SetBool("IsOpen", false);
    }
}
