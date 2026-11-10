# M3 — Hasil & Bukti

Tanggal: 2026-11-11 (dikerjakan otomatis semalam) • Status: **SELESAI** dengan catatan jujur (bagian bawah)

## Yang dibangun

| Fitur | File | Keterangan |
|---|---|---|
| Engine filter ABP subset (C# murni) | `src/Flit/Adblock/FilterEngine.cs` | `||domain^` → Dictionary O(1) per domain (+domain induk), `@@` pengecualian, `$`opsi (tipe, third/first-party, important), `|prefix`, cosmetic `##` global + per-domain + `#@#` unhide |
| Unduh/cache/update daftar | `src/Flit/Adblock/AdblockStore.cs` | ETag/If-None-Match, cache `%LOCALAPPDATA%\Flit\filters\`, parse di background thread → atomic swap, refresh 24 jam, gagal unduh **tidak** mengosongkan blokir |
| Pintasan request di WebView2 | `MainForm.cs` (WebResourceRequested) | blokir = respons kosong 200; counter status bar |
| Cosmetic (sembunyikan elemen) | `AdblockStore.BuildCosmeticScripts` + handler `DOMContentLoaded` | selektor `##` → `<style data-flit>` per dokumen |
| Riwayat | `Stores.cs` (HistoryStore) + `Dialogs.cs` (HistoryForm) | JSONL append, jendela Ctrl+H: filter + hapus semua, klik-dua-kali buka URL |
| Bookmark | `Stores.cs` (BookmarksStore) + menu Bookmark | bintang di toolbar (Ctrl+D), menu dibangun ulang saat dibuka |
| Temukan di halaman | `MainForm.cs` find bar | Ctrl+F, ‹›, Enter/Shift+Enter, hitung jumlah, Esc tutup |
| Simpan halaman | `MainForm.cs` | Ctrl+S → MHTML (CDP `Page.captureSnapshot`), print→PDF |
| Unduhan | `MainForm.cs` (DownloadStarting) | otomatis ke `Downloads\Flit`, nama unik, status di status bar |
| Toggle adblock | menu | on/off runtime |

## Bukti uji (runtime nyata, bukan klaim)

```
adblock.fetched  easylist     2.031.794 byte
adblock.fetched  easyprivacy  1.515.983 byte
adblock.fetched  abpindo        314.809 byte
adblock.swap     NetRules: 101.545   CosmeticRules: 30.163

detik.com   →  BLOKIR-SETELAH-DETIK: 30   (request diblokir network layer)
detik.com   →  document.querySelectorAll('style[data-flit]').length === 181   (cosmetic terpasang)
```

Skrip uji: `scripts/test-adblock.ps1` (menjalankan `Flit.exe --mcp`, navigasi detik.com via MCP, baca counter + hitung style).

## Bug yang ditemukan & diperbaiki selama M3

1. **`AddScriptToExecuteOnDocumentCreatedAsync` gagal senyap** untuk script cosmetic besar (~1 MB): ter-log sukses tapi tidak pernah dieksekusi (style = 0), dan tetap gagal setelah dipecah ~300 KB. CSP & DOM disingkirkan lewat probe (`flit_evaluate_js`: style manual masuk, tidak ada meta CSP). **Solusi:** pindah ke `DOMContentLoaded` + `ExecuteScriptAsync` per pecahan (~300 KB, 4 bagian) — mekanisme yang sama dengan MCP evaluate yang terbukti jalan headless. Hasil: 181 style terpasang.
2. `DownloadOperation.ResultFilePath` read-only → pakai `DownloadStartingEventArgs.ResultFilePath` (settable).
3. Opsi `$` tak dikenal → **aturan dilewati seluruhnya** (bukan diabaikan separuh) agar tidak salah blok.

## Batasan v1 — JUJUR, belum ada

- ❌ Opsi `$domain=`, `$redirect=`, `$csp=`, `$replace=` → aturan dilewati (minoritas aturan daftar populer)
- ❌ Scriptlet `#$#`, procedural `#?#`, selektor regex `/…/` → dilewati
- ❌ Aturan network generik tanpa anchor `||`/`|` → dilewati (menghindari salah blok massal)
- ❌ Cosmetic per-domain dengan pengecualian `~domain` → dilewati
- ⚠️ Cosmetic baru terpasang saat `DOMContentLoaded` — iklan bisa "berkedip" sebentar sebelum tersembunyi; elemen yang muncul belakangan (SPA/lazy) tidak dikejar (belum MutationObserver)
- ⚠️ Request dari service worker tidak dijamin tersentak (keterbatasan `WebResourceRequested`)
- ❌ Konfirmasi aksi sensitif (tutup tab via MCP, hapus riwayat) → **digeser ke M4** sesuai peta jalan yang direvisi
- Belum: daftar filter kustom milik user, sync, whitelist per-situs ( ada toggle global saja )

## Lisensi

Atribusi EasyList/EasyPrivacy/ABPindo (CC BY-SA 3.0) terpasang di [NOTICES.md](NOTICES.md) **dan** menu Tentang. Tidak ada kode GPL dibundel.
