# M5 — Hasil & Bukti (pasca-peta jalan)

Tanggal: 2026-11-11 • Status: **SELESAI** • Tema: optimasi memori (deep-discard tier-2)

## Yang dibangun: deep-discard tier-2

M1 sudah punya tidur ringan (`TrySuspend`, WS −45% tapi private tetap). M5 melengkapinya
dengan tingkat kedua yang benar-benar membuang memori:

- Tab latar yang sudah tidur lebih lama dari ambang (default **300 detik**, `--deep-after=N`,
  `0` = mati) → **WebView2-nya di-Dispose seluruhnya** — proses renderer msedgewebview2
  milik tab itu ikut lenyap dari Task Manager.
- URL terakhir disimpan (`RestoreUrl`); tab disentuh (diklik manusia / disentuh tool MCP)
  → WebView2 dibuat ulang + halaman dimuat ulang otomatis (`tab.revived` di log).
- Aman: tab aktif tidak pernah dibuang; tab yang main audio tidak pernah tidur → tak pernah dibuang.
- Status MCP `flit_list_tabs` kini menampilkan `isDiscarded: true` (transparan untuk agent).

## Bukti uji (runtime nyata — skrip `scripts/test-m5.ps1` + pengukuran proses)

```
tab.discarded  {"title":"Example Domain","url":"https://example.org/"}   ← log
tab.revived    {"url":"https://example.org/"}                            ← log, saat disentuh

flit_list_tabs  → {"index":1, "isSleeping":true, "isDiscarded":true}
flit_evaluate_js tab 1 (setelah dibuang) → "Example Domain | https://example.org/"  ← hidup lagi

Metrik (2 tab: about:blank aktif + example.org latar):
  private (status Flit)      270 MB → 231 MB  (−39 MB utk 1 tab ringan)
  total WS pohon Flit        878 MB → 819 MB
  jumlah proses msedgewebview2  13 → 12       (renderer tab dibuang lenyap)
```

Situs berat (video/SPA) membuang jauh lebih banyak — example.org hanyalah batas bawah.

## Tradeoff jujur

- Tab yang dibuang **kehilangan state halaman** (scroll, isi form, riwayat maju-mundur):
  dibangunkan = dimuat ulang URL terakhir — perilaku mirip tab "discarded" Chrome.
- Default 300 detik sengaja longgar; penjelajah biasa hampir tak pernah merasakan reload.
- Sesi login umumnya aman (cookie tersimpan di folder profil, bukan di memori tab).

## Lain-lain di M5

- `ReviveTabAsync` dipakai bersama manusia (klik tab) & agent (`WakeTabAsync`) — satu jalur.
- Regresi: M2/M3/M4 tetap hijau (uji dijalankan ulang setelah perubahan).
