using UnityEngine;
using UnityEngine.Networking;
using NativeWebSocket;
using Newtonsoft.Json.Linq;
using System.Collections;
using System.Threading.Tasks;
using Live2D.Cubism.Core;

public class StreamerClient : MonoBehaviour
{
    // 🌟 新增：全域靜態實例，用來確保不論前端呼叫到哪一個物件，都能找到真正連線的那個實例
    public static StreamerClient Instance { get; private set; }

    WebSocket websocket;

    // 關鍵修正：透過本地布林值避開 WebGL 的 State 同步 Bug
    private bool _isConnected = false;
    private string _lastToken = "";  // 重連時帶回 token

    // 提供給 UIManager 或 UIManagerBridge 讀取的公開屬性
    public bool IsConnected => _isConnected;

    [Header("音訊")]
    public AudioSource audioSource;

    [Header("Live2D 嘴巴參數（LipSync）")]
    public CubismModel cubismModel;
    private CubismParameter _mouthOpenY;

    [Header("動作與表情控制")]
    public MotionController motionController;

    [Header("待機狀態管理")]
    public IdleStateManager idleStateManager;

    private UIManager _uiManager;

    void Awake()
    {
        // 🌟 新增：單例初始化防禦機制
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[WS] 偵測到重複或多餘的 StreamerClient 在物件【{gameObject.name}】上。");
        }
        else
        {
            Instance = this;
        }

        _uiManager = FindAnyObjectByType<UIManager>();

        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

        if (cubismModel == null) cubismModel = FindAnyObjectByType<CubismModel>();
        if (cubismModel != null)
        {
            _mouthOpenY = cubismModel.Parameters.FindById("ParamMouthOpenY");
            if (_mouthOpenY == null)
                Debug.LogError("找不到參數 ParamMouthOpenY");
        }

        if (motionController == null)
            motionController = FindAnyObjectByType<MotionController>();

