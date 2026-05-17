// Assets/Scripts/AudioRecorder.cs
// 支援平台：iOS、Android
// WebGL：Microphone API 不存在，整個錄音功能在編譯時排除

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class AudioRecorder : MonoBehaviour
{
    [Header("References")]
    public Button         micButton;
    public UIManager      uiManager;
    public StreamerClient streamerClient;

    [Header("Settings")]
    public int   sampleRate       = 16000;
    public int   maxRecordSeconds = 30;
    public Color recordingColor   = new Color(0.9f, 0.2f, 0.2f, 1f);
    public Color idleColor        = Color.white;

// ═══════════════════════════════════════════════════════════
// WebGL 以外的平台才編譯以下程式碼
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
        Debug.Log("[AudioRecorder] 開始錄音");
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

        Debug.Log($"[AudioRecorder] 停止，時長={duration:F1}s，樣本={micPos}");

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

    void SetButtonColor(Color c)
    {
        var img = micButton.GetComponent<Image>();
        if (img != null) img.color = c;
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
// WebGL：空實作，讓編譯通過，執行時隱藏麥克風按鈕
// ═══════════════════════════════════════════════════════════

    void Start()
    {
        if (micButton != null)
            micButton.gameObject.SetActive(false);
        Debug.Log("[AudioRecorder] WebGL 平台，麥克風功能已停用");
    }

    public void OnMicButtonClickPublic() { }

#endif  // !UNITY_WEBGL || UNITY_EDITOR
}
