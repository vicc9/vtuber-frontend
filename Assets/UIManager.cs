using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class UIManager : MonoBehaviour
{
    [Header("聊天歷程")]
    public GameObject historyPanel;
    public ScrollRect chatScrollRect;
    public Transform  chatContent;
    public GameObject userMessagePrefab;
    public GameObject aiMessagePrefab;
    public Button     historyButton;


    [Header("輸入區（WebGL 由 HTML 層接管）")]
    public RectTransform  inputFieldRect;
    public TMP_InputField inputField;
    public Button         sendButton;
    public Button         micButton;
    public TMP_Text       sendButtonText;
    public TMP_Text       micButtonText;

    [Header("字幕")]
    public TMP_Text aiSubtitleText;

    [Header("系統訊息顯示時間（秒）")]
    public float systemMessageDuration = 3f;

    private StreamerClient _streamerClient;
    private bool           _historyVisible = false;
    private Coroutine      _clearSubtitleCoroutine;

    void Awake()
    {

    }

    void Start()
    {
        _streamerClient = FindAnyObjectByType<StreamerClient>();

        if (sendButtonText != null) sendButtonText.text = "送出";
        if (micButtonText  != null) micButtonText.text  = "語音";

        historyButton.onClick.AddListener(ToggleHistory);
        historyPanel.SetActive(false);

        if (chatScrollRect != null)
        {
            chatScrollRect.vertical          = true;
            chatScrollRect.horizontal        = false;
            chatScrollRect.scrollSensitivity = 20f;
            chatScrollRect.movementType      = ScrollRect.MovementType.Clamped;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        SetupWebGL();
#else
        SetupNative();
#endif
    }

    // ─────────────────────────────────────────
    // JS → Unity 橋接：HTML 輸入框送出時呼叫
    // GameObject 名稱必須是 "UIManagerBridge"
    // ─────────────────────────────────────────
    public void OnHTMLSubmit(string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        AddUserMessage(text);
        
        // 🌟 修正：使用遺忘模式（Fire-and-Forget）安全呼叫 Task，避免異常在 WebGL 層被吞掉
        _ = _streamerClient?.SendText(text); 
    }

    // ─────────────────────────────────────────
    // WebGL
    // ─────────────────────────────────────────
#if UNITY_WEBGL && !UNITY_EDITOR

    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern void InitPersistentInput();

    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern void ClearNativeInput();

    void SetupWebGL()
    {
        if (inputField != null)  inputField.gameObject.SetActive(false);
        if (sendButton != null)  sendButton.gameObject.SetActive(false);
        if (micButton  != null)  micButton.gameObject.SetActive(false);

        // 👇👇👇 關鍵修正 2：確保 Unity 引擎不會霸佔鍵盤事件，讓 HTML 輸入框可以正常切換中英文 👇👇👇
        WebGLInput.captureAllKeyboardInput = false;
        // 👆👆👆 加上這行能完美配合 jslib 裡的 stopPropagation 👆👆👆

        InitPersistentInput();
    }

#else
    // ─────────────────────────────────────────
    // iOS / Android / PC
    // ─────────────────────────────────────────
    void SetupNative()
    {
        sendButton.onClick.AddListener(OnSendButtonClicked);
        micButton.onClick.AddListener(OnMicButtonClicked);
        inputField.onSubmit.AddListener((_) => OnSendButtonClicked());
    }

    void OnSendButtonClicked()
    {
        string text = inputField.text.Trim();
        if (string.IsNullOrEmpty(text)) return;

        AddUserMessage(text);
        
        // 🌟 安全調用 Task
        _ = _streamerClient?.SendText(text);

        inputField.text = "";
        inputField.ActivateInputField();
    }

    void OnMicButtonClicked()
    {
        PlatformManager.Instance?.RequestMicrophonePermission(() => {
            // 注意：確保你的 StreamerClient 內有實作 StartMicInput 函數
            // 如果是在原生平台執行，才呼叫此方法
            // _streamerClient?.StartMicInput();
        });
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            if (inputField != null && inputField.isFocused)
                OnSendButtonClicked();
    }
#endif

    // ─────────────────────────────────────────
    void ToggleHistory()
    {
        _historyVisible = !_historyVisible;
        historyPanel.SetActive(_historyVisible);
        if (_historyVisible)
            StartCoroutine(ScrollToBottom());
    }

    public void AddUserMessage(string text)
    {
        if (userMessagePrefab == null) return;
        var go  = Instantiate(userMessagePrefab, chatContent);
        var tmp = go.GetComponentInChildren<TMP_Text>();
        if (tmp != null)
        {
            tmp.text                    = text;
            tmp.enableWordWrapping       = true;
            tmp.overflowMode            = TextOverflowModes.Overflow;

        }
        if (_historyVisible) StartCoroutine(ScrollToBottom());
    }

    public void AddAIMessage(string text)
    {
        if (aiMessagePrefab == null) return;
        var go  = Instantiate(aiMessagePrefab, chatContent);
        var tmp = go.GetComponentInChildren<TMP_Text>();
        if (tmp != null)
        {
            tmp.text              = text;
            tmp.enableWordWrapping = true;
            tmp.overflowMode      = TextOverflowModes.Overflow;

        }

        if (aiSubtitleText != null)
            aiSubtitleText.text = text;

        if (_historyVisible) StartCoroutine(ScrollToBottom());
    }

    public void ShowSystemMessage(string text)
    {
        if (aiSubtitleText == null) return;
        aiSubtitleText.text = text;

        if (_clearSubtitleCoroutine != null)
            StopCoroutine(_clearSubtitleCoroutine);
        _clearSubtitleCoroutine =
            StartCoroutine(ClearSubtitleAfter(systemMessageDuration));
    }

    IEnumerator ClearSubtitleAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (aiSubtitleText != null)
            aiSubtitleText.text = "";
        _clearSubtitleCoroutine = null;
    }

    IEnumerator ScrollToBottom()
    {
        yield return new WaitForEndOfFrame();
        Canvas.ForceUpdateCanvases();
        if (chatScrollRect != null)
            chatScrollRect.verticalNormalizedPosition = 0f;
    }
}