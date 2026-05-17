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

    [Header("輸入區")]
    public TMP_InputField inputField;
    public Button         sendButton;
    public Button         micButton;

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
        sendButton.onClick.AddListener(OnSendButtonClicked);
        micButton.onClick.AddListener(OnMicButtonClicked);
        inputField.onSubmit.AddListener((_) => OnSendButtonClicked());
        historyButton.onClick.AddListener(ToggleHistory);

        historyPanel.SetActive(false);
    }

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
        string text = inputField.text.Trim();
        if (string.IsNullOrEmpty(text)) return;
        AddUserMessage(text);
        _streamerClient.SendText(text);
        inputField.text = "";
        inputField.ActivateInputField();
    }

    // ─────────────────────────────────────────
    // 麥克風按鈕（路由到 PlatformManager）
    // ─────────────────────────────────────────
    void OnMicButtonClicked()
    {
        PlatformManager.Instance?.RequestMicrophonePermission(() => {
            _streamerClient.StartMicInput();
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

        // 字幕區顯示 AI 說的話（永久保留，不自動清除）
        if (aiSubtitleText != null)
            aiSubtitleText.text = text;

        if (_historyVisible) StartCoroutine(ScrollToBottom());
    }

    /// <summary>
    /// 顯示系統狀態提示（辨識中、錄音中等）。
    /// 顯示在字幕區，systemMessageDuration 秒後自動清除。
    /// </summary>
    public void ShowSystemMessage(string text)
    {
        if (aiSubtitleText == null) return;
        aiSubtitleText.text = text;

        if (_clearSubtitleCoroutine != null)
            StopCoroutine(_clearSubtitleCoroutine);
        _clearSubtitleCoroutine = StartCoroutine(ClearSubtitleAfter(systemMessageDuration));
    }

    IEnumerator ClearSubtitleAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        // 只有當字幕還是系統訊息時才清除（避免清掉 AI 正常回應）
        if (aiSubtitleText != null)
            aiSubtitleText.text = "";
        _clearSubtitleCoroutine = null;
    }

    // ─────────────────────────────────────────
    IEnumerator ScrollToBottom()
    {
        yield return new WaitForEndOfFrame();
        Canvas.ForceUpdateCanvases();
        chatScrollRect.verticalNormalizedPosition = 0f;
    }
}
