# M2 — Hasil: MCP Server + Keamanan Agent + Stealth S1

> Diuji 2026-11-11 (malam), build Release. Semua klaim di bawah punya bukti eksekusi
> langsung (transkrip uji `scripts/test-mcp.ps1` + log aktivitas), bukan asumsi.

## Yang dibangun

- **Server MCP stdio** (`Flit.exe --mcp`): JSON-RPC 2.0 newline-delimited, headless
  (jendela tak pernah tampil), keluar bersih saat klien menutup stdin. Handshake
  `initialize` merespons versi protokol yang dikenal (2024-11-05 / 2025-03-26 / 2025-06-18).
- **11 tools**: list/new/close/activate tab, navigate (tunggu load), get_page_text,
  **snapshot AX-tree via CDP**, screenshot (PNG ke file), find, evaluate_js (gerbang),
  status.
- **Keamanan**: izin per-domain (whitelist suffix-match), `evaluate_js` mati default,
  log aktivitas JSONL per hari (`%LOCALAPPDATA%\Flit\logs\`).
- **Stealth S1 dasar**: `--disable-blink-features=AutomationControlled`,
  override UA (`--ua=`) & bahasa (`--lang=`).

## Bukti uji end-to-end

| Uji | Hasil |
|---|---|
| `initialize` handshake | ✅ protocolVersion + serverInfo "flit 0.2.0" |
| `flit_new_tab` + activate | ✅ index 1, tab aktif |
| `flit_get_page_text` | ✅ teks example.com |
| **`navigator.webdriver`** | ✅ **`false`** (S1 bekerja) |
| `flit_screenshot` (headless) | ✅ PNG 63,9 KB — render penuh (dicek visual) |
| `flit_find` | ✅ bekerja (kasus khusus: example.com sudah ganti desain 2026 — tanpa `<h1>`; teks baru) |
| Deny domain (`--allow-domain=example.com` → github.com) | ✅ ditolak: "Domain tidak diizinkan: github.com" |
| Allow subdomain (sub.example.com) | ✅ lolos + navigasi `loaded:true` |
| `evaluate_js` tanpa flag | ✅ ditolak + pesan cara menyalakan |
| stdin EOF | ✅ exit code 0 |
| Log aktivitas | ✅ JSONL: tool_call/tool_ok/tool_failed/agent.denied terekam semua |

## Bug yang ditemukan & diperbaiki saat pengujian

1. **Screenshot menggantung** di host tersembunyi (compositor tak kirim frame) →
   varian CDP berurutan (`fromSurface:false` dulu) + timeout 10 dtk + error jujur.
2. **Race init vs request pertama** (tool tiba saat tab awal belum selesai dibuat,
   `_active=-1`) → server menunggu `InitializeAsync` tuntas sebelum membaca stdin +
   fallback `ResolveTab`.
3. (Non-bug) example.com 2026 tak punya `<h1>` lagi — query uji disesuaikan.

## Belum ada di M2 (jujur)

- Konfirmasi aksi sensitif (form submit/klik beli) — M2 hanya gerbang evaluate_js.
- Screenshot di jendela yang terlihat (mode windowed) belum diuji eksplisit.
- Per-profil identitas + proxy (S1 penuh) — flag UA/lang sudah ada, profil simpan
  masuk M4.
