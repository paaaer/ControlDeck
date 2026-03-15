#include "webui.h"
#include <WiFi.h>
#include <WiFiManager.h>

WebUI::WebUI(SliderManager& sliders)
    : _server(WEBUI_PORT), _sliders(sliders) {}

void WebUI::begin() {
    _server.on("/",                   [this]() { handleRoot();        });
    _server.on("/values",             [this]() { handleValues();      });
    _server.on("/cal/min",            [this]() { handleCalMin();      });
    _server.on("/cal/max",            [this]() { handleCalMax();      });
    _server.on("/cal/reset",          [this]() { handleCalReset();    });
    _server.on("/cal/save",           [this]() { handleCalSave();     });
    _server.on("/slider/invert",      [this]() { handleInvert();      });
    _server.on("/slider/rename",      [this]() { handleRename();      });
    _server.on("/settings",           [this]() { handleSettings();    });
    _server.on("/settings/save",      [this]() { handleSettingsSave();});
    _server.on("/settings/pollrate",  [this]() { handlePollRate();    });
    _server.on("/settings/wifi",      [this]() { handleWifiChange();  });
    _server.on("/settings/wifireset", [this]() { handleWifiReset();   });
    _server.begin();
    Serial.printf("[WebUI] Started on port %d\n", WEBUI_PORT);
}

void WebUI::handle() { _server.handleClient(); }

// ---------------------------------------------------------------------------
// /values — includes names now
// ---------------------------------------------------------------------------
void WebUI::handleValues() {
    String json = "{";
    json += "\"raw\":[";
    for (uint8_t i = 0; i < _sliders.count(); i++) { if (i) json+=","; json+=_sliders.getRaw(i); }
    json += "],\"norm\":[";
    for (uint8_t i = 0; i < _sliders.count(); i++) { if (i) json+=","; json+=String(_sliders.getNormalized(i),3); }
    json += "],\"cal\":[";
    for (uint8_t i = 0; i < _sliders.count(); i++) {
        if (i) json+=",";
        auto c = _sliders.getCalibration(i);
        json += "{\"min\":"+String(c.rawMin)+",\"max\":"+String(c.rawMax)+"}";
    }
    json += "],\"names\":[";
    for (uint8_t i = 0; i < _sliders.count(); i++) {
        if (i) json+=",";
        json += "\"" + _sliders.getName(i) + "\"";
    }
    json += "],\"invert\":[";
    for (uint8_t i = 0; i < _sliders.count(); i++) {
        if (i) json+=",";
        json += _sliders.getInvert(i) ? "true" : "false";
    }
    json += "],\"poll_ms\":" + String(g_sendIntervalMs);
    json += ",\"version\":\"" FIRMWARE_VERSION "\"";
    json += ",\"sliders\":" + String(_sliders.count()) + "}";
    _server.sendHeader("Access-Control-Allow-Origin","*");
    _server.send(200,"application/json",json);
}

void WebUI::handleCalMin() {
    if (!_server.hasArg("s")) { _server.send(400,"text/plain","missing s"); return; }
    uint8_t idx=(uint8_t)_server.arg("s").toInt();
    if (idx>=_sliders.count()) { _server.send(400,"text/plain","bad index"); return; }
    _sliders.setCalMin(idx,_sliders.getRaw(idx));
    _server.send(200,"application/json","{\"ok\":true}");
}

void WebUI::handleCalMax() {
    if (!_server.hasArg("s")) { _server.send(400,"text/plain","missing s"); return; }
    uint8_t idx=(uint8_t)_server.arg("s").toInt();
    if (idx>=_sliders.count()) { _server.send(400,"text/plain","bad index"); return; }
    _sliders.setCalMax(idx,_sliders.getRaw(idx));
    _server.send(200,"application/json","{\"ok\":true}");
}

void WebUI::handleCalSave() {
    _sliders.saveCalibration();
    _server.send(200,"application/json","{\"ok\":true}");
}

