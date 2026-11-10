# M4 — Hasil & Bukti

Tanggal: 2026-11-11 (dikerjakan otomatis semalam) • Status: **SELESAI** (inti M4) + keputusan terdokumentasi untuk backend opsional

## Yang dibangun

| Fitur | File | Keterangan |
|---|---|---|
| Stealth S2 — identitas koheren | `src/Flit/Stealth/Identity.cs` + `AgentConfig.BuildIdentity()` | Begitu `--ua` dipasang: platform/`userAgentData` diturunkan dari UA, bahasa, zona waktu (Intl + `getTimezoneOffset`), cores, deviceMemory — semua satu cerita. Tanpa UA kustom = native utuh |
| Profil terpisah | `--profile=<nama>` | Folder data sendiri `%LOCALAPPDATA%\Flit\profiles\<nama>` (cookie/riwayat/cache) |
| Proxy | `--proxy=<url>` → `--proxy-server` Chromium | Per-instansi |
| Gerbang konfirmasi | `--require-confirm` / `agent.requireConfirm` | `flit_close_tab` & `flit_evaluate_js` wajib `confirm:true` (konfirmasi aksi sensitif, digeser dari M3) |
| Omnibox/skema | `ResolveInput` | `chrome://`, `file://`, `about:` tak lagi dibuang ke pencarian |

## Bukti uji (runtime nyata — skrip `scripts/test-m4.ps1`)

**Fase A — S2 koheren** (`--profile=m4test --lang=ja-JP --timezone=Europe/Paris --ua="Mozilla/5.0 (Macintosh; …)"` di example.com):

```json
{"ua":true, "platform":"MacIntel", "lang":"ja-JP", "langs":"ja-JP,en-US",
 "tz":"Europe/Paris", "off":-60, "cores":8, "mem":8, "webdriver":false}
```

`off:-60` = November Paris (CET, UTC+1) — hitungan zona waktu benar; `platform:MacIntel`
padahal mesin asli Windows — identitas menyatu dengan UA Mac.

**Fase D — profil terpisah**: `localStorage.setItem('flitM4','x1')` → proses dimatikan →
proses baru profil sama → `getItem` = **"x1"** (data persist di folder profil, bukan Default).

**Fase B — gerbang confirm** (`--require-confirm`):

```
flit_evaluate_js "1+1"                  → isError: "Aksi destruktif: tambahkan confirm:true"
flit_evaluate_js "1+1" confirm:true     → {"result":2}
flit_close_tab {index:1}                → isError (ditolak)
flit_close_tab {index:1} confirm:true   → {"closed":1}
```

**Fase C — proxy** (`--proxy=http://127.0.0.1:9`, port mati): halaman web menampilkan
error **Chromium** *"kesalahan dengan server proksi, atau alamat salah"* — sementara semua
uji lain (tanpa proxy) navigasi normal → trafik terbukti dirutekan lewat proxy yang dikonfigurasi.

## Keputusan untuk item "opsional" M4 (dokumen, bukan kode)

| Item | Keputusan | Alasan |
|---|---|---|
| Backend Lightpanda | **Tidak dibangun v1.0** | WebView2 sudah memenuhi mode agent (M2–M4 terbukti); Lightpanda masih pre-alpha. Bila scraping massal situs ringan dibutuhkan nanti: proses terpisah yang mengekspos CDP — Flit cukup menunjuk `--proxy`/port-nya, tanpa ubah inti |
| Sidecar captcha | **Tidak dibangun v1.0** | Butuh API pihak ketiga berbayar + area abu-abu ToS situs; WebView2 nyata (Chromium penuh) jarang tercaptcha untuk penggunaan wajar |
| Eskalasi adblock-rust | **Tidak dibutuhkan saat ini** | Engine C# terbukti: 101.545 aturan, pencocokan O(1) dictionary, detik.com 30 blok + 181 style. Eskalasi HANYA bila terukur ada gap (handler >beberapa ms/request atau daftar besar tersendat) |

## Batasan jujur

- Offset zona waktu dihitung **saat startup** — sesi lintas pergantian DST (jarang, jam 03.00) bisa meleset 1 jam sampai restart.
- `getHighEntropyValues` hanya dipatch medan `platform` — `model`/`uaFullVersion` dari entropi tinggi tidak dipalsukan (v1).
- Proxy auth `user:pass` tidak didukung `--proxy-server` (batasan Chromium) — pakai forwarder lokal.
- Gerbang confirm baru mencakup `flit_close_tab` + `flit_evaluate_js` (dua aksi paling destruktif yang dimiliki agent hari ini).
- `edge://*` internal diblokir WebView2 sendiri (ERR_INVALID_URL) — bukan keterbatasan Flit.

## Regresi

`scripts/test-mcp.ps1` (M2 e2e penuh) tetap hijau setelah perubahan M4: handshake 0.3.0,
tab/teks/webdriver=false/find/screenshot/exit 0. Adblock tetap: `scripts/test-adblock.ps1`
30 blokir + 181 style di detik.com.
