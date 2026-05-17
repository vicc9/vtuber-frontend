using UnityEngine;

/// <summary>
/// 三平台（iOS / Android / WebGL）路由管理。
/// 統一麥克風權限入口，並在 Start 時通知 StreamerClient 平台資訊。
/// </summary>
public class PlatformManager : MonoBehaviour
{
    public static PlatformManager Instance;

    // AudioRecorder 由 Inspector 拖入，或 Awake 自動偵測
    private AudioRecorder _audioRecorder;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        _audioRecorder = FindAnyObjectByType<AudioRecorder>();
        DetectPlatform();
    }

    // ─────────────────────────────────────────
    void DetectPlatform()
    {
#if UNITY_IOS && !UNITY_EDITOR
        Debug.Log("🍎 平台：iOS");
        SetIOSSettings();
#elif UNITY_ANDROID && !UNITY_EDITOR
        Debug.Log("📱 平台：Android");
        SetAndroidSettings();
#elif UNITY_WEBGL && !UNITY_EDITOR
        Debug.Log("🌐 平台：WebGL");
        SetWebGLSettings();
#else
        Debug.Log("💻 平台：PC / Editor");
        SetPCSettings();
#endif
    }

    // ─────────────────────────────────────────
    // 各平台設定
    // ─────────────────────────────────────────
    void SetIOSSettings()
    {
        Application.targetFrameRate = 60;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        // 音訊 Session 由 StreamerClient 的 ConfigureAudioSession() 處理
    }

    void SetAndroidSettings()
    {
        Application.targetFrameRate = 60;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
    }

    void SetWebGLSettings()
    {
        Application.targetFrameRate = 60;
        // WebGL 不支援 Unity Microphone API
        // AudioRecorder.cs 在 WebGL Build 時會自動停用（見其 Start() 邏輯）
    }

    void SetPCSettings()
    {
        Application.targetFrameRate = 60;
    }

    // ─────────────────────────────────────────
    // 統一麥克風權限入口（由 UIManager 呼叫）
    // ─────────────────────────────────────────
    public void RequestMicrophonePermission(System.Action onGranted)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(
                UnityEngine.Android.Permission.Microphone))
        {
            UnityEngine.Android.Permission.RequestUserPermission(
                UnityEngine.Android.Permission.Microphone);
            StartCoroutine(WaitForAndroidPermission(onGranted));
        }
        else
        {
            onGranted?.Invoke();
        }
#elif UNITY_IOS && !UNITY_EDITOR
        // iOS 由 Application.RequestUserAuthorization 處理（AudioRecorder 內已呼叫）
        onGranted?.Invoke();
#elif UNITY_WEBGL && !UNITY_EDITOR
        // WebGL 不支援麥克風，直接略過
        Debug.LogWarning("🌐 WebGL 不支援麥克風語音輸入");
#else
        // PC / Editor：直接允許
        onGranted?.Invoke();
#endif
    }

    // ─────────────────────────────────────────
    // StreamerClient.StartMicInput() 呼叫此方法
    // 路由到 AudioRecorder（iOS / Android）
    // ─────────────────────────────────────────
    public void StartMicInput()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        Debug.LogWarning("🌐 WebGL 不支援語音輸入");
        return;
#endif
        if (_audioRecorder == null)
            _audioRecorder = FindAnyObjectByType<AudioRecorder>();

        if (_audioRecorder != null)
            _audioRecorder.OnMicButtonClickPublic();
        else
            Debug.LogWarning("⚠ 找不到 AudioRecorder");
    }

    // ─────────────────────────────────────────
#if UNITY_ANDROID && !UNITY_EDITOR
    System.Collections.IEnumerator WaitForAndroidPermission(System.Action onGranted)
    {
        yield return new WaitForSeconds(0.5f);
        if (UnityEngine.Android.Permission.HasUserAuthorizedPermission(
                UnityEngine.Android.Permission.Microphone))
            onGranted?.Invoke();
        else
            Debug.LogWarning("⚠ 麥克風權限被拒絕");
    }
#endif
}