        if (idleStateManager == null)
            idleStateManager = FindAnyObjectByType<IdleStateManager>();
    }

    async void Start()
    {
#if UNITY_IOS && !UNITY_EDITOR
        ConfigureAudioSession();
#endif
        if (FindAnyObjectByType<AuthManager>() == null)
            await ConnectAsync("");
    }

    void Update()
    {
        // NativeWebSocket 必須在 Update 中持續調度訊息佇列（WebGL 平台以外需要）
        if (websocket != null)
        {
            websocket.DispatchMessageQueue();
        }
    }

    public async void ConnectWithToken(string token)
    {
        await ConnectAsync(token);
    }

    private async Task ConnectAsync(string token)
    {
        _lastToken = token;  // 保存供重連使用
        string host = "localhost:8000";
		string scheme = "ws"; // 新增 scheme 變數
#if UNITY_WEBGL && !UNITY_EDITOR
        string pageUrl  = Application.absoluteURL;
        string withPort = pageUrl.Replace("https://", "")
                                 .Replace("http://", "")
                                 .Split('/')[0];
        host = withPort;
		scheme = pageUrl.StartsWith("https://") ? "wss" : "ws";
#endif
        string wsUrl = string.IsNullOrEmpty(token)
            ? $"ws://{host}/ws"
            : $"ws://{host}/ws?token={token}";

        Debug.Log($"[WS] 連線至: {wsUrl}");

        websocket = new WebSocket(wsUrl);

        // 在事件中同步更新 _isConnected 狀態
        websocket.OnOpen += () => {
            Debug.Log($"已成功連接至 AI 主播後端！(物件: {gameObject.name})");
            _isConnected = true;
            Instance = this; // 確保連線成功的實例被指定為主要實例
        };

        websocket.OnError += (e) => {
            Debug.Log("連線錯誤: " + e);
            _isConnected = false;
        };

        websocket.OnClose += async (c) => {
            Debug.Log($"連線已中斷，代碼: {c}");
            _isConnected = false;

            // 非正常關閉（Undefined / 非 1000）就嘗試重連
            if (c != WebSocketCloseCode.Normal)
            {
                Debug.Log("偵測到非正常斷線，3 秒後嘗試重連...");
                await Task.Delay(3000);
                await ConnectAsync(_lastToken);
            }
        };

        websocket.OnMessage += (bytes) =>
        {
            var message = System.Text.Encoding.UTF8.GetString(bytes);
            Debug.Log("收到後端包裹: " + message);
            HandleTextMessage(message);
        };

        await websocket.Connect();
    }

    // 🌟 關鍵新增：專門餵給網頁端 JS 的 SendMessage 橋接方法（必須為 void 且接收單一 string）
    // 請確認你網頁前端 index.html 的 SendMessage("物件名稱", "SubmitTextFromHTML", 文字); 改成叫這個名字
    public void SubmitTextFromHTML(string message)
    {
        Debug.Log($"[HTML-Bridge] 收到網頁 JS 轉來的文字: {message}");

        // 檢查如果自己沒連線成功，但別的實例成功了，就直接幫忙轉發出去
        if (!_isConnected && Instance != null && Instance != this && Instance.IsConnected)
        {
            Debug.Log($"🔀 [自動重導向] 當前組件未連線，已自動將網頁文字轉發給真正連線的實例。");
            _ = Instance.SendText(message);
        }
        else
        {
            // 如果自己就是連線主體，直接叫用原本的 SendText
            _ = SendText(message);
        }
    }

    /// <summary>
    /// 發送文字訊息給後端
    /// </summary>
    public async Task SendText(string message)
    {
        // 🌟 額外保險：如果從 C# 內部其他地方呼叫此方法，也加上自動路由導向
        if (!_isConnected && Instance != null && Instance != this && Instance.IsConnected)
        {
            await Instance.SendText(message);
            return;
        }

        if (!_isConnected || websocket == null)
        {
            Debug.LogWarning($"⚠️ WebSocket 未連線，無法發送文字 (當前物件: {gameObject.name})");
            _uiManager?.ShowSystemMessage("錯誤：目前尚未與伺服器連線");
            return;
        }

        try
        {
            await websocket.SendText(message);
            Debug.Log($"[WS] 已發送文字: {message}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[WS] 文字發送失敗: {e.Message}");
        }
    }

    /// <summary>
    /// 發送麥克風錄音的二進位資料給後端（修復 AudioRecorder 報錯）
    /// </summary>
    public async Task SendAudioBytes(byte[] audioData)
    {
        if (!_isConnected && Instance != null && Instance != this && Instance.IsConnected)
        {
            await Instance.SendAudioBytes(audioData);
            return;
        }

        if (!_isConnected || websocket == null)
        {
            Debug.LogWarning("⚠️ WebSocket 未連線，無法發送語音資料");
            return;
        }

        try
        {
            await websocket.Send(audioData);
            Debug.Log($"[WS] 已發送語音資料，大小: {audioData.Length} bytes");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[WS] 語音資料發送失敗: {e.Message}");
        }
    }

    void HandleTextMessage(string raw)
    {
        var json = JObject.Parse(raw);
        string type = json["type"]?.ToString();

        switch (type)
        {
            case "streamer_update":
            {
                string dialogue = json["dialogue"]?.ToString();
                string emotion  = json["emotion"]?.ToString() ?? "neutral";
                string action   = json["action"]?.ToString()  ?? "";
                string audioUrl = json["audio_url"]?.ToString();
                bool   isIdle   = json["is_idle"]?.ToObject<bool>() ?? false;
                string stage    = json["stage"]?.ToString() ?? "";

                _uiManager?.AddAIMessage(dialogue);
                motionController?.PlayExpression(emotion);

                if (isIdle && idleStateManager != null)
                    idleStateManager.OnIdleResponse(stage, emotion, action);

                if (!string.IsNullOrEmpty(audioUrl))
                    StartCoroutine(DownloadAndPlay(audioUrl, action));

                break;
            }
            case "stt_result":
            {
                string text = json["text"]?.ToString();
                if (!string.IsNullOrEmpty(text))
                    _uiManager?.AddUserMessage($"[語音] {text}");
                break;
            }
            case "stt_status":
            {
                string status = json["status"]?.ToString();
                if (status == "recognizing")
                    _uiManager?.ShowSystemMessage("辨識中...");
                else if (status == "failed")
                    _uiManager?.ShowSystemMessage(
                        json["message"]?.ToString() ?? "語音辨識失敗");
                break;
            }
            case "auth_error":
            {
                Debug.LogError("後端拒絕連線：Token 無效");
                _uiManager?.ShowSystemMessage("連線驗證失敗，請重新啟動");
                break;
            }
        }
    }

    IEnumerator DownloadAndPlay(string audioUrl, string action)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        using (UnityWebRequest www = UnityWebRequest.Get(audioUrl))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                byte[] wavBytes = www.downloadHandler.data;
                AudioClip clip  = WavToAudioClip(wavBytes);

                if (clip != null)
                {
                    audioSource.clip = clip;
                    audioSource.Play();
                    Debug.Log($"播放音訊（WebGL），長度: {clip.length}s");

                    if (!string.IsNullOrEmpty(action) && action != "無動作")
                        motionController?.PlayMotion(action);
                }
                else
                {
                    Debug.LogWarning("WAV 解析失敗");
                }
            }
            else
            {
                Debug.LogError("音訊下載失敗: " + www.error);
            }
        }