void WebUI::handleCalReset() {
    _sliders.resetCalibration();
    _server.send(200,"application/json","{\"ok\":true}");
}


// POST /slider/invert?s=0&inv=1  (inv=0 to turn off)
void WebUI::handleInvert() {
    if (!_server.hasArg("s") || !_server.hasArg("inv")) {
        _server.send(400,"application/json","{\"error\":\"missing s or inv\"}"); return;
    }
    uint8_t idx = (uint8_t)_server.arg("s").toInt();
    if (idx >= _sliders.count()) {
        _server.send(400,"application/json","{\"error\":\"bad index\"}"); return;
    }
    bool inv = _server.arg("inv") == "1";
    _sliders.setInvert(idx, inv);
    _server.send(200,"application/json",
        String("{\"ok\":true,\"invert\":") + (inv ? "true" : "false") + "}");
}

// POST /slider/rename?s=0&name=Master
void WebUI::handleRename() {
    if (!_server.hasArg("s") || !_server.hasArg("name")) {
        _server.send(400,"application/json","{\"error\":\"missing s or name\"}"); return;
    }
    uint8_t idx = (uint8_t)_server.arg("s").toInt();
    if (idx >= _sliders.count()) {
        _server.send(400,"application/json","{\"error\":\"bad index\"}"); return;
    }
    String name = _server.arg("name");
    _sliders.setName(idx, name);
    _server.send(200,"application/json",
        "{\"ok\":true,\"name\":\"" + _sliders.getName(idx) + "\"}");
}

void WebUI::handleSettings() {
    _prefs.begin(NVS_NAMESPACE,true);
    String hostname=_prefs.getString("hostname",MDNS_HOSTNAME);
    _prefs.end();
    String json="{";
    json+="\"hostname\":\""+hostname+"\",";
    json+="\"ssid\":\""+WiFi.SSID()+"\",";
    json+="\"ip\":\""+WiFi.localIP().toString()+"\",";
    json+="\"poll_ms\":"+String(g_sendIntervalMs)+",";
    json+="\"poll_min\":"+String(SEND_INTERVAL_MIN_MS)+",";
    json+="\"poll_max\":"+String(SEND_INTERVAL_MAX_MS)+",";
    json+="\"version\":\"" FIRMWARE_VERSION "\",";
    json+="\"sliders\":"+String(_sliders.count());
    json+="}";
    _server.sendHeader("Access-Control-Allow-Origin","*");
    _server.send(200,"application/json",json);
}

void WebUI::handleSettingsSave() {
    if (!_server.hasArg("hostname")) { _server.send(400,"application/json","{\"error\":\"missing hostname\"}"); return; }
    String h=_server.arg("hostname"); h.trim();
    if (h.length()<1||h.length()>32) { _server.send(400,"application/json","{\"error\":\"hostname 1-32 chars\"}"); return; }
    _prefs.begin(NVS_NAMESPACE,false);
    _prefs.putString("hostname",h);
    _prefs.end();
    _server.send(200,"application/json","{\"ok\":true,\"reboot\":true}");
    delay(500); ESP.restart();
}

void WebUI::handlePollRate() {
    if (!_server.hasArg("ms")) { _server.send(400,"application/json","{\"error\":\"missing ms\"}"); return; }
    uint32_t ms=(uint32_t)_server.arg("ms").toInt();
    ms=constrain(ms,(uint32_t)SEND_INTERVAL_MIN_MS,(uint32_t)SEND_INTERVAL_MAX_MS);
    g_sendIntervalMs=ms;
    _prefs.begin(NVS_NAMESPACE,false);
    _prefs.putUInt("poll_ms",ms);
    _prefs.end();
    Serial.printf("[Poll] Rate: %u ms (%u Hz)\n",ms,1000/ms);
    _server.send(200,"application/json",
        "{\"ok\":true,\"ms\":"+String(ms)+",\"hz\":"+String(1000/ms)+"}");
}

