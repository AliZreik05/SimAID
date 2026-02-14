using UnityEngine;

public class CursorLock : MonoBehaviour
{

    private void Start()
    {
        LockCursor();
    }

    private void Update()
    {
        // Optional: press Esc to free cursor
        if (Input.GetKeyDown(KeyCode.Escape))
            UnlockCursor();
    }

    public void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;   // locks to center
        Cursor.visible = false;
    }

    public void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;     // free cursor
        Cursor.visible = true;
    }
}
