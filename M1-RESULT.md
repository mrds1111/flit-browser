# M1 — Hasil Benchmark Multi-tab + Sleeping Tabs

> Diukur 2026-11-11 (malam). Build Release, .NET 10.0.404, WebView2 Runtime 157.0.4341.42.
> Sama sekali **belum ada** optimasi flag (`--renderer-process-limit` dst.) — baseline arsitektur.

## Metodologi

- 5 tab: example.com • Wikipedia (artikel Indonesia) • GitHub • Hacker News • DuckDuckGo.
- Setiap skenario = proses Flit segar, profil data segar, diukur setelah settle (A: 45 dtk, B: 70 dtk).
- Filter proses: `msedgewebview2.exe` yang command line-nya memuat `\Flit\` (+ shell `Flit.exe`).
- **A** = `--sleep-after=3600` (tidur praktis dimatikan, semua tab termuat aktif-dalam-memori).
- **B** = `--sleep-after=5` (tab latar otomatis `TrySuspendAsync` 5 detik setelah selesai memuat).

## Angka

| Skenario | Proses | Working Set | Private |
|---|---|---|---|
| **A** — 5 tab termuat, tanpa tidur | 11 | 895,2 MB | 477 MB |
| **B** — 5 tab latar tertidur | 11 | **493,7 MB (−45%)** | 466 MB (−2,3%) |
| *Referensi pemilik: Chrome 3 tab* | — | — | *±1,1 GB (±366 MB/tab)* |

Per-tab Flit (A): **±95 MB/tab** vs Chrome ±366 MB/tab — sudah ±4× lebih hemat **sebelum** optimasi apa pun.

## Interpretasi (jujur)

1. **TrySuspend = suspend, bukan discard.** Working set (RAM fisik) turun drastis (−45%) karena OS mem-page-out memori renderer yang disuspend — inilah yang dilihat Task Manager kolom "Memory" dan yang berarti saat RAM mepet. Tapi **private/committed hampir tidak turun** — memorinya masih "dipesan", hanya pindah ke pagefile.
2. Berarti M1 sudah mendapat manfaat kelas **"sleeping tabs"-nya Edge** untuk tekanan RAM fisik. Untuk memotong private bytes juga (target spesifikasi ≤400 MB @20 tab tidur), butuh **tier 2: discard** — buang WebView2 tab yang tidur > N menit (state: URL saja; bangun = buka ulang). Ini pola yang sama dipakai browser besar.
3. Nominal absolut masih tinggi karena: browser-proses + GPU + utility shared (~120 MB) dihitung penuh di 5 tab, dan belum ada flag/consolidation.

## Kesimpulan M1

- Multi-tab + auto-sleep **bekerja** (terverifikasi benchmark A/B + uji manual pemilik di M0.1).
- Thesis RAM terbukti arahnya benar; gap ke target ditutup dengan discard tier-2 + flag — daftar di bawah.

## Optimasi berikutnya (urut dampak)

1. **Deep discard (tier 2):** tab tidur > N menit → `Dispose()` WebView2, simpan URL; bangun = buat ulang + reload. Memotong private bytes besar.
2. `--renderer-process-limit` + konsolidasi utility (`--disable-features=AudioServiceOutOfProcess` dll.).
3. Profil agent terisolasi (folder data per-profil) — belum disentuh di M1.

## Catatan kejadian

- Satu kali crash-start saat eksekusi pertama benchmark A (proses exit < 45 dtk, tanpa jejak di Event Log); reproduksi & retry normal. Dugaan: pemindaian antivirus/Defender terhadap exe yang baru selesai di-compile. Perlu dipantau; jika berulang, tambahkan logging startup.
