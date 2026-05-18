using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 三平台（iOS / Android / WebGL）路由管理。
/// 步驟 16：新增觸控 / 滑鼠雙棲支援。
/// </summary>
public class PlatformManager : MonoBehaviour
{
    public static PlatformManager Instance;

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
        Debug.Log("平台：iOS");
        SetIOSSettings();
#elif UNITY_ANDROID && !UNITY_EDITOR
        Debug.Log("平台：Android");
        SetAndroidSettings();
#elif UNITY_WEBGL && !UNITY_EDITOR
        Debug.Log("平台：WebGL");
        SetWebGLSettings();
#else
        Debug.Log("平台：PC / Editor");
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
        // 啟用多點觸控
        Input.multiTouchEnabled = true;
    }

    void SetAndroidSettings()
    {
        Application.targetFrameRate = 60;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        Input.multiTouchEnabled = true;
    }

    void SetWebGLSettings()
    {
        Application.targetFrameRate = 60;
        // WebGL 用滑鼠模擬觸控，不需要額外設定
    }

    void SetPCSettings()
    {
        Application.targetFrameRate = 60;
    }

    // ─────────────────────────────────────────
    // 觸控 / 滑鼠雙棲：判斷輸入來源
    // ─────────────────────────────────────────

    /// <summary>
    /// 本幀是否有點擊或觸控輸入
    /// </summary>
    public static bool IsPressed()
    {
#if UNITY_IOS || UNITY_ANDROID
        return Input.touchCount > 0 &&
               Input.GetTouch(0).phase == TouchPhase.Began;
#else
        return Input.GetMouseButtonDown(0);
#endif
    }

    /// <summary>
    /// 取得目前點擊 / 觸控位置
    /// </summary>
    public static Vector2 GetInputPosition()
    {
#if UNITY_IOS || UNITY_ANDROID
        if (Input.touchCount > 0)
            return Input.GetTouch(0).position;
        return Vector2.zero;
#else
        return Input.mousePosition;
#endif
    }

    /// <summary>
    /// 是否點擊在 UI 上（避免穿透到 3D 場景）
    /// </summary>
    public static bool IsPointerOverUI()
    {
        return EventSystem.current != null &&
               EventSystem.current.IsPointerOverGameObject();
    }

    // ─────────────────────────────────────────
    // 麥克風權限
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
        onGranted?.Invoke();
#elif UNITY_WEBGL && !UNITY_EDITOR
        Debug.LogWarning("WebGL 不支援麥克風語音輸入");
#else
        onGranted?.Invoke();
#endif
    }

    public void StartMicInput()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        Debug.LogWarning("WebGL 不支援語音輸入");
        return;
#endif
        if (_audioRecorder == null)
            _audioRecorder = FindAnyObjectByType<AudioRecorder>();

        if (_audioRecorder != null)
            _audioRecorder.OnMicButtonClickPublic();
        else
            Debug.LogWarning("找不到 AudioRecorder");
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
            Debug.LogWarning("麥克風權限被拒絕");
    }
#endif
}
