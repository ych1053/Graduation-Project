
using UnityEngine;

public class ChestDemo : MonoBehaviour
{
    public Animator chestAnim;

    bool isOpen = false;

    void Awake()
    {
        chestAnim = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.E) && !isOpen)
        {
            chestAnim.SetTrigger("open");
            isOpen = true;
        }
    }
}
