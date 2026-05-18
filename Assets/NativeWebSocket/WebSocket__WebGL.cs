#if UNITY_WEBGL && !UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using AOT;
using UnityEngine;

namespace NativeWebSocket
{
    public class WebSocket : IWebSocket
    {
        // ── JS Interop 宣告（對應 WebSocket.jslib）──────────────
        [DllImport("__Internal")] private static extern int  WebSocketConnect(int instanceId, string url, string protocols);
        [DllImport("__Internal")] private static extern int  WebSocketClose(int instanceId, int code, string reason);
        [DllImport("__Internal")] private static extern int  WebSocketSend(int instanceId, byte[] dataPtr, int dataLength);
        [DllImport("__Internal")] private static extern int  WebSocketSendText(int instanceId, string message);
        [DllImport("__Internal")] private static extern int  WebSocketGetState(int instanceId);
        [DllImport("__Internal")] private static extern void WebSocketSetOnOpen(int instanceId, Action<int> callback);
        [DllImport("__Internal")] private static extern void WebSocketSetOnMessage(int instanceId, Action<int, IntPtr, int> callback);
        [DllImport("__Internal")] private static extern void WebSocketSetOnError(int instanceId, Action<int> callback);
        [DllImport("__Internal")] private static extern void WebSocketSetOnClose(int instanceId, Action<int, int> callback);

        // ── 靜態實例管理 ────────────────────────────────────────
        private static int _nextId = 1;
        private static readonly Dictionary<int, WebSocket> _instances = new();

        private int _id;
        private string _url;

        public event WebSocketOpenEventHandler    OnOpen;
        public event WebSocketMessageEventHandler OnMessage;
        public event WebSocketErrorEventHandler   OnError;
        public event WebSocketCloseEventHandler   OnClose;

        public WebSocket(string url, Dictionary<string, string> headers = null)
        {
            _url = url;
            _id  = _nextId++;
            _instances[_id] = this;
        }

        public WebSocket(string url, string subprotocol, Dictionary<string, string> headers = null)
            : this(url, headers) { }

        public WebSocket(string url, List<string> subprotocols, Dictionary<string, string> headers = null)
            : this(url, headers) { }

        // ── 狀態 ────────────────────────────────────────────────
        public WebSocketState State
        {
            get
            {
                int s = WebSocketGetState(_id);
                return s switch
                {
                    0 => WebSocketState.Connecting,
                    1 => WebSocketState.Open,
                    2 => WebSocketState.Closing,
                    _ => WebSocketState.Closed,
                };
            }
        }

        // ── 連線 ────────────────────────────────────────────────
        public Task Connect()
        {
            WebSocketSetOnOpen(_id, OnOpenCallback);
            WebSocketSetOnMessage(_id, OnMessageCallback);
            WebSocketSetOnError(_id, OnErrorCallback);
            WebSocketSetOnClose(_id, OnCloseCallback);
            WebSocketConnect(_id, _url, "");
            return Task.CompletedTask;
        }

        // ── 傳送 ────────────────────────────────────────────────
        public Task Send(byte[] data)
        {
            WebSocketSend(_id, data, data.Length);
            return Task.CompletedTask;
        }

        public Task SendText(string message)
        {
            WebSocketSendText(_id, message);
            return Task.CompletedTask;
        }

        // ── 關閉 ────────────────────────────────────────────────
        public Task Close(WebSocketCloseCode code = WebSocketCloseCode.Normal, string reason = null)
        {
            WebSocketClose(_id, (int)code, reason ?? "");
            return Task.CompletedTask;
        }

        // ── WebGL 不需要手動 Dispatch ────────────────────────────
        public void DispatchMessageQueue() { }
        public void CancelConnection()     { }

        // ── JS Callback（static，由 jslib 呼叫）─────────────────
        [MonoPInvokeCallback(typeof(Action<int>))]
        private static void OnOpenCallback(int id)
        {
            if (_instances.TryGetValue(id, out var ws))
                ws.OnOpen?.Invoke();
        }

        [MonoPInvokeCallback(typeof(Action<int, IntPtr, int>))]
        private static void OnMessageCallback(int id, IntPtr dataPtr, int dataLen)
        {
            if (!_instances.TryGetValue(id, out var ws)) return;

            if (dataLen == -1)
            {
                // 文字訊息
                string text = Marshal.PtrToStringUTF8(dataPtr);
                ws.OnMessage?.Invoke(System.Text.Encoding.UTF8.GetBytes(text ?? ""));
            }
            else
            {
                // Binary 訊息
                byte[] data = new byte[dataLen];
                Marshal.Copy(dataPtr, data, 0, dataLen);
                ws.OnMessage?.Invoke(data);
            }
        }

        [MonoPInvokeCallback(typeof(Action<int>))]
        private static void OnErrorCallback(int id)
        {
            if (_instances.TryGetValue(id, out var ws))
                ws.OnError?.Invoke("WebSocket error");
        }

        [MonoPInvokeCallback(typeof(Action<int, int>))]
        private static void OnCloseCallback(int id, int code)
        {
            if (_instances.TryGetValue(id, out var ws))
            {
                ws.OnClose?.Invoke(WebSocketHelpers.ParseCloseCodeEnum(code));
                _instances.Remove(id);
            }
        }
    }
}

#endif // UNITY_WEBGL && !UNITY_EDITOR