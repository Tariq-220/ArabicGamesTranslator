// SPDX-License-Identifier: AGPL-3.0-only
// Independent implementation: reads only an accessible live Xbox video, never login fields.
(function () {
  const output = (status, extra) => JSON.stringify(Object.assign({status}, extra || {}));
  if (!/(^|\.)xbox\.com$/.test(location.hostname)) return output('NOT_XBOX');
  const videos = Array.from(document.querySelectorAll('video')).filter(v => {
    const r = v.getBoundingClientRect();
    const tracks = v.srcObject && typeof v.srcObject.getVideoTracks === 'function' ? v.srcObject.getVideoTracks() : [];
    return tracks.some(t => t.readyState === 'live') && v.readyState >= 2 && !v.paused && v.videoWidth > 0 && r.width > 100 && r.height > 80;
  });
  if (videos.length !== 1) return output('WAIT_VIDEO');
  const video = videos[0];
  // WebView may report zero/stale quality counters for a live MediaStream.
  // Playback time is an independent progress signal; never rely on the counter alone.
  let qualityFrames = 0;
  try { const q = video.getVideoPlaybackQuality && video.getVideoPlaybackQuality(); qualityFrames = q && Number.isFinite(q.totalVideoFrames) ? q.totalVideoFrames : 0; } catch (e) { /* use playback time */ }
  const playbackTime = Number.isFinite(video.currentTime) ? video.currentTime : 0;
  const count = qualityFrames + ':' + playbackTime;
  let state = window.__arabicGameReader;
  if (!state || state.video !== video || state.stream !== video.srcObject) {
    state = window.__arabicGameReader = {video, stream: video.srcObject, count: null, canvas: document.createElement('canvas')};
  }
  // Some WebViews freeze both counters for a live MediaStream. Probe at a bounded rate instead of stalling forever.
  const capturedAt = performance.now();
  if (state.count === count && capturedAt - (state.capturedAt || 0) < 750) return output('WAIT_FRAME');
  try {
    // Crop before transferring a frame to Android: subtitle area only, no entire-screen copy.
    const mode = window.__arabicRegion || 'subtitles';
    const rect = mode === 'phoneRight' ? [.55,.08,.44,.89] : mode === 'phoneLeft' ? [.01,.08,.44,.89] : mode === 'all' ? [0,0,1,1] : mode === 'left' ? [0,0,.65,1] : mode === 'right' ? [.35,0,.65,1] : [.10,.62,.80,.35];
    const sx = Math.floor(video.videoWidth * rect[0]), sy = Math.floor(video.videoHeight * rect[1]);
    const sw = Math.max(1, Math.floor(video.videoWidth * rect[2])), sh = Math.max(1, Math.floor(video.videoHeight * rect[3]));
    const scale = Math.min(1, (mode === 'subtitles' ? 1000 : 1280) / sw);
    const canvas = state.canvas, w = Math.round(sw * scale), h = Math.round(sh * scale);
    if (canvas.width !== w) canvas.width = w;
    if (canvas.height !== h) canvas.height = h;
    canvas.getContext('2d').drawImage(video, sx, sy, sw, sh, 0, 0, w, h);
    const image = canvas.toDataURL('image/jpeg', .80);
    state.count = count;state.capturedAt = capturedAt;
    return output('FRAME', {image});
  } catch (e) { return output('CAPTURE_BLOCKED'); }
})();
