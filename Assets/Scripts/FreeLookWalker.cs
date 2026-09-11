using UnityEngine;
using UnityEngine.InputSystem;

public class FreeLookWalker : MonoBehaviour
{
    public float moveSpeed = 4f;
    public float sprintMultiplier = 2f;
    public float lookSensitivity = 0.12f;
    public float minPitch = -80f;
    public float maxPitch = 80f;

    float yaw;
    float pitch;
    bool cursorLocked = true;

    void Start()
    {
        Vector3 e = transform.eulerAngles;
        yaw = e.y;
        pitch = e.x;
        SetCursorLock(true);
    }

    void Update()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb == null) return;

        if (kb.escapeKey.wasPressedThisFrame) SetCursorLock(false);
        if (mouse != null && mouse.leftButton.wasPressedThisFrame && !cursorLocked) SetCursorLock(true);

        if (cursorLocked && mouse != null)
        {
            Vector2 delta = mouse.delta.ReadValue();
            yaw += delta.x * lookSensitivity;
            pitch -= delta.y * lookSensitivity;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        Vector3 move = Vector3.zero;
        if (kb.wKey.isPressed) move += transform.forward;
        if (kb.sKey.isPressed) move -= transform.forward;
        if (kb.aKey.isPressed) move -= transform.right;
        if (kb.dKey.isPressed) move += transform.right;
        if (kb.eKey.isPressed) move += Vector3.up;
        if (kb.qKey.isPressed) move -= Vector3.up;

        move.Normalize();
        float speed = moveSpeed * (kb.leftShiftKey.isPressed ? sprintMultiplier : 1f);
        transform.position += move * speed * Time.deltaTime;
    }

    void SetCursorLock(bool locked)
    {
        cursorLocked = locked;
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
