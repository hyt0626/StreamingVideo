using UnityEngine;
using UnityEngine.SceneManagement;

public class ExternalStoragePermission : MonoBehaviour
{
    bool isLoadScene;
    void Start()
    {
        GetPermission();
    }
    private void OnApplicationFocus(bool focus)
    {
        if (focus)
        {
            GetPermission();
        }
    }

    void GetPermission()
    {
        // 检查是否已经拥有权限
        if (!HasExternalStoragePermission())
        {
            RequestExternalStoragePermission();
        }
        else
        {
            if (!isLoadScene)
            {
                isLoadScene = true;
                SceneManager.LoadSceneAsync(1);
            }
        }
    }

    // 检查是否拥有 MANAGE_EXTERNAL_STORAGE 权限
    private bool HasExternalStoragePermission()
    {
#if (!UNITY_EDITOR && UNITY_ANDROID)
        using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
        {
            if (version.GetStatic<int>("SDK_INT") >= 30) // Android 11 (API 30) 及以上
            {
                using (var environment = new AndroidJavaClass("android.os.Environment"))
                {
                    return environment.CallStatic<bool>("isExternalStorageManager");
                }
            }
        }
#endif
        return true; // 低于 Android 11 的设备默认返回 true
    }

    // 请求 MANAGE_EXTERNAL_STORAGE 权限
    private void RequestExternalStoragePermission()
    {
#if (!UNITY_EDITOR && UNITY_ANDROID)
        using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
        {
            if (version.GetStatic<int>("SDK_INT") >= 30) // Android 11 (API 30) 及以上
            {
                // 跳转到系统设置页面
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                {
                    using (var currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                    {
                        using (var intent = new AndroidJavaObject("android.content.Intent"))
                        {
                            // 设置 Intent 的 Action
                            intent.Call<AndroidJavaObject>("setAction", "android.settings.MANAGE_APP_ALL_FILES_ACCESS_PERMISSION");

                            // 将包名字符串转换为 Uri 对象
                            using (var uriClass = new AndroidJavaClass("android.net.Uri"))
                            {
                                using (var uri = uriClass.CallStatic<AndroidJavaObject>("parse", "package:" + currentActivity.Call<string>("getPackageName")))
                                {
                                    // 调用 setData 方法
                                    intent.Call<AndroidJavaObject>("setData", uri);
                                }
                            }

                            // 启动系统设置页面
                            currentActivity.Call("startActivity", intent);
                        }
                    }
                }
            }
        }
#endif
    }
}
