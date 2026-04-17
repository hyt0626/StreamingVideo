using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Encryption : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        string _filePath = "sdcard/video.mp4";

        Encrypt.Encryption("happyMaster",_filePath);
    }

}
