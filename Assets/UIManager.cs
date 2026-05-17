using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class UIManager : MonoBehaviour
{
    [Header("聊天歷程")]
    public GameObject historyPanel;      // 拖入 HistoryPanel
    public ScrollRect chatScrollRect;
    public Transform chatContent;
    public GameObject userMessagePrefab;
    public GameObject aiMessagePrefab;
    public Button historyButton;         // 拖入 HistoryButton

    [Header("輸入區")]
    public TMP_InputField inputField;
    public Button sendButton;
    public Button micButton;

    [Header("字幕")]
    public TMP_Text aiSubtitleText;

    private StreamerClient _streamerClient;
    private bool _historyVisible = false;

    void Start()
    {
        _streamerClient = FindAnyObjectByType<StreamerClient>();
        sendButton.onClick.AddListener(OnSendButtonClicked);
        micButton.onClick.AddListener(OnMicButtonClicked);
        inputField.onSubmit.AddListener((_) => OnSendButtonClicked());
        historyButton.onClick.AddListener(ToggleHistory);

        // 預設隱藏歷程面板
        historyPanel.SetActive(false);
    }

    void ToggleHistory()
    {
        _historyVisible = !_historyVisible;
        historyPanel.SetActive(_historyVisible);

        // 打開時自動捲到底部
        if (_historyVisible)
            StartCoroutine(ScrollToBottom());
    }

    void OnSendButtonClicked()
    {
        string text = inputField.text.Trim();
        if (string.IsNullOrEmpty(text)) return;
        AddUserMessage(text);
        _streamerClient.SendText(text);
        inputField.text = "";
        inputField.ActivateInputField();
    }

    void OnMicButtonClicked()
    {
        PlatformManager.Instance?.RequestMicrophonePermission(() => {
            _streamerClient.StartMicInput();
        });
    }

    public void AddUserMessage(string text)
    {
        if (userMessagePrefab == null) return;
        var go = Instantiate(userMessagePrefab, chatContent);
        go.GetComponentInChildren<TMP_Text>().text = text;
        if (_historyVisible)
            StartCoroutine(ScrollToBottom());
    }

    public void AddAIMessage(string text)
    {
        if (aiMessagePrefab == null) return;
        var go = Instantiate(aiMessagePrefab, chatContent);
        go.GetComponentInChildren<TMP_Text>().text = text;

        // 字幕永遠更新
        if (aiSubtitleText != null)
            aiSubtitleText.text = text;

        if (_historyVisible)
            StartCoroutine(ScrollToBottom());
    }

    IEnumerator ScrollToBottom()
    {
        yield return new WaitForEndOfFrame();
        Canvas.ForceUpdateCanvases();
        chatScrollRect.verticalNormalizedPosition = 0f;
    }
}