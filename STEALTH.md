# Stealth & Anti-Detection — inventaris dan peta jalan

Referensi pola: **Camoufox** (anti-detect browser berbasis Gecko, github.com/daijro/camoufox)
— dipelajari 2026-11-11 atas arahan pemilik. Prinsip yang diadopsi:

1. **Spoof hanya yang mengidentifikasi mesin/jaringan** — sisanya biarkan stock
   (nilai palsu yang tidak wajar justru menjadi fingerprint tersendiri).
2. **Satu cerita**: UA, platform, bahasa, zona waktu, Client Hints, header HTTP —
   semuanya harus konsisten satu sama lain.
3. **Level semestinya**: nilai dijawab serendah mungkin (Camoufox: dari C++;
   Flit: dari CDP/injeksi di batas shell — batasan engine terkunci).

## Yang sudah aktif (terverifikasi runtime)

| Level | Cakupan | Bukti |
|---|---|---|
| **S1** | `navigator.webdriver === false` (`--disable-blink-features=AutomationControlled`) | M2: evaluate di halaman nyata |
| **S2** | Dengan `--ua`: `navigator.userAgent/platform/language/languages/hardwareConcurrency/deviceMemory` + `Intl` zona waktu + `Date.getTimezoneOffset` — satu cerita, terkunci per-instansi | M4: Mac+ja-JP+Paris → `MacIntel`/`-60` di mesin Windows |
| **S2b** (2026-11-11) | `navigator.userAgentData` direplika utuh: `platform`, `brands` + versi dari UA, `mobile`, `getHighEntropyValues` dijawab dari data identitas (platform, platformVersion, architecture) | Menutup 3 kebocoran: platform masih "Windows" (getter prototype gagal ditimpa), brands membocorkan "Microsoft Edge WebView2", platformVersion membocorkan Windows (19.0.0) |
| **S2.5** (2026-11-11) | **Header HTTP** via CDP `Network.setUserAgentOverride`: `User-Agent`, `Accept-Language` (dari `--lang`, q-value dinormalkan Chromium), Client Hints metadata lengkap | httpbin.org/headers: `ja-JP,ja;q=0.9,en;q=0.8` + UA Chrome 131 Mac dari mesin Windows fisik |

Cara pakai (semua flag CLI, tidak aktif tanpa `--ua` — identitas native dibiarkan utuh):

```
Flit.exe --profile=kerja --ua="Mozilla/5.0 (Macintosh; ...) Chrome/131.0.0.0 Safari/537.36" --lang=ja-JP --timezone=Europe/Paris --proxy=http://127.0.0.1:8888
```

## Catatan jujur: efek samping & lubang yang tersisa

- **Sec-Ch-Ua tidak dikirim** setelah override metadata — tidak ada kontradiksi,
  tapi absennya Client Hints utk UA Chrome 131 adalah anomali ringan yang bisa
  dicatat WAF ketat.
- **Worker/Service Worker**: injeksi S2 tidak berjalan di scope worker
  (AddScriptToExecuteOnDocumentCreated hanya dokumen) — `Intl` di worker bocor
  zona waktu asli. Batasan shell WebView2.
- **Canvas/WebGL/Audio rendering**: pixel asli GPU (mengikuti prinsip Camoufox:
  TIDAK diberi noise — canvas noise buatan justru fingerprint ala Tor/LibreWolf).
  Tapi `WebGLRenderingContext.getParameter(VENDOR/RENDERER)` masih nilai GPU
  asli → mengidentifikasi mesin. (→ S3)
- **Font/voice/media-device list**: asli mesin. Di Camoufox ini patch C++
  (`font-hijacker`, `voice-spoofing`) — **mustahil di shell WebView2**, hanya
  bisa di engine sendiri.
- **`toString()` getter**: definisi `defineProperty` kita masih bisa terbaca
  bukan native (`function(){ return val; }`). (→ S3)

## Peta jalan S3 (level shell, masih realistis di WebView2)

| Item | Ide | Prioritas |
|---|---|---|
| WebGL vendor/renderer | override `getParameter` dari config (`--gpu="Apple M1"`), konsisten dgn UA | tinggi — GPU mengidentifikasi mesin |
| Getter hardening | `toString` semua spoof tampil `[native code]` | tinggi — murah |
| WebRTC + proxy | kebijakan IP handling agar ICE tak membocorkan IP asli saat `--proxy` (uji: browserleaks) | sedang |
| GeoIP alignment | ambil lokasi IP proxy saat start → set `--timezone`/`--lang` otomatis (pola `geoip=True` Camoufox) | sedang |
| Screen/viewport | koherensi ukuran dgn identitas (mis. Macbook 1440×900) | rendah |
| Humanize input (agent) | gerakan kursor mirip manusia utk MCP click (pola `humanize=True` Cursory) | rendah — agent headless tak pakai kursor |

## Yang hanya mungkin lewat fork engine (jalur "edisi stealth" Gecko)

Font list, voice list, media devices, codec answers, nilai C++ native
(`toString` = native sempurna), isolated-world automation. Semua terdokumentasi
di FULL-FINAL.md §8 sebagai eskalasi masa depan jika proyek punya kontributor.
