using UnityEngine;
using UnityEngine.Networking;
using NativeWebSocket;
using Newtonsoft.Json;
using System.Collections;
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

    // UIManager 引用
    private UIManager _uiManager;

    [System.Serializable]
    public class StreamerResponse
    {
        public string dialogue;
        public string emotion;
        public string action;
        public string audio_url;
    }

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
        else
        {
            Debug.LogError("❌ 找不到 CubismModel！");
        }

        // 自動尋找 MotionController
        if (motionController == null)
            motionController = FindAnyObjectByType<MotionController>();
    }

    async void Start()
    {
        websocket = new WebSocket("ws://localhost:8000/ws");
        websocket.OnOpen  += () => Debug.Log("✅ 已成功連接至 AI 主播後端！");
        websocket.OnError += (e) => Debug.Log("❌ 連線錯誤: " + e);
        websocket.OnClose += (c) => Debug.Log("🔌 連線已中斷。");

        websocket.OnMessage += (bytes) =>
        {
            var message = System.Text.Encoding.UTF8.GetString(bytes);
            Debug.Log("📦 收到後端包裹: " + message);
            StreamerResponse data = JsonConvert.DeserializeObject<StreamerResponse>(message);
            StartCoroutine(ProcessStreamerResponse(data));
        };

        await websocket.Connect();
    }

    void Update()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        websocket.DispatchMessageQueue();
#endif

        if (Input.GetKeyDown(KeyCode.Space))
            SendText("你好，小光！請問今天的氣溫如何？");
    }

    public void StartMicInput()
    {
        Debug.Log("🎤 麥克風功能將在步驟 14 實作");
    }

    public async void SendText(string text)
    {
        if (websocket != null && websocket.State == WebSocketState.Open)
            await websocket.SendText(text);
        else
            Debug.LogWarning("⚠ WebSocket 未連線，無法發送訊息");
    }

    // LipSync
    void LateUpdate()
    {
        if (_mouthOpenY == null) return;

        float volume = 0f;

        if (audioSource.isPlaying && audioSource.clip != null)
        {
            float[] samples = new float[256];
            int channels    = audioSource.clip.channels;
            int sampleOffset = audioSource.timeSamples * channels;
            int totalSamples = audioSource.clip.samples * channels;

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

    IEnumerator ProcessStreamerResponse(StreamerResponse data)
    {
        Debug.Log($"主播說：{data.dialogue} [表情: {data.emotion}] [動作: {data.action}]");

        // ── 更新聊天室 UI ──
        _uiManager?.AddAIMessage(data.dialogue);

        // ── 表情連動（立即切換，LateUpdate 會平滑插值）──
        if (motionController != null)
        {
            motionController.PlayExpression(data.emotion);
        }
        else
        {
            Debug.LogWarning("⚠ 找不到 MotionController，表情略過");
        }

        // ── 音訊下載與播放 ──
        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(data.audio_url, AudioType.WAV))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                if (clip != null && clip.loadState == AudioDataLoadState.Loaded)
                {
                    audioSource.clip = clip;
                    audioSource.Play();
                    Debug.Log($"▶ 開始播放，長度: {clip.length} 秒");

                    // ── 動作連動（語音開始播放後觸發，更自然）──
                    if (motionController != null)
                        motionController.PlayMotion(data.action);
                }
            }
            else
            {
                Debug.LogError("❌ 語音下載失敗: " + www.error);
            }
        }
    }

    private async void OnApplicationQuit()
    {
        if (websocket != null)
            await websocket.Close();
    }
}