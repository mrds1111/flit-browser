# Flit MCP Server — Kontrol Browser untuk AI Agent (M2, diperluas M4)

Flit punya server **MCP (Model Context Protocol)** bawaan lewat mode `--mcp`.
Agent (Claude, dll.) menjalankan `Flit.exe --mcp` sebagai subprocess dan bicara
JSON-RPC 2.0 newline-delimited lewat **stdio** — jendela **tidak pernah tampil** (headless).

## Setup (contoh Claude Code)

```bash
claude mcp add flit -- C:\path\ke\Flit.exe --mcp
```

Atau di konfigurasi klien MCP lain (mis. Claude Desktop `claude_desktop_config.json`):

```json
{
  "mcpServers": {
    "flit": {
      "command": "C:\\path\\ke\\Flit.exe",
      "args": ["--mcp"]
    }
  }
}
```

Server keluar bersih saat klien menutup stdin.

## Tools

| Tool | Argumen | Fungsi |
|---|---|---|
| `flit_list_tabs` | — | Semua tab: index, judul, URL, loading/tidur, aktif |
| `flit_new_tab` | `url?`, `activate?=false` | Tab baru (default tab latar) |
| `flit_close_tab` | `index` | Tutup tab |
| `flit_activate_tab` | `index` | Jadikan tab depan |
| `flit_navigate` | `url`, `tabIndex?` | Navigasi (URL atau kata kunci → DuckDuckGo); menunggu load ≤30 dtk |
| `flit_get_page_text` | `tabIndex?`, `maxChars?` | Teks halaman (`innerText`) — cara paling hemat membaca |
| `flit_snapshot` | `tabIndex?`, `maxNodes?` | **Snapshot aksesibilitas (AX tree) via CDP** — pohon role/name/value |
| `flit_screenshot` | `tabIndex?`, `path?` | PNG halaman → disimpan ke file (dipakai flit_snapshot bila gagal) |
| `flit_find` | `query`, `tabIndex?` | Cari teks; laporkan jumlah + sorot pertama |
| `flit_evaluate_js` | `script`, `tabIndex?`, `confirm?` | Jalankan JS — **nonaktif secara default** (lihat keamanan) |
| `flit_status` | — | Versi, tab, RAM, izin aktif |

Prinsip a11y-first (FULL-FINAL §2): baca halaman dengan `flit_snapshot` /
`flit_get_page_text` dulu — bukan screenshot — lalu bertindak.

## Keamanan agent (default)

1. **Izin per-domain** — tanpa konfigurasi, semua domain diizinkan (dipakai pribadi).
   Set `agent.allowedDomains` untuk mode whitelist: domain lain **ditolak** untuk
   `flit_navigate`, `flit_new_tab`, dan `flit_evaluate_js`. Suffix match:
   `example.com` mencakup `sub.example.com`.
2. **`flit_evaluate_js` nonaktif default** — butuh `--allow-evaluate` atau
   `agent.allowEvaluate: true`. Jalankan JS = risiko tertinggi, jadi gerbang dobel
   (flag + izin domain).
3. **Log aktivitas** — setiap panggilan tool dicatat ke
   `%LOCALAPPDATA%\Flit\logs\agent-YYYYMMDD.jsonl` (JSONL: `ts`, `action`, `detail`),
   termasuk upaya yang **ditolak**.
4. **Gerbang konfirmasi (M4, opsional)** — dengan `--require-confirm`, tool destruktif
   (`flit_close_tab`, `flit_evaluate_js`) menolak panggilan tanpa `confirm: true`.
   Default mati agar agent lancar bekerja; nyalkan saat browser dipakai bersama data penting.

### config.json

`%LOCALAPPDATA%\Flit\config.json` (dibaca saat start; tidak pernah ditimpa Flit):

```json
{
  "agent": {
    "allowedDomains": ["example.com", "wikipedia.org"],
    "allowEvaluate": false,
    "userAgent": "",
    "language": "",
    "timezone": "",
    "platform": "",
    "hardwareConcurrency": 8,
    "deviceMemory": 8,
    "proxy": "",
    "profile": "",
    "requireConfirm": false,
    "adblockEnabled": true
  }
}
```

### Override CLI (menang atas config.json)

| Flag | Arti |
|---|---|
| `--mcp` | Mode server MCP headless |
| `--allow-domain=<d>` | Tambah domain ke whitelist (boleh berulang) |
| `--allow-evaluate` | Nyalakan flit_evaluate_js |
| `--ua=<string>` | Override User-Agent (S1) — memicu identitas koheren S2 |
| `--lang=<id-ID>` | Bahasa engine (S1) |
| `--timezone=<IANA>` | Zona waktu JS (`Asia/Jakarta`, `Europe/Paris`, …) (S2) |
| `--platform=<str>` | Paksa `navigator.platform`, mis. `MacIntel` (S2; default diturunkan dari UA) |
| `--proxy=<url>` | Proxy Chromium, mis. `http://127.0.0.1:8888` (M4) |
| `--profile=<nama>` | Profil data terpisah: cookie/riwayat/cache di `Flit\profiles\<nama>` (M4) |
| `--require-confirm` | Tool destruktif wajib `confirm:true` (M4) |
| `--deep-after=N` | Tab tidur > N detik → WebView2 dibuang hemat RAM (default 300; `0` mati). Tab disentuh = dimuat ulang otomatis; `flit_list_tabs` menandai `isDiscarded` |
| `--sleep-after=N` | Tab latar tidur setelah N detik (default 60) |
| `URL...` | URL yang dibuka sebagai tab awal |

## Stealth S1+S2 (aktif)

**S1** — engine diluncurkan dengan `--disable-blink-features=AutomationControlled`
→ `navigator.webdriver === false` (terverifikasi uji M2). UA & bahasa bisa
dioverride per-instansi lewat flag/config.

**S2 (M4)** — begitu `--ua` dipasang, Flit menyuntikkan script kecil ke setiap
dokumen agar seluruh jejak JS jadi **satu cerita**: `navigator.platform` /
`userAgentData` diturunkan dari UA (UA Macintosh → `MacIntel`/`macOS`), bahasa
(`navigator.language`/`languages`), zona waktu (`Intl` + `Date.getTimezoneOffset`),
`hardwareConcurrency`, `deviceMemory`. Tanpa `--ua`, identitas native WebView2
dibiarkan utuh (jangan menambah jejak). Terverifikasi uji M4: UA Mac + `ja-JP` +
`Europe/Paris` → platform `MacIntel`, offset `-60`, semua medan konsisten.

**Identitas per-instansi** = gabungkan flag: `--profile=kerja --ua=... --lang=... --timezone=...`
menghasilkan isolasi penuh (data + identitas). Proxy auth `user:pass` **belum**
didukung argumen ini — jalankan forwarder lokal tanpa auth bila perlu (batasan Chromium `--proxy-server`).

## Catatan teknis

- Satu proses Flit = satu server MCP + seluruh tab (WebView2 shared environment).
- Tab tidur (`TrySuspend`) dibangunkan otomatis saat agent menyentuhnya (`Resume`).
- Screenshot di host tersembunyi memakai varian CDP `fromSurface:false` +
  batas waktu 10 dtk per varian — kalau engine tak mengirim frame, tool
  mengembalikan error (bukan menggantung) dan menyarankan snapshot/teks.
- Respons tool berupa teks JSON di dalam `content[0].text`.
- Halaman internal `edge://*` diblokir WebView2 (ERR_INVALID_URL) — bukan keterbatasan Flit.
