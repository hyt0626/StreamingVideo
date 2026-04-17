using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Text;
using System.Security.Cryptography;
using UnityEngine;
using RenderHeads.Media.AVProVideo;
using System.Buffers;

public class AndroidVideoServer : MonoBehaviour
{
    [Header("AVPro 播放器引用")]
    public MediaPlayer mediaPlayer;

    private HttpListener _listener;
    private Thread _serverThread;
    private string _filePath;
    private string keyStr = "happyMaster";
    private int _key;
    private int _port;
    private bool _isRunning;

    // 缓存解密后的 512 字节文件头
    private byte[] _decryptedHeader = new byte[512];
    private bool _hasDecryptedHeader = false;
    byte[] buffer = ArrayPool<byte>.Shared.Rent(1024 * 1024);

    void Start()
    {
        _filePath = "sdcard/video.mp4";
        StartProxyAndPlay(_filePath, keyStr);
    }


    /// <summary>
    /// 启动本地代理并播放
    /// </summary>
    /// <param name="filePath">本地加密视频的绝对路径</param>
    /// <param name="keyStr">你的字符串密码</param>
    public void StartProxyAndPlay(string filePath, string keyStr)
    {
        _filePath = filePath;

        // 根据算法计算 Key
        _key = HashKey(keyStr);

        // 预先读取并解密前 512 字节缓存到内存中，避免 Range 请求时的 4 字节对齐问题
        CacheDecryptedHeader();

        // 分配随机端口避免冲突
        _port = UnityEngine.Random.Range(20000, 40000);

        // 启动 HTTP 服务
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://127.0.0.1:{_port}/");
        _listener.Start();
        _isRunning = true;

        _serverThread = new Thread(HandleRequests);
        _serverThread.IsBackground = true;
        _serverThread.Start();

        Debug.Log($"[VideoServer] 代理启动成功: http://127.0.0.1:{_port}/");

        // 让 AVPro 播放代理地址
        if (mediaPlayer != null)
        {
            string playUrl = $"http://127.0.0.1:{_port}/video.mp4";
            mediaPlayer.OpenMedia(new MediaPath(playUrl, MediaPathType.AbsolutePathOrURL), autoPlay: true);
        }
    }

    private void CacheDecryptedHeader()
    {
        if (!File.Exists(_filePath)) return;

        try
        {
            using (var fs = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                byte[] encryptedHeader = new byte[512];
                int readCount = fs.Read(encryptedHeader, 0, 512);

                if (readCount == 512)
                {
                    _decryptedHeader = HaskByte(encryptedHeader, -_key);
                    _hasDecryptedHeader = true;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[VideoServer] 缓存头部解密失败: {ex.Message}");
        }
    }

    private void HandleRequests()
    {
        while (_isRunning && _listener.IsListening)
        {
            try
            {
                var context = _listener.GetContext();
                ThreadPool.QueueUserWorkItem(c => ProcessRequest(context));
            }
            catch { /* 监听停止时忽略异常 */ }
        }
    }

    private void ProcessRequest(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

        try
        {
            using (FileStream fs = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                long fileLength = fs.Length;
                long startByte = 0;
                long endByte = fileLength - 1;

                // 处理 ExoPlayer 的 Range 进度拖拽请求
                string rangeHeader = request.Headers["Range"];
                if (!string.IsNullOrEmpty(rangeHeader))
                {
                    string[] range = rangeHeader.Replace("bytes=", "").Split('-');
                    startByte = long.Parse(range[0]);
                    if (range.Length > 1 && !string.IsNullOrEmpty(range[1]))
                    {
                        endByte = long.Parse(range[1]);
                    }
                    response.StatusCode = 206;
                }
                else
                {
                    response.StatusCode = 200;
                }

                long contentLength = endByte - startByte + 1;
                response.ContentLength64 = contentLength;
                response.AddHeader("Content-Range", $"bytes {startByte}-{endByte}/{fileLength}");
                response.AddHeader("Accept-Ranges", "bytes");
                response.ContentType = "video/mp4";

                fs.Seek(startByte, SeekOrigin.Begin);

                long totalSent = 0;

                while (totalSent < contentLength)
                {
                    int toRead = (int)Math.Min(buffer.Length, contentLength - totalSent);
                    int readCount = fs.Read(buffer, 0, toRead);

                    if (readCount <= 0) break;

                    long currentFilePos = startByte + totalSent;

                    // 如果当前读取的数据区间，包含了文件的前 512 字节
                    if (_hasDecryptedHeader && currentFilePos < 512)
                    {
                        int overlapStart = (int)currentFilePos;
                        int overlapLength = (int)Math.Min(readCount, 512 - currentFilePos);

                        // 直接用缓存的“明文”覆盖掉刚从磁盘读出来的“密文”
                        Array.Copy(_decryptedHeader, overlapStart, buffer, 0, overlapLength);
                    }

                    // 写入 HTTP 响应流
                    response.OutputStream.Write(buffer, 0, readCount);
                    totalSent += readCount;
                }
            }
        }
        catch (Exception e)
        {
            // 客户端主动断开连接(Seek 拖拽进度条时会抛弃旧连接是正常现象)
            Debug.Log("Stream disconnected: " + e.Message);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            response.Close();
        }
    }

    private void OnDestroy()
    {
        _isRunning = false;
        if (_listener != null)
        {
            _listener.Stop();
            _listener.Close();
        }
        if (_serverThread != null)
        {
            _serverThread.Abort();
        }
    }

    #region 加解密算法

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

    #endregion
}