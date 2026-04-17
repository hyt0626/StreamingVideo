using System.IO;
using System.Text;
using System.Security.Cryptography;
public class Encrypt
{
    /// <summary>
    /// 加密
    /// </summary>
    public static void Encryption(string key, string readfile)
    {
        StartCrypto(HashKey(key), readfile, false);
    }
    /// <summary>
    /// 解密
    /// </summary>
    public static void Decryption(string key, string readfile)
    {
        StartCrypto(HashKey(key), readfile, true);
    }
    /// <summary>
    /// 开始使用密码
    /// </summary>
    static void StartCrypto(int key, string readfile, bool recovery)
    {
        if (string.IsNullOrEmpty(readfile) == false)
        {
            CryptoStart(key, readfile, recovery);
        }
    }
    static void CryptoStart(int key, string readfile, bool recovery)
    {
        if (File.Exists(readfile) == false)
        {
            return;
        }
        //
        var size = 512;
        var buf1 = new byte[size];
        using (var fsmr = new FileStream(readfile, FileMode.Open))
            fsmr.Read(buf1, 0, size);
        //
        var mpeg = true;
        var ftyp = new char[4] { 'f', 't', 'y', 'p' };
        for (var j = 0; j < ftyp.Length; j++)
        {
            if (ftyp[j] != buf1[j + 4])
            {
                mpeg = false;
                break;
            }
        }
        //解密
        if (recovery == true && mpeg == false)
        {
            var buf2 = HaskByte(buf1, -key);
            using (var fsmw = new FileStream(readfile, FileMode.Open, FileAccess.Write, FileShare.Read))
            {
                fsmw.Write(buf2, 0, size);
            }
            return;
        }
        //加密
        if (recovery == false && mpeg == true)
        {
            var buf2 = HaskByte(buf1, key);
            using (var fsmw = new FileStream(readfile, FileMode.Open, FileAccess.Write, FileShare.Read))
            {
                fsmw.Write(buf2, 0, size);
            }
            return;
        }
    }
    static int HashKey(string key)
    {
        var tmp = 0;
        var md5 = new MD5CryptoServiceProvider();
        var bit = md5.ComputeHash(Encoding.UTF8.GetBytes(key));
        for (var i = 0; i < bit.Length; i++)
        {
            tmp += bit[i];
        }
        return tmp;
    }
    static byte[] HaskByte(byte[] bytes, int key)
    {
        var i = 0;
        var c = bytes.Length;
        var temp = new byte[4];
        var data = new byte[c];
        for (i = 3; i < c; i += 4)
        {
            temp[0] = bytes[i - 3];
            temp[1] = bytes[i - 2];
            temp[2] = bytes[i - 1];
            temp[3] = bytes[i];
            var ints = byteToInt(temp);
            var bits = intToByte(ints + key);
            data[i - 3] = bits[0];
            data[i - 2] = bits[1];
            data[i - 1] = bits[2];
            data[i] = bits[3];
        }
        for (var j = i; j < c; j++)
        {
            data[j] = bytes[j];
        }
        return data;
    }
    static byte[] intToByte(int i)
    {
        byte[] result = new byte[4];
        result[0] = (byte)((i >> 24) & 0xFF);
        result[1] = (byte)((i >> 16) & 0xFF);
        result[2] = (byte)((i >> 8) & 0xFF);
        result[3] = (byte)(i & 0xFF);
        return result;
    }
    static int byteToInt(byte[] bytes)
    {
        int value = 0;
        for (int i = 0; i < 4; i++)
        {
            int off = (3 - i) * 8;
            value += (bytes[i] & 0x000000FF) << off;
        }
        return value;
    }
}