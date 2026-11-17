// SPDX-License-Identifier: AGPL-3.0-only
// Controls only media elements in the local Xbox WebView. Never touches console/TV audio.
(function () {
 if (!/(^|\.)xbox\.com$/.test(location.hostname)) return;
 const state = window.__arabicPhoneAudio || (window.__arabicPhoneAudio = {volumes:new WeakMap()});
 function apply() {
  document.querySelectorAll('video,audio').forEach(media => {
   if (!state.volumes.has(media)) state.volumes.set(media, media.volume > 0 ? media.volume : 1);
   if (window.__arabicPhoneMuted !== false) { if(!media.muted)media.muted=true; }
   else { if(media.muted)media.muted=false;if(media.volume===0)media.volume=state.volumes.get(media)||1; }
  });
 }
 apply();
 if(!state.observer && typeof MutationObserver==='function'){
  state.observer=new MutationObserver(apply);state.observer.observe(document.documentElement,{childList:true,subtree:true});
 }
})();
