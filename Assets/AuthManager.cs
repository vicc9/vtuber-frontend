// Assets/Scripts/AuthManager.cs
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json.Linq;

public class AuthManager : MonoBehaviour
{
    [Header("References")]
    public StreamerClient streamerClient;

    private string _tokenUrl;
    private string _token = "";

    void Start()
    {
        string host = "localhost:8000";
        string httpScheme = "http";

#if UNITY_WEBGL && !UNITY_EDITOR
        host = "vtuber-backend-qwmt.onrender.com";
        httpScheme = "https";
#endif
        _tokenUrl = $"{httpScheme}://{host}/api/token?client_id=vtuber_app";
        StartCoroutine(FetchTokenAndConnect());
    }

    IEnumerator FetchTokenAndConnect()
    {
        Debug.Log("[Auth] 正在取得 Token...");

        using (UnityWebRequest req = UnityWebRequest.Get(_tokenUrl))
        {
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                var json = JObject.Parse(req.downloadHandler.text);
                _token = json["token"]?.ToString() ?? "";
                Debug.Log($"[Auth] Token 取得成功");
                streamerClient.ConnectWithToken(_token);
            }
            else
            {
                Debug.LogError($"[Auth] Token 取得失敗: {req.error}");
                streamerClient.ConnectWithToken("");
            }
        }
    }
}