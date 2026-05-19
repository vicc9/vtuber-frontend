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
    micBtn.style.cssText = [
      'position: fixed', 'bottom: 0', 'right: 0',
      'width: 72px', 'height: 52px', 'font-size: 16px',
      'background: #666', 'color: #ccc', 'border: none',
      'border-top: 2px solid #333', 'cursor: not-allowed',
      'z-index: 99999',
    ].join(';');
    document.body.appendChild(micBtn);

    function submitInput() {
      var val = input.value.trim();
      if (!val) return;
      
      // 改用 window.unityInstance 確保能抓到
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

    // 關鍵修正：阻止 Unity 偷走鍵盤事件，讓英文跟數字可以正常輸入
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

    micBtn.addEventListener('click', function() {
      alert('\u7db2\u9801\u7248\u4e0d\u652f\u63f4\u8a9e\u97f3\u8f38\u5165\n\u8acb\u4f7f\u7528 iOS \u6216 Android App');
    });
    
    setTimeout(function() { input.focus(); }, 500);
    console.log('[IME] \u6301\u4e45\u8f38\u5165\u6846\u5df2\u5efa\u7acb');
  },

  ClearNativeInput: function() {
    var el = document.getElementById('unity-ime-input');
    if (el) { el.value = ''; el.focus();}
  },

  ShowNativeInput: function(x, y, width, height, defaultTextPtr) {},
  HideNativeInput: function() {}
});