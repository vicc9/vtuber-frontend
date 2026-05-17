using UnityEngine;
using UnityEngine.Networking;
using NativeWebSocket;
using Newtonsoft.Json;
using System.Collections;
using Live2D.Cubism.Core;
using Live2D.Cubism.Framework;

public class StreamerClient : MonoBehaviour
{
    WebSocket websocket;

    [Header("音訊")]
    public AudioSource audioSource;

    [Header("Live2D 嘴巴參數")]
    public CubismModel cubismModel;
    private CubismParameter _mouthOpenY;

    // ✅ 新增：UIManager 引用
    private UIManager _uiManager;

    [System.Serializable]
    public class StreamerResponse
    {
        public string dialogue;
        public string emotion;
        public string action;
        public string audio_url;
    }

    void Awake() {
        // 取得 UIManager
        _uiManager = FindAnyObjectByType<UIManager>();

        // 取得 AudioSource
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

        // 取得 Live2D 嘴巴參數
        if (cubismModel == null) cubismModel = FindAnyObjectByType<CubismModel>();
        if (cubismModel != null) {
            _mouthOpenY = cubismModel.Parameters.FindById("ParamMouthOpenY");
            if (_mouthOpenY == null)
                Debug.LogError("❌ 找不到參數 ParamMouthOpenY！");
        } else {
            Debug.LogError("❌ 找不到 CubismModel！");
        }
    }

    async void Start()
    {
        websocket = new WebSocket("ws://localhost:8000/ws");
        websocket.OnOpen += () => Debug.Log("✅ 已成功連接至 AI 主播後端！");
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

    // ✅ 麥克風輸入（預留給步驟 14 實作）
    public void StartMicInput() {
        Debug.Log("🎤 麥克風功能將在步驟 14 實作");
    }

    // ✅ 修改：將原本的 SendTextToBackend 改名為 SendText 並公開
    public async void SendText(string text)
    {
        if (websocket != null && websocket.State == WebSocketState.Open) {
            await websocket.SendText(text);
        } else {
            Debug.LogWarning("⚠️ WebSocket 未連線，無法發送訊息");
        }
    }

    // ✅ LipSync 驅動
    void LateUpdate() {
        if (_mouthOpenY == null) return;

        float volume = 0f;

        if (audioSource.isPlaying && audioSource.clip != null) {
            float[] samples = new float[256];
            int channels = audioSource.clip.channels;
            int sampleOffset = audioSource.timeSamples * channels;
            int totalSamples = audioSource.clip.samples * channels;

            if (sampleOffset + 256 <= totalSamples) {
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

        // ✅ 更新聊天室 UI
        _uiManager?.AddAIMessage(data.dialogue);

        // ✅ 執行動作（需確認是否有對應的動作系統，先留 stub）
        PlayMotion(data.action);

        // ✅ 音訊下載與播放
        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(data.audio_url, AudioType.WAV))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                if (clip != null && clip.loadState == AudioDataLoadState.Loaded) {
                    audioSource.clip = clip;
                    audioSource.Play();
                    Debug.Log($"▶ 開始播放，長度: {clip.length} 秒");
                }
            }
            else
            {
                Debug.LogError("❌ 語音下載失敗: " + www.error);
            }
        }
    }

    // ✅ 預留：動作播放函式
    void PlayMotion(string actionName) {
        if (string.IsNullOrEmpty(actionName)) return;
        Debug.Log($"🎬 執行動作: {actionName}");
        // 這裡未來會放入 Animation 或 Cubism Motion 的觸發程式碼
    }

    private async void OnApplicationQuit()
    {
        if (websocket != null)
            await websocket.Close();
    }
}