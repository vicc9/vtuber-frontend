using UnityEngine;
using UnityEngine.Networking;
using NativeWebSocket;
using Newtonsoft.Json.Linq;
using System.Collections;
using System.Threading.Tasks;
using Live2D.Cubism.Core;

public class StreamerClient : MonoBehaviour
{
    WebSocket websocket;

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

    // ─────────────────────────────────────────
    void Awake()
    {
        _uiManager = FindAnyObjectByType<UIManager>();

        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

        if (cubismModel == null) cubismModel = FindAnyObjectByType<CubismModel>();
        if (cubismModel != null)
        {
            _mouthOpenY = cubismModel.Parameters.FindById("ParamMouthOpenY");
            if (_mouthOpenY == null)
                Debug.LogError("❌ 找不到參數 ParamMouthOpenY！");
        }

        if (motionController == null)
            motionController = FindAnyObjectByType<MotionController>();

        if (idleStateManager == null)
            idleStateManager = FindAnyObjectByType<IdleStateManager>();
    }

    // ─────────────────────────────────────────
    // Start：有 AuthManager 時等它呼叫 ConnectWithToken
    //        沒有時自動連線（向下相容）
    // ─────────────────────────────────────────
    async void Start()
    {
#if UNITY_IOS && !UNITY_EDITOR
        ConfigureAudioSession();
#endif
        if (FindAnyObjectByType<AuthManager>() == null)
        {
            // 沒有 AuthManager，直接連線（開發模式）
            await ConnectAsync("");
        }
        // 有 AuthManager 則等它取得 Token 後呼叫 ConnectWithToken()
    }

    // ─────────────────────────────────────────
    // 供 AuthManager 呼叫
    // ─────────────────────────────────────────
    public async void ConnectWithToken(string token)
    {
        await ConnectAsync(token);
    }

    // ─────────────────────────────────────────
    // 核心連線邏輯
    // ─────────────────────────────────────────
    private async Task ConnectAsync(string token)
    {
        // ── 動態取得 host ──────────────────────────────────
        string host = "localhost:8000";
#if UNITY_WEBGL && !UNITY_EDITOR
        string pageUrl  = Application.absoluteURL;
        string withPort = pageUrl.Replace("https://", "")
                                 .Replace("http://", "")
                                 .Split('/')[0];
        host = withPort;
#endif

        // ── WebSocket 網址（Token 帶在 Query String）────────
        string wsUrl = string.IsNullOrEmpty(token)
            ? $"ws://{host}/ws"
            : $"ws://{host}/ws?token={token}";

        Debug.Log($"[WS] 連線至: {wsUrl}");

        websocket = new WebSocket(wsUrl);
        websocket.OnOpen  += () => Debug.Log("✅ 已成功連接至 AI 主播後端！");
        websocket.OnError += (e) => Debug.Log("❌ 連線錯誤: " + e);
        websocket.OnClose += (c) => Debug.Log($"🔌 連線已中斷，代碼: {c}");

        websocket.OnMessage += (bytes) =>
        {
            var message = System.Text.Encoding.UTF8.GetString(bytes);
            Debug.Log("📦 收到後端包裹: " + message);
            HandleTextMessage(message);
        };

        await websocket.Connect();
    }

    // ─────────────────────────────────────────
    // 解析 JSON 訊息
    // ─────────────────────────────────────────
    void HandleTextMessage(string raw)
    {
        var json = JObject.Parse(raw);
        string type = json["type"]?.ToString();

        switch (type)
        {
            // ── 正常對話 / Idle 自動搭話 ──
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

            // ── STT 辨識結果 ──
            case "stt_result":
            {
                string text = json["text"]?.ToString();
                if (!string.IsNullOrEmpty(text))
                    _uiManager?.AddUserMessage($"[語音] {text}");
                break;
            }

            // ── STT 狀態通知 ──
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

            // ── Token 被後端拒絕 ──
            case "auth_error":
            {
                Debug.LogError("❌ 後端拒絕連線：Token 無效");
                _uiManager?.ShowSystemMessage("連線驗證失敗，請重新啟動");
                break;
            }
        }
    }

    // ─────────────────────────────────────────
    // 下載音訊並播放
    // ─────────────────────────────────────────
    IEnumerator DownloadAndPlay(string audioUrl, string action)
    {
        using (UnityWebRequest www =
               UnityWebRequestMultimedia.GetAudioClip(audioUrl, AudioType.WAV))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                if (clip != null && clip.loadState == AudioDataLoadState.Loaded)
                {
                    audioSource.clip = clip;
                    audioSource.Play();
                    Debug.Log($"▶ 播放音訊，長度: {clip.length}s");

                    if (!string.IsNullOrEmpty(action) && action != "無動作")
                        motionController?.PlayMotion(action);
                }
            }
            else
            {
                Debug.LogError("❌ 音訊下載失敗: " + www.error);
            }
        }
    }

    // ─────────────────────────────────────────
    // 傳送文字
    // ─────────────────────────────────────────
    public async void SendText(string text)
    {
        if (websocket != null && websocket.State == WebSocketState.Open)
            await websocket.SendText(text);
        else
            Debug.LogWarning("⚠ WebSocket 未連線，無法發送文字");
    }

    // ─────────────────────────────────────────
    // 傳送音訊 bytes（由 AudioRecorder 呼叫）
    // ─────────────────────────────────────────
    public async void SendAudioBytes(byte[] wavBytes)
    {
        if (websocket != null && websocket.State == WebSocketState.Open)
        {
            await websocket.Send(wavBytes);
            Debug.Log($"🎤 已傳送音訊，大小: {wavBytes.Length} bytes");
        }
        else
        {
            Debug.LogWarning("⚠ WebSocket 未連線，無法傳送音訊");
        }
    }

    // ─────────────────────────────────────────
    // StartMicInput（由 UIManager 呼叫）
    // ─────────────────────────────────────────
    public void StartMicInput()
    {
        PlatformManager.Instance?.StartMicInput();
    }

    // ─────────────────────────────────────────
    // LipSync
    // ─────────────────────────────────────────
    void LateUpdate()
    {
        if (_mouthOpenY == null) return;

        float volume = 0f;

        if (audioSource.isPlaying && audioSource.clip != null)
        {
            float[] samples      = new float[256];
            int     channels     = audioSource.clip.channels;
            int     sampleOffset = audioSource.timeSamples * channels;
            int     totalSamples = audioSource.clip.samples  * channels;

            if (sampleOffset + 256 <= totalSamples)
            {
                audioSource.clip.GetData(samples, audioSource.timeSamples);
                float sum = 0f;
                foreach (var s in samples) sum += s * s;
                volume = Mathf.Sqrt(sum / samples.Length);
                volume = Mathf.Clamp01(volume * 10f);
            }
        }

        _mouthOpenY.Value = volume;
    }

    // ─────────────────────────────────────────
    // Update：WebGL 以外需要手動 Dispatch
    // ─────────────────────────────────────────
    void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        websocket?.DispatchMessageQueue();
#endif
    }

    // ─────────────────────────────────────────
    // iOS AVAudioSession
    // ─────────────────────────────────────────
#if UNITY_IOS && !UNITY_EDITOR
    [System.Runtime.InteropServices.DllImport("__Internal")]
    static extern void ConfigureAudioSession();
#else
    static void ConfigureAudioSession()
    {
        Debug.Log("[AudioSession] 非 iOS 平台，略過");
    }
#endif

    // ─────────────────────────────────────────
    private async void OnApplicationQuit()
    {
        if (websocket != null)
            await websocket.Close();
    }
}