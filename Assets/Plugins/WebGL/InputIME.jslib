mergeInto(LibraryManager.library, {

  ShowNativeInput: function(x, y, width, height, defaultTextPtr) {
    var defaultText = UTF8ToString(defaultTextPtr);

    var old = document.getElementById('unity-native-input');
    if (old) old.remove();

    var canvas = document.getElementById('unity-canvas') ||
                 document.querySelector('canvas');
    if (!canvas) return;

    var rect   = canvas.getBoundingClientRect();
    var scaleX = rect.width  / canvas.width;
    var scaleY = rect.height / canvas.height;

    var input       = document.createElement('input');
    input.id        = 'unity-native-input';
    input.type      = 'text';
    input.value     = defaultText;
    input.setAttribute('autocomplete', 'off');
    input.setAttribute('autocorrect',  'off');
    input.setAttribute('spellcheck',   'false');

    input.style.cssText = [
      'position: fixed',
      'left: '        + (rect.left + x * scaleX) + 'px',
      'top: '         + (rect.top  + y * scaleY) + 'px',
      'width: '       + (width  * scaleX) + 'px',
      'height: '      + (height * scaleY) + 'px',
      'font-size: 18px',
      'padding: 4px 8px',
      'border: 2px solid #4A90E2',
      'border-radius: 4px',
      'background: rgba(255,255,255,0.97)',
      'color: #222',
      'outline: none',
      'z-index: 99999',
      'box-sizing: border-box',
      'font-family: sans-serif',
    ].join(';');

    document.body.appendChild(input);

    // 稍微延遲 focus 確保 IME 正確啟動
    setTimeout(function() { input.focus(); }, 50);

    input.addEventListener('input', function() {
      // 即時同步（composing 中也同步，讓 Unity 看到輸入過程）
      if (typeof unityInstance !== 'undefined') {
        unityInstance.SendMessage('IMEBridge', 'OnNativeInputChanged', input.value);
      }
    });

    input.addEventListener('keydown', function(e) {
      if (e.key === 'Enter' && !e.isComposing) {
        var val = input.value;
        if (typeof unityInstance !== 'undefined') {
          unityInstance.SendMessage('IMEBridge', 'OnNativeInputSubmit', val);
        }
        input.remove();
        e.preventDefault();
      }
      if (e.key === 'Escape') {
        input.remove();
      }
    });

    input.addEventListener('blur', function() {
      setTimeout(function() {
        var el = document.getElementById('unity-native-input');
        if (el) el.remove();
      }, 300);
    });
  },

  HideNativeInput: function() {
    var input = document.getElementById('unity-native-input');
    if (input) input.remove();
  }

});
