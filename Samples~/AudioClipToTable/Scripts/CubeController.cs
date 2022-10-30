using UnityEngine;

public class CubeController : MonoBehaviour
{
    private CsoundUnity csoundUnity;
    public AudioClip audioClip;

    // Start is called before the first frame update
    void Start()
    {
        csoundUnity = GameObject.Find("CsoundUnity").GetComponent<CsoundUnity>();
        if (!csoundUnity)
            Debug.LogError("Can't find CsoundUnity?");

        var name = "Samples/" + audioClip.name;

        var samplesStereo = CsoundUnity.GetSamples(name, 1, true);
        var samplesMono = CsoundUnity.GetSamples(name, 1, false);

        if (csoundUnity.CreateTable(9000, samplesStereo) == -1)
            Debug.LogError("Couldn't create table");
        if (csoundUnity.CreateTable(9001, samplesMono) == -1)
            Debug.LogError("Couldn't create table");
    }
}
