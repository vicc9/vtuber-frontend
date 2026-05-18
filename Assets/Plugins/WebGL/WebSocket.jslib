mergeInto(LibraryManager.library, {

  WebSocketConnect: function(instanceId, url, protocols) {
    var parsedUrl = UTF8ToString(url);
    var parsedProtocols = UTF8ToString(protocols);
    var socket;

    if (parsedProtocols.length === 0) {
      socket = new WebSocket(parsedUrl);
    } else {
      socket = new WebSocket(parsedUrl, parsedProtocols.split(','));
    }

    socket.binaryType = 'arraybuffer';

    var id = instanceId;

    socket.onopen = function() {
      Module.webSocketInstances[id].state = 1;
      dynCall('vi', Module.webSocketInstances[id].onOpen, [id]);
    };

    socket.onmessage = function(e) {
      if (e.data instanceof ArrayBuffer) {
        var dataBuffer = new Uint8Array(e.data);
        var buffer = _malloc(dataBuffer.length);
        HEAPU8.set(dataBuffer, buffer);
        dynCall('viii', Module.webSocketInstances[id].onMessage, [id, buffer, dataBuffer.length]);
        _free(buffer);
      } else {
        var str = e.data;
        var buffer = _malloc(lengthBytesUTF8(str) + 1);
        stringToUTF8(str, buffer, lengthBytesUTF8(str) + 1);
        dynCall('viii', Module.webSocketInstances[id].onMessage, [id, buffer, -1]);
        _free(buffer);
      }
    };

    socket.onerror = function(e) {
      dynCall('vi', Module.webSocketInstances[id].onError, [id]);
    };

    socket.onclose = function(e) {
      Module.webSocketInstances[id].state = 3;
      dynCall('vii', Module.webSocketInstances[id].onClose, [id, e.code]);
    };

    if (!Module.webSocketInstances) {
      Module.webSocketInstances = {};
    }

    if (!Module.webSocketInstances[id]) {
      Module.webSocketInstances[id] = {};
    }

    Module.webSocketInstances[id].socket = socket;
    Module.webSocketInstances[id].state = 0;

    return id;
  },

  WebSocketSetOnOpen: function(instanceId, callback) {
    if (!Module.webSocketInstances) Module.webSocketInstances = {};
    if (!Module.webSocketInstances[instanceId]) Module.webSocketInstances[instanceId] = {};
    Module.webSocketInstances[instanceId].onOpen = callback;
  },

  WebSocketSetOnMessage: function(instanceId, callback) {
    if (!Module.webSocketInstances) Module.webSocketInstances = {};
    if (!Module.webSocketInstances[instanceId]) Module.webSocketInstances[instanceId] = {};
    Module.webSocketInstances[instanceId].onMessage = callback;
  },

  WebSocketSetOnError: function(instanceId, callback) {
    if (!Module.webSocketInstances) Module.webSocketInstances = {};
    if (!Module.webSocketInstances[instanceId]) Module.webSocketInstances[instanceId] = {};
    Module.webSocketInstances[instanceId].onError = callback;
  },

  WebSocketSetOnClose: function(instanceId, callback) {
    if (!Module.webSocketInstances) Module.webSocketInstances = {};
    if (!Module.webSocketInstances[instanceId]) Module.webSocketInstances[instanceId] = {};
    Module.webSocketInstances[instanceId].onClose = callback;
  },

  WebSocketSend: function(instanceId, dataPtr, dataLength) {
    var instance = Module.webSocketInstances[instanceId];
    if (!instance || !instance.socket) return -1;
    if (instance.socket.readyState !== 1) return -1;
    var data = HEAPU8.buffer.slice(dataPtr, dataPtr + dataLength);
    instance.socket.send(data);
    return 0;
  },

  WebSocketSendText: function(instanceId, message) {
    var instance = Module.webSocketInstances[instanceId];
    if (!instance || !instance.socket) return -1;
    if (instance.socket.readyState !== 1) return -1;
    instance.socket.send(UTF8ToString(message));
    return 0;
  },

  WebSocketClose: function(instanceId, code, reason) {
    var instance = Module.webSocketInstances[instanceId];
    if (!instance || !instance.socket) return -1;
    instance.socket.close(code, UTF8ToString(reason));
    return 0;
  },

  WebSocketGetState: function(instanceId) {
    var instance = Module.webSocketInstances[instanceId];
    if (!instance || !instance.socket) return 3;
    return instance.socket.readyState;
  }

});
