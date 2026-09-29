using UnityEngine;
using UnityEngine.InputSystem;

public class FreeLookWalker : MonoBehaviour
{
    public float moveSpeed = 4f;
    public float sprintMultiplier = 2f;
    public float lookSensitivity = 0.12f;
    public float minPitch = -80f;
    public float maxPitch = 80f;

    public float bobFrequency = 9f;
    public float bobAmplitude = 0.045f;

    // This rig was dead code until desktop testing became the Editor
    // default (see PlatformRigSelector) - movement was raw transform
    // translation with no collision check at all, so walking straight
    // through walls was never actually possible to notice before.
    public float collisionRadius = 0.3f;
    public LayerMask collisionMask = ~0;

    float yaw;
    float pitch;
    bool cursorLocked = true;
    float bobTimer;
    Vector3 previousBobOffset;

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

        // Undo last frame's bob first, so an external teleport (e.g. the
        // Airlock moving the player into the Corridor) that changed
        // transform.position between frames isn't clobbered below.
        transform.position -= previousBobOffset;

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
        Vector3 moveDelta = move * speed * Time.deltaTime;

        if (moveDelta.sqrMagnitude > 0f)
        {
            float dist = moveDelta.magnitude;
            Vector3 dir = moveDelta / dist;
            const float skin = 0.05f;
            if (Physics.SphereCast(transform.position, collisionRadius, dir, out RaycastHit hit, dist + skin, collisionMask, QueryTriggerInteraction.Ignore))
                moveDelta = dir * Mathf.Max(0f, hit.distance - skin);
        }

        transform.position += moveDelta;

        // Subtle head-bob so moving reads as walking, not floating.
        bool isMoving = move.sqrMagnitude > 0.01f;
        bobTimer = isMoving ? bobTimer + Time.deltaTime * bobFrequency : 0f;
        float bobY = isMoving ? Mathf.Sin(bobTimer) * bobAmplitude : 0f;
        float bobX = isMoving ? Mathf.Cos(bobTimer * 0.5f) * bobAmplitude * 0.5f : 0f;
        previousBobOffset = transform.up * bobY + transform.right * bobX;
        transform.position += previousBobOffset;
    }

    void SetCursorLock(bool locked)
    {
        cursorLocked = locked;
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
