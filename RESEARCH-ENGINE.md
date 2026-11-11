# Riset: jalur "extension .crx beneran" — perbandingan engine/shell (2026-11-11)

Dipicu keputusan pemilik: **riset dulu sebelum memutuskan** (bukan langsung fork).
Pertanyaan inti: *apakah ada jalur yang memuat extension Chrome asli tanpa fork
engine sendiri — dan berapa biaya sebenarnya?*

## Hasil riset (semua status terverifikasi Nov 2026)

| Jalur | Stack | Extension .crx | Status & sumber |
|---|---|---|---|
| **WebView2** (sekarang) | C#/WinForms ✓ | ❌ **Tidak ada** | Terbukti uji M6 (argumen lolos, engine menolak); isu terbuka bertahun-tahun (WebView2Feedback #98/#191/#3258) |
| **CefSharp / CEF** | C#/WinForms ✓ | ❌ **Praktis mati** | Hanya 4 dari 70+ API Chrome yang diimplementasi (utk PDF viewer); "further work is discontinued"; extension tak persisten — [diskusi #4937](https://github.com/cefsharp/CefSharp/discussions/4937), [issue #5067](https://github.com/cefsharp/CefSharp/issues/5067), [perbandingan dev.to](https://dev.to/dotnetbrowser/chrome-extensions-in-net-web-view-controls-11lm) |
| **DotNetBrowser** | C#/WinForms ✓ (WinForms, WPF, Avalonia) | ✓ **Jalan — fitur shipping** | v4.4.1 (Chromium 157): install dari **Chrome Web Store** atau file CRX, popup, badge, izin — [teamdev.com/dotnetbrowser](https://teamdev.com/dotnetbrowser); uBlock & AdBlock terverifikasi vendor ([artikel](https://dev.to/dotnetbrowser/chrome-extensions-in-net-web-view-controls-11lm), catat: ditulis vendor) |
| **EO.WebBrowser** | C#/WinForms ✓ | ❌ **Tidak ada** | Vendor konfirmasi "chrome extension is not supported" (forum EO 2016, tak berubah sampai kini) — [forum](https://www.essentialobjects.com/forum/postst9802_Chrome-Extensions-to-WebBrowser.aspx), [perbandingan TeamDev 2025](https://teamdev.com/dotnetbrowser/blog/dotnetbrowser-or-eo-webbrowser) |
| **NW.js** | JS/TS | ~ **Bukan extension sungguhan** | Dukungannya Chrome *Apps* (manifest lama), MV3 masih isu terbuka sejak 2022 — [issue #7841](https://github.com/nwjs/nw.js/issues/7841) |
| **Qt WebEngine** | C++/Qt | ❌ **Tidak diekspos** | Sistem extension tak dibuka sama sekali; qutebrowser menunggu bertahun-tahun — [Qt Forum](https://forum.qt.io/topic/103558/how-to-make-qwebengineview-support-chrome-extension), [Reddit qutebrowser](https://www.reddit.com/r/qutebrowser/comments/j3fkjl/unable_to_install_extensions_from_chrome_web_store) |
| **Electron + electron-chrome-extensions** | JS/TS (rewrite total) | ~ **Parsial, ekosistem paling hidup** | browser-shell: tabbed browser + loader CRX + installer Web Store; **MV3 service worker: PR upstream #44411 MERGED 31 Jan 2025** (Electron 35+) — [browser-shell](https://github.com/samuelmaddock/electron-browser-shell), [PR](https://github.com/electron/electron/pull/44411); API chrome.* penuh masih "Eventually" |
| **Fork Gecko/Chromium sendiri** | apa saja | ✓ penuh | Puluhan GB source, patch security selamanya — realistis hanya dengan tim (Camoufox/Mercury punya tim) |

## Biaya tiap jalur yang masuk akal (jujur, tanpa menghaluskan)

### A. Tetap WebView2 — extension ditutup selamanya
- Biaya: nol. Userscript + fitur native jadi jawaban (sudah jalan).
- Kehilangan: seluruh ekosistem extension (password manager, dark reader, dll
  kelas berat — userscript cuma menggantikan kelas ringan).

### B. DotNetBrowser — C# tetap, extension jalan, tapi ada harga
- Shell C#/WinForms **tidak berubah stack** (terkunci tetap terpenuhi).
- Pekerjaan: ganti hosting WebView2 → DotNetBrowser (API berbeda tapi
  setara — tab/event/injeksi terpusat di EnsureWebAsync); adblock (C#),
  MCP, stealth, stores semua **ikut tanpa port**. Estimasi: hari, bukan minggu.
- **Lisensi**: perpetual — Indie **$1.429**, Project dari **$4.299**
  ([licensing](https://teamdev.com/dotnetbrowser/docs/guides/licensing));
  ada **lisensi gratis open-source** (di-bind ke nama tipe proyek — kriteria
  perlu dikonfirmasi langsung ke TeamDev; proyek MPL-2.0 publik kandidat).
- Risiko: dependensi proprietary (tercakup di NOTICES.md; kode kita tetap MPL);
  bundle membawa Chromium sendiri (~+150 MB installer vs WebView2 evergreen
  yang sudah ada di Windows); RAM baseline setara (sama-sama Chromium).
- **Bisa divalidasi murah**: lisensi evaluasi 30 hari — bangun spike
  Flit-on-DotNetBrowser + pasang uBlock asli = keputusan berbasis kenyataan.

### C. Electron + electron-chrome-extensions — ekosistem paling hidup, harga terbesar
- Rewrite total shell ke JS/TS: adblock (C#), MCP server, stores, stealth —
  semua port ulang. Stack C#/WinForms terkunci **dilanggar**.
- Baseline RAM & installer membengkak (Chromium+Node) — melawan identitas
  inti "ringan" yang memotivasi proyek ini.
- Dukungan extension masih testbed (API management UI belum ada) —
  meski MV3 sudah di-upstream.

## Rekomendasi

**Riset lengkap (8 jalur dicek, 2026-11-11):** DotNetBrowser satu-satunya yang memenuhi dua
syarat sekaligus — extension .crx beneran **dan** stack C#/WinForms tetap. Yang lain selalu
memilih salah satu: extension tapi pindah ke JS (Electron/NW.js), atau C# tapi tanpa
extension (WebView2/CefSharp/EO.WebBrowser/Qt WebEngine), atau kerjaan tim (fork).

**Spike DotNetBrowser 30 hari** (paket evaluasi) bila extension adalah syarat
mutlak: satu spike kecil — shell minimal + uBlock asli + satu situs berat —
sebelum keputusan engine. Kalau spike gagal/kurang → jalur A dengan hati tenang
(userscript + S3 stealth), fork tetap jadi eskalasi terakhir. Jika spike sukses →
ajukan lisensi OSS ke TeamDev (fallback: beli Indie).

Electron hanya masuk akal bila kamu mau meninggalkan C# — riset ini menemukan
tidak ada alasan kuat membayar biaya itu selama DotNetBrowser lulus spike.

## Catatan latar

- Google mulai **membatasi `--load-extension`** di build Chrome branded ([blog Juni 2025](https://developer.chrome.com/blog/extension-news-june-2025)) — memukul hack ala CEF, tidak memukul DotNetBrowser (install via store/CRX lewat bridge sendiri).
- Sumber utama: tautan di tabel + [Stack Overflow lisensi OSS](https://stackoverflow.com/questions/48067839/how-to-install-open-source-license-dotnetbrowser).
