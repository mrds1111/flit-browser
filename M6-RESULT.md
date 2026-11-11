# M6 — Hasil & Bukti (pasca-peta jalan)

Tanggal: 2026-11-11 • Status: **SELESAI** • Tema: quick wins + sesi & settings + UX tab + userscripts

## Yang dibangun

**Quick wins (menu, shortcut, settings engine):**
- `Ctrl+P` Print dialog (`ShowPrintUI`), *Save as PDF*, `Ctrl+S` simpan halaman MHTML,
  `Ctrl+F` find-in-page, `Ctrl+D` bookmark, `Ctrl+H` history.
- *Mute tab* (`CoreWebView2.IsMuted`), *Pin tab* (tab pinned tak pernah tidur/dibuang),
  *Clear browsing data* (`ClearBrowsingDataAsync(AllProfile)` + hapus history/session).
- **Settings dialog** (Menu → Settings…): adblock on/off, restore sesi on/off,
  ambang tidur (`sleepAfterSec`) & deep-discard (`deepAfterSec`) — tersimpan ke
  `config.json` dan diterapkan live.
- Settings engine per-halaman: simpan password (`IsPasswordAutosaveEnabled`),
  autofill (`IsGeneralAutofillEnabled`), DevTools, zoom control.
- `About Flit` + status RAM kini menyertakan penghitung adblock.

**Sesi & settings:**
- **Restore sesi**: tab yang terbuka saat browser ditutup disimpan ke
  `%LOCALAPPDATA%\Flit\session.json` dan dibuka kembali saat start berikutnya
  (tab aktif terakhir ikut). `--no-restore` / settings untuk mematikan.
- Bahasa UI **berubah ke Inggris** (permintaan pemilik): menu, status, dialog
  history/settings, new tab page semuanya English.

**UX tab & klik-kanan (bug fix):**
- **Bug label adblock diperbaiki**: label lama dihitung saat menu dibangun (sebelum
  engine termuat → selalu "nonaktif") dan logika kliknya terbalik. Kini ada
  `AdblockStore.Enabled` + label dirender ulang saat diklik & saat engine siap:
  `Adblock: on (loading…)` / `on` / `off`.
- **Bug fatal menu ditemukan & diperbaiki** (laporan pemilik: "menu semuanya gak
  bisa dipencet"): helper `Item()` menerima `onClick` tapi **tak pernah memasangnya**
  ke event Click — seluruh item menu mati sejak commit awal. Diperbaiki
  (`it.Click += onClick`) dan diverifikasi dengan interaksi nyata (UI Automation +
  klik mouse sungguhan + screenshot):
  - klik "New tab" → judul jendela berubah `"New Tab — Flit"` ✓
  - klik "Settings…" → dialog Settings terbuka (tertangkap screenshot: tema gelap,
    English, nilai 60/300 benar) ✓
  - efek klik "Pin tab" ikut terlihat di status bar ("Tab pinned (won't sleep/discard)") ✓

**Userscript (jawaban atas "harus ada extension"):**
- Terverifikasi empiris: WebView2 **tidak mendukung** extension Chromium. Detail di bawah.
- Pengganti praktis: **userscript** ala Tampermonkey — file `.js` di
  `%LOCALAPPDATA%\Flit\userscripts\` dengan header `// @match host`, disuntikkan
  pada DOMContentLoaded per-situs, dibaca ulang dari disk tiap navigasi (edit
  langsung berlaku tanpa restart). Menu → *Userscripts folder* membuka foldernya.

## Bukti uji

**Regresi penuh (build Release, semua hijau):**

```
test-mcp.ps1    INIT version=0.6.0 milestone=M6; navigate/text/webdriver=false/
                find/shot/tabs/status/exit(0)               → MCP-TEST-DONE
test-adblock.ps1  detik.com → "blocked 33" (counter naik, regex tes ikut diperbaiki)
                cosmetic styles = 181                        → ADBLOCK-TEST-DONE
test-m5.ps1     SEBELUM isDiscarded=false → SESUDAH true
                RAM private 211 → 181 MB (tab dibuang)
                disentuh → konten benar + isDiscarded=false lagi  → M5-TEST-DONE
```

**Sesi save/restore (GUI runtime nyata):**

```
1. session.json pra-tulis: tabs=[google.com, example.com], active=1
2. Luncurkan Flit (mode jendela) → judul jendela "Example Domain — Flit"
   ← tab AKTIF = index 1 (example.com), bukan google → restore bekerja
3. Tutup rapi (CloseMainWindow) → session.json tertulis ulang:
   "saved":"2026-11-11 08.21.47", 2 tab dengan judul live ("Google",
   "Example Domain") + URL ternormalisasi dari WebView2
4. Exit bersih, proses lenyap
```

Catatan: penyimpanan sesi kini mencakup semua penutupan rapi (tombol X, Alt+F4,
End Task dari Task Manager) — hanya WindowsShutDown yang dilewati.

**Userscript (MCP runtime nyata):**

```
probe.js: // @match example.com → set attribute data-flit-us di documentElement

flit_navigate example.com → evaluate getAttribute('data-flit-us') → "1"   ← JALAN
flit_navigate example.org → evaluate getAttribute('data-flit-us') → null  ← @match MENAHAN
```

**Extension — investigasi lengkap (kesimpulan jujur):**

| Langkah | Hasil |
|---|---|
| Extension probe MV3 (manifest + content script) disiapkan | ✓ |
| `--extension-dir` → argumen `--load-extension` | ✓ |
| Argumen sampai ke proses Chromium? (Win32_Process CommandLine pada msedgewebview2 profil uji) | **YA** — `load-extension="C:\...\flit-ext-probe"` ada di cmdline |
| Content script dieksekusi? (attribute probe di example.com) | **TIDAK** — `null` |

Kesimpulan: WebView2 meneruskan argumennya tapi **engine menolak memuat
extension** (fitur lama yang belum ada di WebView2 — bukan keterbatasan Flit).
Klaim awal CONCEPT.md "WebView2 mendukung extension" terbukti salah; sudah
dikoreksi di README + MCP.md. Jalur pengganti yang benar-benar jalan:
**adblock bawaan** (M3) + **userscript** (M6) + fitur native (bookmark,
history, find, PDF, MHTML).

## Regresi & pemeliharaan skrip uji

- `test-m5.ps1`: regex pembaca RAM/isDiscarded diperbaiki (serializer MCP
  meng-escape kutip sebagai `\u2022`/`\u0022` — pola lama tak pernah match;
  isDiscarded kini di-anchor ke URL tab uji, bukan tab pertama).
- `test-adblock.ps1`: regex `blokir (\d+)` → `blocked (\d+)` (label UI English).
- Uji GUI kini memakai UI Automation + klik mouse + screenshot (bukan cuma
  inspeksi kode) — berkat laporan pemilik soal menu mati.

## Jujur: yang belum

- Ekstensi .crx/Edge Add-ons: tidak bisa di engine ini (lihat investigasi).
- CDP broker `CallDevToolsProtocolMethodAsync` belum diekspos sebagai tool MCP.
- Settings dialog sengaja ringkas: identitas (UA/timezone/proxy/profil) tetap
  via CLI flag — mengubahnya live butuh recreate environment.
