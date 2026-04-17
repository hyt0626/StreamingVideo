using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Encryption : MonoBehaviour
{
    public bool isEncrypt = true; // 是否加密，false 则解密
    void Start()
    {
        string _filePath = "sdcard/video.mp4";
        if (isEncrypt)
        {
            Encrypt.Encryption("happyMaster",_filePath);
        }
        else
        {
            Encrypt.Decryption("happyMaster",_filePath);
        }

    }

}
