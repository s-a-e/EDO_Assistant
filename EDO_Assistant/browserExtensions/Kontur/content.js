var REQUEST_TYPE = 'kontur-toolbox-request';
var RESPONSE_TYPE = 'kontur-toolbox-response';
var INSTALLATION_FLAG_ATTRIBUTE = 'kontur-toolbox-installed';
var KONTUR_HOST_NAMES = ['.kontur.ru', '.kontur-ca.ru', '.kontur-extern.ru', '.kontur', '.testkontur.ru'];
var IS_KONTUR_HOST = false;

function checkCurrentHostName() {
    var elem = document.createElement('a');
    elem.href = window.location.href;
    var currentHostName = elem.hostname;
    for (var i = 0; i < KONTUR_HOST_NAMES.length; ++i) {
        var hostName = KONTUR_HOST_NAMES[i];
        if (currentHostName.indexOf(hostName, currentHostName.length - hostName.length) !== -1) {
            return true;
        }
    }
    return false;
}

try {
    IS_KONTUR_HOST = checkCurrentHostName();
    if (IS_KONTUR_HOST) {
        document.addEventListener('DOMContentLoaded', function(event) {
            if (!!document.head) {
                var meta = document.createElement('meta');
                meta.setAttribute(INSTALLATION_FLAG_ATTRIBUTE, 'true');
                document.head.appendChild(meta);
            }
        });
    }
} catch (err) {
}

var send = (function() {
    var ports = {};

    function onPortResponse(response) {
        var result = response.result || response.error;
        if (result && result.type === 'extension' && result.uninstalled) {
            window.removeEventListener('message', handleMessage);
        }

        window.postMessage({
            type: RESPONSE_TYPE, 
            response: response
        }, '*');
    }

    function onDisconnect(sessionId) {
        delete ports[sessionId];
        var error = chrome.runtime.lastError;
        var message = !!error
            ? (!!error.message ? error.message : error)
            : 'disconnect from background script';
        onPortResponse({
            sessionId: sessionId,
            error: {
                type: 'connect',
                message: message
            }
        });
    }

    return function(request) {
        try {
            var sessionId = request.sessionId;
            var port = ports[sessionId];
            if (!port) {
                port = chrome.runtime.connect({ name: sessionId });
                ports[sessionId] = port;
                port.onMessage.addListener(onPortResponse);
                port.onDisconnect.addListener(function() { onDisconnect(sessionId); });
            }
            port.postMessage(request);
        } catch (err) {
            onPortResponse({
                sessionId: request.sessionId,
                commandId: request.commandId,
                error: {
                    type: 'connect',
                    message: err.message || 'failed to send message to background script'
                }
            });
        }
    };
}());

function handleMessage(ev) {
    if (ev.origin === 'null' || ev.source != window || !ev.data) {
        return;
    }

    var data = ev.data;
    if (data.type !== REQUEST_TYPE)
        return;

    var request = data.request;
    if (!request || !request.sessionId)
        return;

    if (request.type === 'extension.uninstall') {
        if (!IS_KONTUR_HOST) {
            window.postMessage({
                type: RESPONSE_TYPE,
                response: {
                    sessionId: request.sessionId,
                    commandId: request.commandId,
                    error: { type: 'extension', message: 'bad request' }
                }
            }, '*');
            return;
        }

        if (request.extensionId !== chrome.runtime.id)
            return;
    }

    request.hostUri = ev.origin;
    send(request);
}

window.addEventListener('message', handleMessage, false);
