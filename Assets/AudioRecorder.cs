// Assets/Scripts/AudioRecorder.cs
// 支援平台：iOS、Android
// WebGL：透過 WebGLMic.jslib 呼叫瀏覽器 MediaRecorder API 錄音

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class AudioRecorder : MonoBehaviour
{
    [Header("References")]
    public Button      micButton;
    public UIManager   uiManager;
    public StreamerClient streamerClient;

    [Header("Settings")]
    public int   sampleRate       = 16000;
    public int   maxRecordSeconds = 30;
    public Color recordingColor   = new Color(0.9f, 0.2f, 0.2f, 1f);
    public Color idleColor        = Color.white;

// ═══════════════════════════════════════════════════════════
// WebGL 以外的平台才編譯以下程式碼 (iOS, Android, Editor)
// ═══════════════════════════════════════════════════════════
#if !UNITY_WEBGL || UNITY_EDITOR

    private AudioClip _recordingClip;
    private bool      _isRecording       = false;
    private float     _recordStartTime;
    private bool      _permissionGranted = false;

    void Start()
    {
        micButton.onClick.AddListener(OnMicButtonClick);
        StartCoroutine(RequestMicPermission());
    }

    IEnumerator RequestMicPermission()
    {
#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
        if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
            yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
        _permissionGranted = Application.HasUserAuthorization(UserAuthorization.Microphone);
#else
        _permissionGranted = true;
        yield return null;
#endif
        if (!_permissionGranted)
            uiManager.ShowSystemMessage("請授予麥克風權限以使用語音功能");
    }

    public void OnMicButtonClickPublic() => OnMicButtonClick();

    void OnMicButtonClick()
    {
        if (!_permissionGranted)
        {
            uiManager.ShowSystemMessage("尚未取得麥克風權限");
            StartCoroutine(RequestMicPermission());
            return;
        }
        if (!_isRecording) StartRecording();
        else               StopRecordingAndSend();
    }

    void StartRecording()
    {
        _recordingClip   = Microphone.Start(null, false, maxRecordSeconds, sampleRate);
        _isRecording     = true;
        _recordStartTime = Time.time;
        SetButtonColor(recordingColor);
        uiManager.ShowSystemMessage("● 錄音中…點擊停止");
        Debug.Log("[AudioRecorder] 開始原生錄音");
    }

    void StopRecordingAndSend()
    {
        if (!_isRecording) return;

        int micPos = Microphone.GetPosition(null);
        Microphone.End(null);
        _isRecording = false;
        SetButtonColor(idleColor);

        float duration = Time.time - _recordStartTime;
        if (duration < 0.5f)
        {
            uiManager.ShowSystemMessage("錄音太短，請重試");
            return;
        }

        Debug.Log($"[AudioRecorder] 停止原生錄音，時長={duration:F1}s，樣本={micPos}");

        AudioClip trimmed  = TrimClip(_recordingClip, micPos);
        byte[]    wavBytes = AudioClipToWav(trimmed);
        uiManager.ShowSystemMessage("🔄 辨識中…");
        streamerClient.SendAudioBytes(wavBytes);
    }

    AudioClip TrimClip(AudioClip clip, int samples)
    {
        float[]   data    = new float[samples];
        clip.GetData(data, 0);
        AudioClip trimmed = AudioClip.Create("trimmed", samples, 1, sampleRate, false);
        trimmed.SetData(data, 0);
        return trimmed;
    }

    byte[] AudioClipToWav(AudioClip clip)
    {
        float[] samples = new float[clip.samples];
        clip.GetData(samples, 0);

        Int16[] intData = new Int16[samples.Length];
        for (int i = 0; i < samples.Length; i++)
            intData[i] = (Int16)(Mathf.Clamp(samples[i], -1f, 1f) * 32767);

        byte[] pcm = new byte[intData.Length * 2];
        Buffer.BlockCopy(intData, 0, pcm, 0, pcm.Length);
        return BuildWavFile(pcm, clip.frequency, 1);
    }

    byte[] BuildWavFile(byte[] pcm, int rate, int channels)
    {
        int    byteRate = rate * channels * 2;
        byte[] wav      = new byte[44 + pcm.Length];

        System.Text.Encoding.ASCII.GetBytes("RIFF").CopyTo(wav, 0);
        BitConverter.GetBytes(36 + pcm.Length).CopyTo(wav, 4);
        System.Text.Encoding.ASCII.GetBytes("WAVE").CopyTo(wav, 8);
        System.Text.Encoding.ASCII.GetBytes("fmt ").CopyTo(wav, 12);
        BitConverter.GetBytes(16).CopyTo(wav, 16);
        BitConverter.GetBytes((short)1).CopyTo(wav, 20);
        BitConverter.GetBytes((short)channels).CopyTo(wav, 22);
        BitConverter.GetBytes(rate).CopyTo(wav, 24);
        BitConverter.GetBytes(byteRate).CopyTo(wav, 28);
        BitConverter.GetBytes((short)(channels * 2)).CopyTo(wav, 32);
        BitConverter.GetBytes((short)16).CopyTo(wav, 34);
        System.Text.Encoding.ASCII.GetBytes("data").CopyTo(wav, 36);
        BitConverter.GetBytes(pcm.Length).CopyTo(wav, 40);
        pcm.CopyTo(wav, 44);

        return wav;
    }

    void Update()
    {
        if (_isRecording && Time.time - _recordStartTime >= maxRecordSeconds)
        {
            Debug.LogWarning("[AudioRecorder] 達到最長錄音時間，自動停止");
            StopRecordingAndSend();
        }
    }

#else
// ═══════════════════════════════════════════════════════════
// WebGL 平台編譯此區塊：透過前端 JavaScript 橋接器錄音
// ═══════════════════════════════════════════════════════════

#if UNITY_WEBGL && !UNITY_EDITOR
    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern void StartWebGLMic();

    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern void StopWebGLMic();
#endif

    private bool  _isRecording       = false;
    private float _recordStartTime;
    private float _lastRecordDuration;

    void Start()
    {
        if (micButton != null)
        {
            micButton.onClick.AddListener(OnMicButtonClick);
        }
        Debug.Log("[AudioRecorder] WebGL 模式已啟動，準備透過瀏覽器進行語音輸入");
    }

    public void OnMicButtonClickPublic() => OnMicButtonClick();

    void OnMicButtonClick()
    {
        if (!_isRecording) StartWebGLRecording();
        else               StopWebGLRecording();
    }

    void StartWebGLRecording()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        StartWebGLMic();
#endif
        _isRecording = true;
        _recordStartTime = Time.time;
        SetButtonColor(recordingColor);
        uiManager.ShowSystemMessage("● 錄音中…點擊停止 (WebGL)");
        Debug.Log("[AudioRecorder] WebGL 開始錄音");
    }

    void StopWebGLRecording()
    {
        if (!_isRecording) return;

        _lastRecordDuration = Time.time - _recordStartTime;
#if UNITY_WEBGL && !UNITY_EDITOR
        StopWebGLMic();
#endif
        _isRecording = false;
        SetButtonColor(idleColor);
        uiManager.ShowSystemMessage("🔄 處理音訊中…");
        Debug.Log("[AudioRecorder] WebGL 停止錄音，等待瀏覽器回傳音訊");
    }

    /// <summary>
    /// 由 WebGL 端的 .jslib 透過 SendMessage 異步回傳的 Base64 音訊資料
    /// </summary>
    public void OnWebGLMicData(string base64Audio)
    {
        if (string.IsNullOrEmpty(base64Audio))
        {
            uiManager.ShowSystemMessage("錄音失敗或瀏覽器權限被拒絕");
            return;
        }

        if (_lastRecordDuration < 0.5f)
        {
            uiManager.ShowSystemMessage("錄音太短，請重試");
            return;
        }

        try
        {
            // 將 Base64 字串還原為二進位音訊（WebM 格式）
            byte[] audioBytes = Convert.FromBase64String(base64Audio);

            uiManager.ShowSystemMessage("🔄 辨識中…");
            // 直接發送給後端大腦 (Groq Whisper 支援 webm)
            streamerClient.SendAudioBytes(audioBytes);
            Debug.Log($"[AudioRecorder] WebGL 成功發送音訊位元組，大小: {audioBytes.Length} bytes");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AudioRecorder] 解析 WebGL 音訊 Base64 失敗: {ex.Message}");
            uiManager.ShowSystemMessage("音訊格式解析錯誤");
        }
    }

    void Update()
    {
        if (_isRecording && Time.time - _recordStartTime >= maxRecordSeconds)
        {
            Debug.LogWarning("[AudioRecorder] WebGL 達到最長錄音時間，自動停止");
            StopWebGLRecording();
        }
    }

#endif  // !UNITY_WEBGL || UNITY_EDITOR

// ═══════════════════════════════════════════════════════════
// 跨平台通用方法
// ═══════════════════════════════════════════════════════════
    void SetButtonColor(Color c)
    {
        if (micButton == null) return;
        var img = micButton.GetComponent<Image>();
        if (img != null) img.color = c;
    }
}