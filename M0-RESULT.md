# M0 — Hasil Benchmark RAM (Go/No-Go)

> Diukur 2026-11-11. Build Release, .NET 10.0.404, WebView2 Runtime 157.0.4341.42.

## Metodologi

- `Flit.exe` diluncurkan, 1 tab membuka `https://example.com`, profil data segar (`%LOCALAPPDATA%\Flit\M0`), tanpa flag optimasi apa pun (baseline jujur).
- Setelah 25 detik (init + load selesai), dicatat Working Set + Private Memory dari **seluruh pohon proses**: `Flit.exe` + semua `msedgewebview2.exe` yang command line-nya memuat `Flit\M0` (filter user-data-folder — proses WebView2 milik aplikasi lain tidak terhitung).
- Satu kali pengukuran, cold start (run pertama setelah install → cache NuGet/NuGet-profile & WebView2 cold).

## Angka

| Metrik | Nilai |
|---|---|
| Jumlah proses | 7 (1 shell + 6 WebView2) |
| **Working Set total** | 429,2 MB |
| **Private Memory total** | **202,8 MB** |
| Shell WinForms sendiri | 63 MB WS / **15,7 MB private** |
| Proses terbesar (browser/renderer) | 102 MB private |
| Proses lain (GPU, utility, crashpad) | 2,6–38,8 MB private |

## Interpretasi

- **Working Set (429 MB) melebih-lebihkan**: memori shared (DLL WebView2 yang dipakai bersama Edge/aplikasi lain) terhitung ganda di tiap proses. **Private Memory (202,8 MB) = angka pembanding yang jujur** (mendekati "Memory (private working set)" Task Manager yang dijumlahkan).
- Konteks pembanding milik pemilik: Chrome 3 tab ≈ 1,1 GB (±366 MB/tab). Flit baseline **belum dioptimasi sama sekali** dan sudah 202,8 MB untuk 1 tab termuat.
- Target spesifikasi "RAM idle ≤150 MB" mengasumsikan arsitektur sleeping-tab jalan (renderer tab latar dibuang) — itu memang produk inti kita, belum ada di M0.

## Verdict: **GO** ✅

Alasan: shell WinForms terbukti hanya 15,7 MB private (validasi stack); 202,8 MB private untuk 1 tab **tanpa satu pun optimasi** masih di jalur menuju ≤150 MB — dan daftar optimasi di bawah adalah thesis inti produk, bukan tambal sulam.

## Optimasi terjadwal (urut dampak)

1. **TrySuspend/discard tab latar** — inti produk; mengosongkan renderer saat idle (uji M0.1).
2. `--renderer-process-limit=1` via `AdditionalBrowserArguments`.
3. Konsolidasi utility (mis. audio in-process: `--disable-features=AudioServiceOutOfProcess`) — kurangi jumlah proses.
4. Matikan DevTools bila tak dipakai; lazy-load service worker dll.
