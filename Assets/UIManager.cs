using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
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

    [Header("輸入區")]
    public RectTransform  inputFieldRect;   // 拖入 InputField 的 RectTransform
    public TMP_InputField inputField;
    public Button         sendButton;
    public Button         micButton;
    public TMP_Text       sendButtonText;   // 拖入送出按鈕的 TMP_Text（可選）
    public TMP_Text       micButtonText;    // 拖入麥克風按鈕的 TMP_Text（可選）

    [Header("字幕")]
    public TMP_Text aiSubtitleText;

    [Header("系統訊息顯示時間（秒）")]
    public float systemMessageDuration = 3f;

    private StreamerClient _streamerClient;
    private bool           _historyVisible = false;
    private Coroutine      _clearSubtitleCoroutine;

    // ─────────────────────────────────────────
    void Start()
    {
        _streamerClient = FindAnyObjectByType<StreamerClient>();

        // ── 按鈕文字設定（避免 emoji 亂碼）────────────────────
        if (sendButtonText != null) sendButtonText.text = "送出";
        if (micButtonText  != null) micButtonText.text  = "語音";

        // ── 按鈕事件 ────────────────────────────────────────
        sendButton.onClick.AddListener(OnSendButtonClicked);
        micButton.onClick.AddListener(OnMicButtonClicked);
        historyButton.onClick.AddListener(ToggleHistory);

        // ── WebGL：點擊輸入框時顯示原生 input ───────────────
#if UNITY_WEBGL && !UNITY_EDITOR
        SetupWebGLInput();
#else
        // 其他平台：用 TMP_InputField 的 onSubmit
        inputField.onSubmit.AddListener((_) => OnSendButtonClicked());
#endif

        historyPanel.SetActive(false);

        // ── ScrollRect 設定：確保可以滾動 ───────────────────
        if (chatScrollRect != null)
        {
            chatScrollRect.vertical              = true;
            chatScrollRect.horizontal            = false;
            chatScrollRect.scrollSensitivity     = 20f;
            chatScrollRect.movementType          = ScrollRect.MovementType.Clamped;
            chatScrollRect.verticalScrollbarVisibility =
                ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        }
    }

    // ─────────────────────────────────────────
    // WebGL 輸入法設定
    // ─────────────────────────────────────────
#if UNITY_WEBGL && !UNITY_EDITOR
    void SetupWebGLInput()
    {
        // 點擊 Unity InputField 時改為顯示原生 HTML input
        var trigger = inputField.gameObject.GetComponent<EventTrigger>()
                   ?? inputField.gameObject.AddComponent<EventTrigger>();

        var entry = new EventTrigger.Entry
            { eventID = EventTriggerType.PointerClick };
        entry.callback.AddListener((_) => {
            if (IMEBridge.Instance != null && inputFieldRect != null)
            {
                IMEBridge.Instance.ShowInput(inputFieldRect, inputField.text);
                IMEBridge.Instance.OnSubmit = (text) => {
                    if (!string.IsNullOrEmpty(text))
                    {
                        inputField.text = "";
                        AddUserMessage(text);
                        _streamerClient?.SendText(text);
                    }
                };
            }
        });
        trigger.triggers.Add(entry);

        // WebGL 模式下隱藏原本的 TMP_InputField（顯示原生 input 取代）
        // 但保留 placeholder 提示文字
        var colors = inputField.colors;
        colors.normalColor = new Color(1, 1, 1, 0.9f);
        inputField.colors  = colors;
    }
#endif

    // ─────────────────────────────────────────
    // 歷程面板開關
    // ─────────────────────────────────────────
    void ToggleHistory()
    {
        _historyVisible = !_historyVisible;
        historyPanel.SetActive(_historyVisible);
        if (_historyVisible)
            StartCoroutine(ScrollToBottom());
    }

    // ─────────────────────────────────────────
    // 送出按鈕
    // ─────────────────────────────────────────
    void OnSendButtonClicked()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        // WebGL：從 IMEBridge 取得文字
        string text = IMEBridge.Instance != null
            ? IMEBridge.Instance.CurrentText.Trim()
            : inputField.text.Trim();
        IMEBridge.Instance?.HideInput();
#else
        string text = inputField.text.Trim();
#endif
        if (string.IsNullOrEmpty(text)) return;
        AddUserMessage(text);
        _streamerClient?.SendText(text);
        inputField.text = "";

#if !UNITY_WEBGL || UNITY_EDITOR
        inputField.ActivateInputField();
#endif
    }

    // ─────────────────────────────────────────
    // 麥克風按鈕
    // ─────────────────────────────────────────
    void OnMicButtonClicked()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        ShowSystemMessage("網頁版不支援語音輸入");
        return;
#endif
        PlatformManager.Instance?.RequestMicrophonePermission(() => {
            _streamerClient?.StartMicInput();
        });
    }

    // ─────────────────────────────────────────
    // 公開方法：新增訊息泡泡
    // ─────────────────────────────────────────
    public void AddUserMessage(string text)
    {
        if (userMessagePrefab == null) return;
        var go = Instantiate(userMessagePrefab, chatContent);
        go.GetComponentInChildren<TMP_Text>().text = text;
        if (_historyVisible) StartCoroutine(ScrollToBottom());
    }

    public void AddAIMessage(string text)
    {
        if (aiMessagePrefab == null) return;
        var go = Instantiate(aiMessagePrefab, chatContent);
        go.GetComponentInChildren<TMP_Text>().text = text;

        if (aiSubtitleText != null)
            aiSubtitleText.text = text;

        if (_historyVisible) StartCoroutine(ScrollToBottom());
    }

    // ─────────────────────────────────────────
    // 系統訊息（辨識中、錄音中等）
    // ─────────────────────────────────────────
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

    // ─────────────────────────────────────────
    // ScrollRect 捲到底部
    // ─────────────────────────────────────────
    IEnumerator ScrollToBottom()
    {
        yield return new WaitForEndOfFrame();
        Canvas.ForceUpdateCanvases();
        if (chatScrollRect != null)
            chatScrollRect.verticalNormalizedPosition = 0f;
    }

    // ─────────────────────────────────────────
    // Update：鍵盤 Enter 送出（PC / Editor）
    // ─────────────────────────────────────────
    void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            if (inputField.isFocused)
                OnSendButtonClicked();
        }
#endif
    }
}
