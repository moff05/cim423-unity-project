using System.Collections;
using UnityEngine;

// Radial burst of light streaks for the hyperspace jump-cut, parented to the
// cinematic camera so it reads regardless of the rig's world position.
public class HyperspaceEffect : MonoBehaviour
{
    public GameObject[] streaks;
    public float burstDuration = 0.35f;

    public IEnumerator Play()
    {
        foreach (var s in streaks)
        {
            if (s == null) continue;
            s.SetActive(true);
            var sc = s.transform.localScale;
            s.transform.localScale = new Vector3(sc.x, sc.y, 0.05f);
        }

        float t = 0f;
        while (t < burstDuration)
        {
            t += Time.deltaTime;
            float u = t / burstDuration;
            foreach (var s in streaks)
            {
                if (s == null) continue;
                var sc = s.transform.localScale;
                s.transform.localScale = new Vector3(sc.x, sc.y, Mathf.Lerp(0.05f, 10f, u * u));
            }
            yield return null;
        }

        foreach (var s in streaks) if (s != null) s.SetActive(false);
    }
}