void WebUI::handleWifiChange() {
    _server.send(200,"application/json","{\"ok\":true}");
    delay(300); WiFiManager wm; wm.startConfigPortal(DEVICE_NAME); ESP.restart();
}

void WebUI::handleWifiReset() {
    _server.send(200,"application/json","{\"ok\":true}");
    delay(300); WiFiManager wm; wm.resetSettings(); ESP.restart();
}

// ---------------------------------------------------------------------------
// PAGE
// ---------------------------------------------------------------------------
void WebUI::handleRoot() { _server.send(200,"text/html",buildPage()); }

String WebUI::buildPage() {
    String h=R"rawhtml(<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>ControlDeckCore</title>
<style>
*{box-sizing:border-box;margin:0;padding:0}
body{font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',sans-serif;background:#0f0f0f;color:#e0e0e0;padding:24px;max-width:700px;margin:0 auto}
h1{font-size:20px;font-weight:600;color:#fff;margin-bottom:4px}
.sub{font-size:13px;color:#666;margin-bottom:20px}
.tabs{display:flex;gap:4px;margin-bottom:20px;border-bottom:1px solid #2a2a2a}
.tab{padding:8px 20px;font-size:13px;font-weight:500;color:#666;cursor:pointer;border:none;background:none;border-bottom:2px solid transparent;margin-bottom:-1px;transition:color .15s,border-color .15s}
.tab.active{color:#22c47a;border-bottom-color:#22c47a}
.tab:hover:not(.active){color:#aaa}
.panel{display:none}.panel.active{display:block}
.card{background:#1a1a1a;border:1px solid #2a2a2a;border-radius:10px;padding:16px;margin-bottom:12px}
.card-header{display:flex;justify-content:space-between;align-items:center;margin-bottom:12px;gap:8px}
.badge{font-size:11px;background:#2a2a2a;border-radius:4px;padding:2px 8px;color:#888;white-space:nowrap}

/* Inline name editor */
.name-wrap{display:flex;align-items:center;gap:6px;flex:1}
.name-display{font-size:14px;font-weight:600;color:#fff;cursor:pointer;padding:2px 6px;border-radius:4px;border:1px solid transparent;transition:border-color .15s}
.name-display:hover{border-color:#2a2a2a}
.name-input{font-size:14px;font-weight:600;background:#111;border:1px solid #22c47a;border-radius:4px;color:#fff;padding:2px 6px;outline:none;width:160px;display:none}
.name-save{font-size:11px;background:#1a4a30;color:#4dd68c;border:none;border-radius:4px;padding:3px 8px;cursor:pointer;display:none}
.name-cancel{font-size:11px;background:#2a2a2a;color:#888;border:none;border-radius:4px;padding:3px 8px;cursor:pointer;display:none}

.bar-wrap{background:#111;border-radius:6px;height:28px;overflow:hidden;margin-bottom:10px;position:relative}
.bar{height:100%;background:linear-gradient(90deg,#1a6b4a,#22c47a);border-radius:6px;transition:width .08s linear;min-width:2px}
.bar-label{position:absolute;right:8px;top:50%;transform:translateY(-50%);font-size:12px;color:#aaa;font-variant-numeric:tabular-nums}
.vals{display:flex;gap:12px;font-size:12px;color:#555;margin-bottom:10px;flex-wrap:wrap}
.vals span{background:#111;border-radius:4px;padding:3px 8px}.vals span b{color:#aaa}
.cal-row{display:flex;gap:8px}
.btn{flex:1;padding:8px;border:none;border-radius:6px;font-size:13px;font-weight:500;cursor:pointer;transition:opacity .15s}
.btn:active{opacity:.7}
.btn-blue{background:#1e3a5f;color:#7bb8f0}
.btn-green{background:#1a4a30;color:#4dd68c}
.btn-red{background:#3a1a1a;color:#f07070}
.btn-full{width:100%;padding:10px;border:none;border-radius:6px;font-size:14px;font-weight:600;cursor:pointer;margin-top:8px}
.field{margin-bottom:16px}
.field label{display:block;font-size:12px;color:#888;margin-bottom:6px;font-weight:500;text-transform:uppercase;letter-spacing:.05em}
.field input[type=text]{width:100%;background:#111;border:1px solid #2a2a2a;border-radius:6px;padding:9px 12px;color:#e0e0e0;font-size:14px;outline:none;transition:border-color .15s}
.field input[type=text]:focus{border-color:#22c47a}
.field .hint{font-size:11px;color:#555;margin-top:4px}
.poll-wrap{display:flex;align-items:center;gap:14px;margin-bottom:10px}
.poll-wrap input[type=range]{flex:1;accent-color:#22c47a;height:6px;cursor:pointer}
.poll-display{min-width:90px;text-align:right}
.poll-hz{font-size:22px;font-weight:600;color:#22c47a;font-variant-numeric:tabular-nums}
.poll-ms{font-size:12px;color:#555;margin-top:2px}
.info-grid{display:grid;grid-template-columns:auto 1fr;gap:6px 16px;font-size:13px;margin-bottom:16px}
.info-grid .key{color:#555}.info-grid .val{color:#aaa}
.divider{border:none;border-top:1px solid #2a2a2a;margin:16px 0}
.section-title{font-size:12px;font-weight:600;color:#666;text-transform:uppercase;letter-spacing:.05em;margin-bottom:12px}
.wifi-row{display:flex;gap:8px;margin-bottom:8px}
.invert-row{display:flex;align-items:center;gap:8px;margin-top:8px;padding-top:8px;border-top:1px solid #222}
.invert-row label{font-size:12px;color:#888;cursor:pointer;display:flex;align-items:center;gap:6px;user-select:none}
.toggle{position:relative;width:36px;height:20px;flex-shrink:0}
.toggle input{opacity:0;width:0;height:0;position:absolute}
.toggle-bg{position:absolute;inset:0;background:#2a2a2a;border-radius:10px;transition:background .2s;cursor:pointer}
.toggle-bg:before{content:"";position:absolute;width:14px;height:14px;left:3px;top:3px;background:#666;border-radius:50%;transition:transform .2s,background .2s}
.toggle input:checked+.toggle-bg{background:#1a4a30}
.toggle input:checked+.toggle-bg:before{transform:translateX(16px);background:#4dd68c}
.flash{animation:flash .4s ease}
@keyframes flash{0%,100%{opacity:1}50%{opacity:.4}}
.status{font-size:12px;color:#444;text-align:center;margin-top:16px}
.dot{display:inline-block;width:6px;height:6px;border-radius:50%;background:#22c47a;margin-right:6px;animation:pulse 2s infinite}
@keyframes pulse{0%,100%{opacity:1}50%{opacity:.3}}
.toast{position:fixed;bottom:24px;left:50%;transform:translateX(-50%);background:#1a4a30;color:#4dd68c;padding:10px 20px;border-radius:8px;font-size:13px;font-weight:500;opacity:0;transition:opacity .3s;pointer-events:none;white-space:nowrap}
.toast.show{opacity:1}.toast.err{background:#3a1a1a;color:#f07070}
</style>
</head>
<body>
<h1>ControlDeckCore</h1>
<div class="sub"><span class="dot"></span>Firmware )rawhtml";
    h+=FIRMWARE_VERSION;
    h+=R"rawhtml(</div>

<div class="tabs">
  <button class="tab active" onclick="showTab('cal',this)">🎚 Calibration</button>
  <button class="tab"        onclick="showTab('cfg',this)">⚙️ Settings</button>
</div>

<div class="panel active" id="tab-cal">
  <div id="sliders"></div>
  <button class="btn-full btn-green" onclick="calSave()">💾 Save calibration to device</button>
  <button class="btn-full btn-red" onclick="calReset()" style="font-size:13px;padding:8px">↺ Reset all calibration to defaults</button>
  <div class="status" id="status">Connecting...</div>
</div>

<div class="panel" id="tab-cfg">
  <div class="card">
    <div class="section-title">Device info</div>
    <div class="info-grid">
      <span class="key">IP address</span>  <span class="val" id="s-ip">—</span>
      <span class="key">Connected to</span><span class="val" id="s-ssid">—</span>
      <span class="key">Hostname</span>    <span class="val" id="s-host">—</span>
      <span class="key">Firmware</span>   <span class="val">)rawhtml";
    h+=FIRMWARE_VERSION;
    h+=R"rawhtml(</span>
      <span class="key">Sliders</span>    <span class="val" id="s-sliders">—</span>
    </div>
  </div>

  <div class="card">
    <div class="section-title">Polling rate</div>
    <div class="poll-wrap">
      <input type="range" id="poll-slider" min="10" max="100" step="1" value="100" oninput="pollDrag(this.value)">
      <div class="poll-display">
        <div class="poll-hz" id="poll-hz">100 <span style="font-size:13px;color:#555">Hz</span></div>
        <div class="poll-ms" id="poll-ms">10 ms</div>
      </div>
    </div>
    <div class="hint" style="margin-bottom:12px">Left = 10 Hz (100 ms) &mdash; Right = 100 Hz (10 ms). Higher = more responsive.</div>
    <button class="btn-full btn-green" onclick="savePollRate()">💾 Save polling rate</button>
  </div>

  <div class="card">
    <div class="section-title">Hostname</div>
    <div class="field">
      <label>mDNS hostname</label>
      <input type="text" id="hostname" maxlength="32" placeholder="controldeck">
      <div class="hint">Accessible as <b>&lt;hostname&gt;.local</b> — reboot required</div>
    </div>
    <button class="btn-full btn-green" onclick="saveHostname()">💾 Save hostname &amp; reboot</button>
  </div>

  <div class="card">
    <div class="section-title">WiFi connection</div>
    <div class="wifi-row">
      <button class="btn btn-blue" onclick="wifiChange()" style="flex:1;padding:10px">📶 Change WiFi network</button>
    </div>
    <div class="hint" style="margin-bottom:12px">Opens the WiFi portal — connect to the <b>ControlDeckCore</b> AP.</div>
    <hr class="divider">
    <div class="section-title" style="color:#a05050">Reset WiFi</div>
    <button class="btn-full btn-red" onclick="wifiReset()" style="font-size:13px;padding:9px">⚠ Reset WiFi — erase credentials &amp; reboot to AP</button>
    <div class="hint" style="margin-top:6px">Clears saved password. Device reboots to <b>ControlDeckCore</b> AP.</div>
  </div>
</div>

<div class="toast" id="toast"></div>

<script>
function showTab(id,btn){
  document.querySelectorAll('.panel').forEach(p=>p.classList.remove('active'));
  document.querySelectorAll('.tab').forEach(t=>t.classList.remove('active'));
  document.getElementById('tab-'+id).classList.add('active');
  btn.classList.add('active');
  if(id==='cfg') loadSettings();
}
function toast(msg,isErr=false){
  const t=document.getElementById('toast');
  t.textContent=msg; t.className='toast show'+(isErr?' err':'');
  clearTimeout(t._tid); t._tid=setTimeout(()=>t.classList.remove('show'),3000);
}
function flash(id){const el=document.getElementById(id);if(el){el.classList.remove('flash');void el.offsetWidth;el.classList.add('flash');}}

// ── Slider name editing ──────────────────────────────────────
function startRename(i){
  document.getElementById('nd'+i).style.display='none';
  document.getElementById('ni'+i).style.display='inline-block';
  document.getElementById('ns'+i).style.display='inline-block';
  document.getElementById('nc'+i).style.display='inline-block';
  document.getElementById('ni'+i).focus();
  document.getElementById('ni'+i).select();
}
function cancelRename(i){
  document.getElementById('nd'+i).style.display='inline-block';
  document.getElementById('ni'+i).style.display='none';
  document.getElementById('ns'+i).style.display='none';
  document.getElementById('nc'+i).style.display='none';
}
function saveName(i){
  const name=document.getElementById('ni'+i).value.trim()||document.getElementById('nd'+i).textContent;
  fetch('/slider/rename?s='+i+'&name='+encodeURIComponent(name))
    .then(r=>r.json())
    .then(d=>{
      if(d.ok){
        document.getElementById('nd'+i).textContent=d.name;
        cancelRename(i);
        toast('✓ Renamed to "'+d.name+'"');
      } else toast('Rename failed',true);
    });
}

// ── Invert ──────────────────────────────────────────────────
function setInvert(i,inv){
  fetch('/slider/invert?s='+i+'&inv='+(inv?'1':'0'))
    .then(r=>r.json())
    .then(d=>{
      if(d.ok) toast(document.getElementById('nd'+i).textContent+(inv?' — inverted':' — normal'));
      else toast('Error setting invert',true);
    });
}

// ── Calibration ──────────────────────────────────────────────
function setMin(i){fetch('/cal/min?s='+i).then(r=>r.json()).then(()=>{flash('min'+i);toast(document.getElementById('nd'+i).textContent+' MIN set');});}
function setMax(i){fetch('/cal/max?s='+i).then(r=>r.json()).then(()=>{flash('max'+i);toast(document.getElementById('nd'+i).textContent+' MAX set');});}
function calSave(){fetch('/cal/save').then(()=>toast('✓ Calibration saved'));}
function calReset(){
  if(!confirm('Reset all calibration to defaults?')) return;
  fetch('/cal/reset').then(()=>{toast('Calibration reset');location.reload();});
}

let _built=false;
function update(){
  fetch('/values').then(r=>r.json()).then(d=>{
    document.getElementById('status').innerHTML='<span class="dot"></span>Connected &mdash; '+d.sliders+' sliders';
    if(!_built){buildUI(d);_built=true;}
    for(let i=0;i<d.sliders;i++){
      const pct=Math.round(d.norm[i]*100);
      document.getElementById('bar'+i).style.width=pct+'%';
      document.getElementById('pct'+i).textContent=pct+'%';
      document.getElementById('raw'+i).textContent=d.raw[i];
      document.getElementById('calmin'+i).textContent=d.cal[i].min;
      document.getElementById('calmax'+i).textContent=d.cal[i].max;
      // Only update name display if not currently editing
      if(document.getElementById('ni'+i).style.display==='none')
        document.getElementById('nd'+i).textContent=d.names[i];
      // Sync invert checkbox (only if not focused)
      const invEl=document.getElementById('inv'+i);
      if(invEl && document.activeElement!==invEl) invEl.checked=d.invert[i];
    }
  }).catch(()=>{document.getElementById('status').textContent='⚠ Connection lost...';});
}

const pins=[)rawhtml";
    for(uint8_t i=0;i<NUM_SLIDERS;i++){if(i)h+=",";h+=SLIDER_PINS[i];}
    h+=R"rawhtml(];

function buildUI(d){
  const el=document.getElementById('sliders');
  el.innerHTML='';
  for(let i=0;i<d.sliders;i++){
    el.innerHTML+=`
    <div class="card">
      <div class="card-header">
        <div class="name-wrap">
          <span class="name-display" id="nd${i}" title="Click to rename" onclick="startRename(${i})">${d.names[i]}</span>
          <input class="name-input" id="ni${i}" type="text" maxlength="24" value="${d.names[i]}"
                 onkeydown="if(event.key==='Enter')saveName(${i});if(event.key==='Escape')cancelRename(${i})">
          <button class="name-save"   id="ns${i}" onclick="saveName(${i})">✓</button>
          <button class="name-cancel" id="nc${i}" onclick="cancelRename(${i})">✕</button>
        </div>
        <span class="badge">GPIO ${pins[i]}</span>
      </div>
      <div class="bar-wrap">
        <div class="bar" id="bar${i}" style="width:0%"></div>
        <div class="bar-label" id="pct${i}">0%</div>
      </div>
      <div class="vals">
        <span>Raw: <b id="raw${i}">-</b></span>
        <span>Cal min: <b id="calmin${i}">-</b></span>
        <span>Cal max: <b id="calmax${i}">-</b></span>
      </div>
      <div class="cal-row">
        <button class="btn btn-blue" id="min${i}" onclick="setMin(${i})">▼ Set MIN</button>
        <button class="btn btn-blue" id="max${i}" onclick="setMax(${i})">▲ Set MAX</button>
      </div>
      <div class="invert-row">
        <label>
          <span class="toggle">
            <input type="checkbox" id="inv${i}" onchange="setInvert(${i},this.checked)">
            <span class="toggle-bg"></span>
          </span>
          Invert direction (slider mounted upside down)
        </label>
      </div>
    </div>`;
  }
}

// ── Poll rate ────────────────────────────────────────────────
function pollDrag(hz){
  hz=parseInt(hz);
  const ms=Math.round(1000/hz);
  document.getElementById('poll-hz').innerHTML=hz+' <span style="font-size:13px;color:#555">Hz</span>';
  document.getElementById('poll-ms').textContent=ms+' ms';
}
function savePollRate(){
  const hz=parseInt(document.getElementById('poll-slider').value);
  const ms=Math.round(1000/hz);
  fetch('/settings/pollrate?ms='+ms).then(r=>r.json()).then(d=>{
    if(d.ok) toast('✓ Polling rate: '+d.hz+' Hz ('+d.ms+' ms)');
    else toast('Error',true);
  });
}

// ── Settings ─────────────────────────────────────────────────
function loadSettings(){
  fetch('/settings').then(r=>r.json()).then(d=>{
    document.getElementById('s-ip').textContent=d.ip;
    document.getElementById('s-ssid').textContent=d.ssid||'—';
    document.getElementById('s-host').textContent=d.hostname+'.local';
    document.getElementById('s-sliders').textContent=d.sliders;
    document.getElementById('hostname').value=d.hostname;
    const sl=document.getElementById('poll-slider');
    sl.min=10; sl.max=100; sl.step=1;
    const hz=Math.round(1000/d.poll_ms);
    sl.value=hz;
    pollDrag(hz);
  }).catch(()=>toast('Could not load settings',true));
}
function saveHostname(){
  const v=document.getElementById('hostname').value.trim();
  if(!v){toast('Hostname cannot be empty',true);return;}
  if(!/^[a-zA-Z0-9\-]{1,32}$/.test(v)){toast('Letters, numbers and hyphens only',true);return;}
  if(!confirm('Save hostname "'+v+'" and reboot?')) return;
  fetch('/settings/save?hostname='+encodeURIComponent(v))
    .then(r=>r.json()).then(d=>{if(d.ok)toast('✓ Saved — rebooting...');else toast(d.error||'Error',true);})
    .catch(()=>toast('Device rebooting...'));
}
function wifiChange(){
  if(!confirm('Open WiFi portal?\nConnect to the "ControlDeckCore" AP to choose a new network.')) return;
  fetch('/settings/wifi',{method:'POST'}).then(()=>toast('Opening WiFi portal...')).catch(()=>toast('Opening WiFi portal...'));
}
function wifiReset(){
  if(!confirm('Erase WiFi credentials and reboot?\nAre you sure?')) return;
  fetch('/settings/wifireset',{method:'POST'}).then(()=>toast('WiFi reset — connect to ControlDeckCore AP')).catch(()=>toast('WiFi reset...'));
}

setInterval(update,200);
update();
</script>
</body>
</html>)rawhtml";
    return h;
}
