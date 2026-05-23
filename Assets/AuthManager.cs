// Assets/Scripts/AuthManager.cs
// 啟動時向後端取得 Token，交給 StreamerClient 建立 WebSocket

using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json.Linq;

public class AuthManager : MonoBehaviour
{
    [Header("References")]
    public StreamerClient streamerClient;

    // Token 取得端點（與後端同 host）
    private string _tokenUrl;
    private string _token = "";

    void Start()
    {
        // 動態取得 host（與 StreamerClient 相同邏輯）
        string host = "localhost:8000";
		string httpScheme = "http"; // 新增

#if UNITY_WEBGL && !UNITY_EDITOR
        string pageUrl = Application.absoluteURL;
        host = pageUrl.Replace("https://", "")
                      .Replace("http://", "")
                      .Split('/')[0];
		httpScheme = pageUrl.StartsWith("https://") ? "https" : "http";
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

                // 把 Token 傳給 StreamerClient 再連線
                streamerClient.ConnectWithToken(_token);
            }
            else
            {
                Debug.LogError($"[Auth] Token 取得失敗: {req.error}");
                // 失敗時仍嘗試連線（開發模式降級）
                streamerClient.ConnectWithToken("");
            }
        }
    }
}