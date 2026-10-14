using UnityEngine;

// Mixes up which script sits on which parkour goal, so every game is different.
// It runs in Awake, which happens before the scripts remember their starting spot in Start.
public class ScriptShuffle : MonoBehaviour
{
    public ScriptSheet[] sheets;

    // the layout from last time, so pressing R doesn't give you the same one again
    static string lastLayout = "";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetWhenGameStarts()
    {
        lastLayout = "";
    }

    void Awake()
    {
        int count = sheets.Length;

        // remember every spot (position and which way it faces)
        Vector3[] spots = new Vector3[count];
        Quaternion[] turns = new Quaternion[count];
        for (int i = 0; i < count; i++)
        {
            spots[i] = sheets[i].transform.position;
            turns[i] = sheets[i].transform.rotation;
        }

        // shuffle the spot numbers (Fisher-Yates shuffle)
        int[] order = new int[count];
        for (int tries = 0; tries < 10; tries++)
        {
            for (int i = 0; i < count; i++) order[i] = i;
            for (int i = count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                int temp = order[i];
                order[i] = order[j];
                order[j] = temp;
            }

            string layout = string.Join(",", order);
            if (layout != lastLayout)
            {
                lastLayout = layout;
                break;
            }
        }

        // move every script to its new spot
        for (int i = 0; i < count; i++)
        {
            sheets[i].transform.SetPositionAndRotation(spots[order[i]], turns[order[i]]);

            // move the physics body too, or it pulls the script back to its old spot
            Rigidbody body = sheets[i].GetComponent<Rigidbody>();
            if (body != null)
            {
                body.position = spots[order[i]];
                body.rotation = turns[order[i]];
            }
        }
        Physics.SyncTransforms();
    }
}
