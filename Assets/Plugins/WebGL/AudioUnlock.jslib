mergeInto(LibraryManager.library, {

  UnlockAudioContext: function() {
    var unlock = function() {
      // Unity WebGL 的 AudioContext 存在 WEBAudio.audioContext
      var ctx = Module['WEBAudio'] && Module['WEBAudio']['audioContext'];
      if (!ctx) {
        // 嘗試另一個路徑
        ctx = window['WEBAudio'] && window['WEBAudio']['audioContext'];
      }
      if (ctx && ctx.state === 'suspended') {
        ctx.resume().then(function() {
          console.log('[Audio] AudioContext 已解鎖');
        });
      }
      document.removeEventListener('click',      unlock);
      document.removeEventListener('touchstart', unlock);
      document.removeEventListener('keydown',    unlock);
    };

    document.addEventListener('click',      unlock, { once: true });
    document.addEventListener('touchstart', unlock, { once: true });
    document.addEventListener('keydown',    unlock, { once: true });
    console.log('[Audio] AudioContext 解鎖監聽器已註冊');
  }

});
