using UnityEngine;

// Constant slow rotation, for the artifact and its rings.
public class SlowSpin : MonoBehaviour
{
    public Vector3 degreesPerSecond = new Vector3(0f, 12f, 0f);

    void Update()
    {
        transform.Rotate(degreesPerSecond * Time.deltaTime, Space.Self);
    }
}
