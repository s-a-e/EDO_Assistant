(()=>{"use strict";var e={322:(e,t)=>{var n;Object.defineProperty(t,"__esModule",{value:!0}),t.APP_ID=t.AM_CONNECTION_ID_INITIAL=t.APP_CONNECTION_ID=t.SERVICE_CONNECTION_ID=t.EXTENSION_LOADED_ID=t.TRACKING_ID=t.BROADCAST_TAB_ID=void 0,t.BROADCAST_TAB_ID=-1,t.TRACKING_ID=-111,t.EXTENSION_LOADED_ID=-30,t.SERVICE_CONNECTION_ID=-40,t.APP_CONNECTION_ID=-41,t.AM_CONNECTION_ID_INITIAL=-120,(n=t.APP_ID||(t.APP_ID={}))[n.SabyPlugin=101]="SabyPlugin",n[n.SabyAdmin=102]="SabyAdmin",n[n.SabyScreen=103]="SabyScreen",n[n.SabyCam=104]="SabyCam"},830:()=>{function e(){const e=document.createElement("script");e.src=chrome.runtime.getURL("injectExtensionId.js"),e.onload=function(){this.remove()},(document.head||document.documentElement).appendChild(e)}document.addEventListener("DOMContentLoaded",e),"complete"===document.readyState&&e()}},t={};function n(o){var r=t[o];if(void 0!==r)return r.exports;var _=t[o]={exports:{}};return e[o](_,_.exports,n),_.exports}(()=>{const e=n(322);n(830);const t="destructExtension_"+chrome.runtime.id;function o(t,n,o){if(t.tab_id===e.BROADCAST_TAB_ID){t.type="FROM_CONTENT_SCRIPT_TO_WEB_PAGE";try{window.postMessage(t,"*")}catch(e){console.error("Send message to web page fail ("+e.name+") : "+e.message)}}}document.dispatchEvent(new CustomEvent(t)),document.addEventListener(t,(()=>{chrome.runtime.onMessage.removeListener(o)})),chrome.runtime.onMessage.addListener(o)})()})();