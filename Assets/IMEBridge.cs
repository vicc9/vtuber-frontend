// Assets/Scripts/IMEBridge.cs
using UnityEngine;
using TMPro;

public class IMEBridge : MonoBehaviour
{
    public static IMEBridge Instance;

    public string CurrentText { get; private set; } = "";
    public System.Action<string> OnSubmit;

    void Awake()
    {
         // 確保這是唯一的 Instance
        if (Instance != null && Instance != this)
    {
        Destroy(gameObject);
        return;
    }
        Instance        = this;
        gameObject.name = "IMEBridge";
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        UnlockAudioContext();
    }

    // ── 由 JS 呼叫 ──────────────────────────────────────────
    public void OnNativeInputChanged(string text)
    {
        CurrentText = text;
    }

    public void OnNativeInputSubmit(string text)
    {
        CurrentText = text;
        OnSubmit?.Invoke(text);
        CurrentText = "";
    }

    // ── JS Interop ───────────────────────────────────────────
#if UNITY_WEBGL && !UNITY_EDITOR
    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern void ShowNativeInput(
        float x, float y, float width, float height, string defaultText);

    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern void HideNativeInput();

    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern void UnlockAudioContext();
#else
    private static void ShowNativeInput(
        float x, float y, float width, float height, string defaultText) { }
    private static void HideNativeInput() { }
    private static void UnlockAudioContext()
    {
        Debug.Log("[AudioUnlock] 非 WebGL 平台，略過");
    }
#endif

    public void ShowInput(RectTransform rect, string currentText = "")
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);

        float x      = corners[0].x;
        float y      = Screen.height - corners[2].y;
        float width  = corners[2].x - corners[0].x;
        float height = corners[2].y - corners[0].y;

        ShowNativeInput(x, y, width, height, currentText);
#endif
    }

    public void HideInput()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        HideNativeInput();
#endif
    }
}
