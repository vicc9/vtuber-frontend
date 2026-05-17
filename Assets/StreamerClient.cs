using UnityEngine;
using UnityEngine.Networking;
using NativeWebSocket;
using Newtonsoft.Json;
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

    async void Start()
    {
#if UNITY_IOS && !UNITY_EDITOR
        // iOS：設定音訊 Session，允許錄音與播放同時進行
        ConfigureAudioSession();
#endif

        websocket = new WebSocket("ws://localhost:8000/ws");
        websocket.OnOpen  += () => Debug.Log("✅ 已成功連接至 AI 主播後端！");
        websocket.OnError += (e) => Debug.Log("❌ 連線錯誤: " + e);
        websocket.OnClose += (c) => Debug.Log("🔌 連線已中斷。");

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
            // ── 正常對話 / Idle 自動搭話（共用同一 type）──
            case "streamer_update":
            {
                string dialogue = json["dialogue"]?.ToString();
                string emotion  = json["emotion"]?.ToString() ?? "neutral";
                string action   = json["action"]?.ToString()  ?? "";
                string audioUrl = json["audio_url"]?.ToString();
                bool   isIdle   = json["is_idle"]?.ToObject<bool>() ?? false;
                string stage    = json["stage"]?.ToString() ?? "";

                // 字幕 + 聊天室
                _uiManager?.AddAIMessage(dialogue);

                // 表情（立即切換，LateUpdate 平滑插值）
                motionController?.PlayExpression(emotion);

                // idle 特殊狀態處理
                if (isIdle && idleStateManager != null)
                    idleStateManager.OnIdleResponse(stage, emotion, action);

                // 下載音訊並播放（播放時觸發動作）
                if (!string.IsNullOrEmpty(audioUrl))
                    StartCoroutine(DownloadAndPlay(audioUrl, action));

                break;
            }

            // ── STT 辨識結果：顯示使用者說了什麼 ──
            case "stt_result":
            {
                string text = json["text"]?.ToString();
                if (!string.IsNullOrEmpty(text))
                    _uiManager?.AddUserMessage($"🎙 {text}");
                break;
            }

            // ── STT 狀態通知 ──
            case "stt_status":
            {
                string status = json["status"]?.ToString();
                if (status == "recognizing")
                    _uiManager?.ShowSystemMessage("🔄 辨識中…");
                else if (status == "failed")
                    _uiManager?.ShowSystemMessage(
                        json["message"]?.ToString() ?? "語音辨識失敗");
                break;
            }
        }
    }

    // ─────────────────────────────────────────
    // 下載音訊並播放（保留原有 HTTP 下載方式）
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

                    // 動作在語音開始時觸發
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
    // 傳送文字（由 UIManager / PlatformManager 呼叫）
    // ─────────────────────────────────────────
    public async void SendText(string text)
    {
        if (websocket != null && websocket.State == WebSocketState.Open)
            await websocket.SendText(text);
        else
            Debug.LogWarning("⚠ WebSocket 未連線，無法發送文字");
    }

    // ─────────────────────────────────────────
    // StartMicInput（由 UIManager 的麥克風按鈕呼叫，路由到 PlatformManager）
    // ─────────────────────────────────────────
    public void StartMicInput()
    {
        PlatformManager.Instance?.StartMicInput();
    }

    // ─────────────────────────────────────────
    // 傳送音訊 bytes（由 AudioRecorder 呼叫）
    // iOS / Android 皆使用此方法
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
    // LipSync（保留原有邏輯）
    // ─────────────────────────────────────────
    void LateUpdate()
    {
        if (_mouthOpenY == null) return;

        float volume = 0f;

        if (audioSource.isPlaying && audioSource.clip != null)
        {
            float[] samples     = new float[256];
            int     channels    = audioSource.clip.channels;
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
    // Update：DispatchMessageQueue（WebGL 以外需要手動 Dispatch）
    // ─────────────────────────────────────────
    void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        websocket.DispatchMessageQueue();
#endif
    }

    // ─────────────────────────────────────────
    // iOS Native Plugin：設定 AVAudioSession
    // ─────────────────────────────────────────
#if UNITY_IOS && !UNITY_EDITOR
    [System.Runtime.InteropServices.DllImport("__Internal")]
    static extern void ConfigureAudioSession();
#else
    static void ConfigureAudioSession()
    {
        Debug.Log("[AudioSession] 非 iOS 平台，略過設定");
    }
#endif

    // ─────────────────────────────────────────
    private async void OnApplicationQuit()
    {
        if (websocket != null)
            await websocket.Close();
    }
}
