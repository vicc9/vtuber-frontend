mergeInto(LibraryManager.library, {

  // 開始錄音
  StartWebGLMic: function () {
    if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
      console.error("[WebGLMic] 瀏覽器不支援 getUserMedia");
      return;
    }

    window._webglMicChunks = [];
    window._webglMicRecording = true;

    navigator.mediaDevices.getUserMedia({ audio: true })
      .then(function (stream) {
        window._webglMicStream = stream;
        window._webglMicRecorder = new MediaRecorder(stream, {
          mimeType: MediaRecorder.isTypeSupported('audio/webm;codecs=opus')
            ? 'audio/webm;codecs=opus'
            : 'audio/webm'
        });

        window._webglMicRecorder.ondataavailable = function (e) {
          if (e.data && e.data.size > 0) {
            window._webglMicChunks.push(e.data);
          }
        };

        window._webglMicRecorder.onstop = function () {
          var blob = new Blob(window._webglMicChunks, {
            type: window._webglMicRecorder.mimeType
          });
          var reader = new FileReader();
          reader.onloadend = function () {
            // 取得 Base64 字串（去掉 data:...;base64, 前綴）
            var base64 = reader.result.split(',')[1];
            // 透過 SendMessage 回傳 Unity（GameObject 名稱需為 AudioRecorderObject）
            SendMessage('AudioRecorderObject', 'OnWebGLAudioReady', base64);
          };
          reader.readAsDataURL(blob);

          // 停止所有 track，釋放麥克風
          if (window._webglMicStream) {
            window._webglMicStream.getTracks().forEach(function (t) { t.stop(); });
            window._webglMicStream = null;
          }
        };

        window._webglMicRecorder.start();
        console.log("[WebGLMic] 錄音開始");
      })
      .catch(function (err) {
        console.error("[WebGLMic] 麥克風權限失敗：", err);
        SendMessage('AudioRecorderObject', 'OnWebGLMicError', err.message);
      });
  },

  // 停止錄音
  StopWebGLMic: function () {
    if (window._webglMicRecorder &&
        window._webglMicRecorder.state !== 'inactive') {
      window._webglMicRecorder.stop();
      console.log("[WebGLMic] 錄音停止，處理中...");
    }
  }
});