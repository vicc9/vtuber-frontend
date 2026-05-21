mergeInto(LibraryManager.library, {

  InitPersistentInput: function() {
    if (document.getElementById('unity-ime-input')) return;

    var input = document.createElement('input');
    input.id   = 'unity-ime-input';
    input.type = 'text';
    input.placeholder = '在此輸入訊息，按 Enter 送出...';
    input.setAttribute('autocomplete',   'off');
    input.setAttribute('autocorrect',    'off');
    input.setAttribute('autocapitalize', 'none');
    input.setAttribute('spellcheck',     'false');
    input.style.cssText = [
      'position: fixed', 'bottom: 0', 'left: 0',
      'width: calc(100% - 144px)', 'height: 52px',
      'font-size: 17px', 'padding: 0 14px',
      'border: none', 'border-top: 2px solid #333',
      'background: rgba(245,245,245,0.98)', 'color: #111',
      'outline: none', 'z-index: 99999', 'box-sizing: border-box',
      'font-family: "Noto Sans TC", "Microsoft JhengHei", sans-serif',
    ].join(';');
    document.body.appendChild(input);

    var sendBtn = document.createElement('button');
    sendBtn.id          = 'unity-send-btn';
    sendBtn.textContent = '\u9001\u51fa'; // 送出
    sendBtn.style.cssText = [
      'position: fixed', 'bottom: 0', 'right: 72px',
      'width: 72px', 'height: 52px', 'font-size: 16px',
      'font-weight: bold', 'background: #4A90E2', 'color: white',
      'border: none', 'border-top: 2px solid #333',
      'cursor: pointer', 'z-index: 99999',
    ].join(';');
    document.body.appendChild(sendBtn);

    var micBtn = document.createElement('button');
    micBtn.id          = 'unity-mic-btn';
    micBtn.textContent = '\u8a9e\u97f3'; // 語音
    
    // 🌟 修正 1：將原本灰色不可點擊的樣式，改成可點擊的綠色按鈕外觀
    micBtn.style.cssText = [
      'position: fixed', 'bottom: 0', 'right: 0',
      'width: 72px', 'height: 52px', 'font-size: 16px',
      'font-weight: bold', 'background: #27AE60', 'color: white', 
      'border: none', 'border-top: 2px solid #333', 
      'cursor: pointer', 'z-index: 99999',
    ].join(';');
    document.body.appendChild(micBtn);

    function submitInput() {
      var val = input.value.trim();
      if (!val) return;
      
      if (typeof window.unityInstance !== 'undefined') {
        window.unityInstance.SendMessage('UIManagerBridge', 'OnHTMLSubmit', val);
      } else {
        console.error("[IME Error] 找不到 window.unityInstance，請檢查 index.html！");
      }
      
      input.value = '';
      input.focus();
    }

    input.addEventListener('input', function() {
      if (typeof window.unityInstance !== 'undefined') {
        window.unityInstance.SendMessage('IMEBridge', 'OnNativeInputChanged', input.value);
      }
    });

    var stopPropagation = function(e) { e.stopPropagation(); };
    input.addEventListener('keydown', function(e) {
      e.stopPropagation(); 
      if (e.key === 'Enter' && !e.isComposing) {
        submitInput();
        e.preventDefault();
      }
    });
    input.addEventListener('keyup', stopPropagation);
    input.addEventListener('keypress', stopPropagation);

    sendBtn.addEventListener('click', function() {
      submitInput();
    });

    // 🌟 修正 2：解鎖語音按鈕！點擊時直接觸發 Unity 內的 AudioRecorderObject
    micBtn.addEventListener('click', function() {
      if (typeof window.unityInstance !== 'undefined') {
        // 呼叫我們在 AudioRecorder.cs 設定的 WebGL 公開點擊方法
        window.unityInstance.SendMessage('AudioRecorderObject', 'OnMicButtonClickPublic');
      } else {
        console.error("[IME Error] 找不到 window.unityInstance，無法觸發錄音！");
      }
    });
    
    setTimeout(function() { input.focus(); }, 500);
    console.log('[IME] 持久輸入框與語音按鈕已成功建立並綁定');
  },

  ClearNativeInput: function() {
    var el = document.getElementById('unity-ime-input');
    if (el) { el.value = ''; el.focus();}
  },

  ShowNativeInput: function(x, y, width, height, defaultTextPtr) {},
  HideNativeInput: function() {},

  // 🌟 修正 3：補上本次打包遺漏的關鍵函數（注意前方的逗號隔開）
  // 讓 Unity 收到 STT (語音轉文字) 結果時，能把文字塞回這個網頁輸入框中
  SetWebGLInputValue: function(strPtr) {
    var text = UTF8ToString(strPtr);
    var input = document.getElementById('unity-ime-input');
    if (input) {
      input.value = text;
      
      // 觸發原生 input 事件，確保如果有其他網頁組件在監聽能同步更新
      var event = new Event('input', { bubbles: true });
      input.dispatchEvent(event);
    }
  }
});