// Live playback of the annotated MediaMTX streams: WebRTC (WHEP) first for
// sub-second latency, HLS as fallback. Mirrors client/index.html.
//
// Base URLs default to the current origin: behind the Caddy HTTPS proxy the
// streams are reachable at /webrtc/* and /hls/* on the same host; over plain
// HTTP (development) MediaMTX is addressed directly on ports 8889/8888.

export function start(video, dotnetRef, streamPath, webrtcBase, hlsBase) {
  if (!webrtcBase)
    webrtcBase = location.protocol === "https:"
      ? `${location.origin}/webrtc`
      : `http://${location.hostname || "localhost"}:8889`;
  if (!hlsBase)
    hlsBase = location.protocol === "https:"
      ? `${location.origin}/hls`
      : `http://${location.hostname || "localhost"}:8888`;

  const encodedPath = streamPath.split("/").map(encodeURIComponent).join("/");
  const state = { stopped: false, pc: null, hls: null, retryTimer: null };

  const setStatus = (kind) => {
    if (!state.stopped)
      dotnetRef.invokeMethodAsync("SetStatus", kind).catch(() => {});
  };

  const play = async () => {
    if (state.stopped) return;
    setStatus("connecting");
    try {
      state.pc = await playWebrtc();
      setStatus("webrtc");
    } catch (err) {
      state.pc?.close();
      state.pc = null;
      if (state.stopped) return;
      console.warn(`[live] WebRTC failed (${err.message}), trying HLS…`);
      try {
        await playHls();
        setStatus("hls");
      } catch (err2) {
        state.hls?.destroy();
        state.hls = null;
        if (state.stopped) return;
        console.warn(`[live] HLS failed (${err2.message}).`);
        setStatus("offline");
        state.retryTimer = setTimeout(play, 10000);
      }
    }
  };

  // --- WebRTC via MediaMTX's WHEP endpoint ---
  async function playWebrtc() {
    const pc = new RTCPeerConnection();
    state.pc = pc;
    pc.addTransceiver("video", { direction: "recvonly" });

    const gotFrames = new Promise((resolve, reject) => {
      // Playback starts at the next keyframe, so allow a generous timeout.
      const timer = setTimeout(() => reject(new Error("timeout")), 15000);
      pc.ontrack = (e) => {
        video.srcObject = e.streams[0];
        video.play().catch(() => {});
        video.onloadeddata = () => { clearTimeout(timer); resolve(); };
      };
      pc.onconnectionstatechange = () => {
        if (pc.connectionState === "failed") { clearTimeout(timer); reject(new Error("ICE failed")); }
      };
    });

    await pc.setLocalDescription(await pc.createOffer());
    await waitForIceGathering(pc);

    const res = await fetch(`${webrtcBase}/${encodedPath}/whep`, {
      method: "POST",
      headers: { "Content-Type": "application/sdp" },
      body: pc.localDescription.sdp,
    });
    if (!res.ok) { pc.close(); throw new Error(`WHEP ${res.status}`); }
    await pc.setRemoteDescription({ type: "answer", sdp: await res.text() });

    try {
      await gotFrames;
    } catch (err) {
      pc.close();
      throw err;
    }
    return pc;
  }

  function waitForIceGathering(pc) {
    if (pc.iceGatheringState === "complete") return Promise.resolve();
    return new Promise((resolve) => {
      const check = () => {
        if (pc.iceGatheringState === "complete") { pc.removeEventListener("icegatheringstatechange", check); resolve(); }
      };
      pc.addEventListener("icegatheringstatechange", check);
      setTimeout(resolve, 2000); // don't wait forever for candidates
    });
  }

  // --- HLS fallback (a few seconds of latency) ---
  function playHls() {
    const url = `${hlsBase}/${encodedPath}/index.m3u8`;
    return new Promise(async (resolve, reject) => {
      if (video.canPlayType("application/vnd.apple.mpegurl")) {
        video.src = url;
        video.onloadeddata = () => { video.play().catch(() => {}); resolve(); };
        video.onerror = () => reject(new Error("native HLS failed"));
        return;
      }
      try {
        if (!window.Hls) await loadScript("https://cdn.jsdelivr.net/npm/hls.js@1/dist/hls.min.js");
        const hls = new Hls({ lowLatencyMode: true });
        state.hls = hls;
        hls.on(Hls.Events.ERROR, (_, data) => { if (data.fatal) { hls.destroy(); reject(new Error(data.type)); } });
        hls.loadSource(url);
        hls.attachMedia(video);
        hls.on(Hls.Events.MANIFEST_PARSED, () => { video.play().catch(() => {}); resolve(); });
      } catch (e) {
        reject(e);
      }
    });
  }

  function loadScript(src) {
    return new Promise((resolve, reject) => {
      const s = document.createElement("script");
      s.src = src;
      s.onload = resolve;
      s.onerror = () => reject(new Error("failed to load hls.js"));
      document.head.appendChild(s);
    });
  }

  play();

  return {
    stop() {
      state.stopped = true;
      clearTimeout(state.retryTimer);
      state.pc?.close();
      state.hls?.destroy();
      video.srcObject = null;
      video.removeAttribute("src");
    },
  };
}