#else
        using (UnityWebRequest www =
               UnityWebRequestMultimedia.GetAudioClip(audioUrl, AudioType.WAV))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                AudioClip clip = DownloadHandlerAudioClip.GetContent(www);

                float waitTime = 0f;
                while (clip.loadState == AudioDataLoadState.Loading && waitTime < 5f)
                {
                    yield return new WaitForSeconds(0.05f);
                    waitTime += 0.05f;
                }

                if (clip.loadState == AudioDataLoadState.Loaded)
                {
                    audioSource.clip = clip;
                    audioSource.Play();
                    Debug.Log($"播放音訊，長度: {clip.length}s");

                    if (!string.IsNullOrEmpty(action) && action != "無動作")
                        motionController?.PlayMotion(action);
                }
            }
            else
            {
                Debug.LogError("音訊下載失敗: " + www.error);
            }
        }
#endif
    }

    private AudioClip WavToAudioClip(byte[] wavBytes)
    {
        if (wavBytes == null || wavBytes.Length < 44) return null;

        int channels = System.BitConverter.ToInt16(wavBytes, 22);
        int sampleRate = System.BitConverter.ToInt32(wavBytes, 24);
        int pos = 12;

        while (pos < wavBytes.Length - 8)
        {
            if (wavBytes[pos] == 'd' && wavBytes[pos + 1] == 'a' && wavBytes[pos + 2] == 't' && wavBytes[pos + 3] == 'a')
            {
                pos += 4;
                int dataSize = System.BitConverter.ToInt32(wavBytes, pos);
                pos += 4;

                int sampleCount = dataSize / 2;
                float[] samples = new float[sampleCount];

                for (int i = 0; i < sampleCount; i++)
                {
                    if (pos + i * 2 + 1 >= wavBytes.Length) break;
                    short shot = System.BitConverter.ToInt16(wavBytes, pos + i * 2);
                    samples[i] = shot / 32768f;
                }

                AudioClip clip = AudioClip.Create("StreamingAudio", sampleCount / channels, channels, sampleRate, false);
                clip.SetData(samples, 0);
                return clip;
            }
            pos++;
        }
        return null;
    }

    private void OnDestroy()
    {
        if (websocket != null)
        {
            websocket.Close();
        }
    }

#if UNITY_IOS && !UNITY_EDITOR
    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern void ConfigureAudioSession();
#endif

    public void StartMicInput()
    {
        Debug.Log("開始麥克風收音...");
    }
}