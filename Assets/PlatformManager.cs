using UnityEngine;

public class PlatformManager : MonoBehaviour
{
    public static PlatformManager Instance;

    void Awake() {
        if (Instance == null) {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        } else {
            Destroy(gameObject);
        }

        DetectPlatform();
    }

    void DetectPlatform() {
        #if UNITY_ANDROID
            Debug.Log("📱 平台：Android");
            SetAndroidSettings();
        #elif UNITY_IOS
            Debug.Log("🍎 平台：iOS");
            SetIOSSettings();
        #elif UNITY_WEBGL
            Debug.Log("🌐 平台：WebGL");
            SetWebGLSettings();
        #else
            Debug.Log("💻 平台：PC/Editor");
        #endif
    }

    void SetAndroidSettings() {
        Application.targetFrameRate = 60;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
    }

    void SetIOSSettings() {
        Application.targetFrameRate = 60;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
    }

    void SetWebGLSettings() {
        Application.targetFrameRate = 60;
        // WebGL 不需要 Sleep Timeout 設定
    }

    // ✅ 統一麥克風權限請求入口
    public void RequestMicrophonePermission(System.Action onGranted) {
        #if UNITY_ANDROID
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(
                  UnityEngine.Android.Permission.Microphone)) {
                UnityEngine.Android.Permission.RequestUserPermission(
                  UnityEngine.Android.Permission.Microphone);
                StartCoroutine(WaitForAndroidPermission(onGranted));
            } else {
                onGranted?.Invoke();
            }
        #elif UNITY_IOS
            // iOS 系統自動處理，直接執行
            onGranted?.Invoke();
        #elif UNITY_WEBGL
            // 呼叫 JS 橋接
            RequestMicrophoneWebGL();
            onGranted?.Invoke();
        #else
            onGranted?.Invoke();
        #endif
    }

    #if UNITY_ANDROID
    System.Collections.IEnumerator WaitForAndroidPermission(System.Action onGranted) {
        yield return new WaitForSeconds(0.5f);
        if (UnityEngine.Android.Permission.HasUserAuthorizedPermission(
              UnityEngine.Android.Permission.Microphone)) {
            onGranted?.Invoke();
        } else {
            Debug.LogWarning("⚠️ 麥克風權限被拒絕");
        }
    }
    #endif

    #if UNITY_WEBGL && !UNITY_EDITOR
    [System.Runtime.InteropServices.DllImport("__Internal")]
    static extern void StartMicrophoneJS();

    void RequestMicrophoneWebGL() {
        StartMicrophoneJS();
    }
    #else
    void RequestMicrophoneWebGL() {
        Debug.Log("🌐 WebGL 麥克風（Editor 模擬）");
    }
    #endif
}