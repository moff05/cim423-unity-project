using System.Collections;
using UnityEngine;

// Grab the power cell, carry it across the room and bring it up to the
// reactor socket, where it locks in on its own. Let go early and it drifts
// back to its rack, so it can never be lost or thrown out of reach.
public class CarryCell : HoldInteractable
{
    public Transform socket;
    public Renderer socketGlow;
    public float seatRadius = 0.55f;

    Vector3 homePos;
    Quaternion homeRot;
    Coroutine returning;
    FloatingHintText socketHint;
    static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
    MaterialPropertyBlock socketMpb;

    protected override void Awake()
    {
        base.Awake();
        homePos = transform.position;
        homeRot = transform.rotation;
        socketMpb = new MaterialPropertyBlock();
        if (socket != null) socketHint = FloatingHintText.Attach(socket, 0.5f);
    }

    protected override void OnHoldStart(Vector3 handPos)
    {
        if (returning != null) { StopCoroutine(returning); returning = null; }
        if (socketHint != null) socketHint.Show("BRING THE CELL HERE");
    }

    protected override void OnHold(Vector3 handPos)
    {
        Vector3 target = handPos + hand.forward * 0.18f;
        transform.position = Vector3.Lerp(transform.position, target, Time.deltaTime * 14f);
        transform.rotation = Quaternion.Slerp(transform.rotation, hand.rotation * Quaternion.Euler(90f, 0f, 0f), Time.deltaTime * 10f);
        PulseSocket();
        if (socket != null && Vector3.Distance(transform.position, socket.position) <= seatRadius)
            Seat();
    }

    protected override void OnHoldEnd()
    {
        if (socketHint != null) socketHint.Hide();
        SetSocketGlow(Color.black);
        if (!done) returning = StartCoroutine(ReturnHome());
    }

    void PulseSocket()
    {
        SetSocketGlow(InteractionSequencer.PulseColor(3f, 0.2f, 0.5f));
    }

    void SetSocketGlow(Color c)
    {
        if (socketGlow == null) return;
        socketMpb.SetColor(EmissionColor, c);
        socketGlow.SetPropertyBlock(socketMpb);
    }

    void Seat()
    {
        transform.SetPositionAndRotation(socket.position, socket.rotation);
        foreach (var col in GetComponentsInChildren<Collider>()) col.enabled = false;
        if (socketHint != null) socketHint.Hide();
        SetSocketGlow(Color.black);
        Complete();
    }

    IEnumerator ReturnHome()
    {
        while ((transform.position - homePos).sqrMagnitude > 0.0004f)
        {
            transform.position = Vector3.MoveTowards(transform.position, homePos, Time.deltaTime * 3f);
            transform.rotation = Quaternion.Slerp(transform.rotation, homeRot, Time.deltaTime * 8f);
            yield return null;
        }
        transform.SetPositionAndRotation(homePos, homeRot);
        returning = null;
    }

    protected override IEnumerator DesktopRoutine()
    {
        auto = true;
        Vector3 from = transform.position;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / 1.4f;
            transform.position = Vector3.Lerp(from, socket.position, Mathf.SmoothStep(0f, 1f, t)) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.25f;
            transform.rotation = Quaternion.Slerp(homeRot, socket.rotation, t);
            PulseSocket();
            yield return null;
        }
        Seat();
    }
}
