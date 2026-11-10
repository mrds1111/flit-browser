# Desain Blokir Iklan Bawaan — Annex [CONCEPT.md](CONCEPT.md)

> Hasil riset workflow `webview2-adblock-research` (10 Nov 2026).
> **Metodologi:** 4 peneliti paralel gagal (batasan konfigurasi model); seluruh temuan berasal dari agent verifier independen yang memverifikasi ulang klaim krusial langsung ke sumber primer (Microsoft Learn, repo GitHub). Sisa pertanyaan terbuka dicatat di bawah.

## Arsitektur berlapis (terverifikasi)

### Lapisan 1 — Network blocking (MVP)
- `CoreWebView2.WebResourceRequested` + `AddWebResourceRequestedFilter("*", <semua konteks resource>)` **plus** filter `CoreWebView2WebResourceRequestSourceKinds (Supplemental | NonServiceWorker)` agar request iframe/subresource ikut tercakup.
- Blokir: `e.Handled = true` + response kosong.
- Engine: **parser ABP murni C#** — titik awal kandidat: [DistillNET](https://www.nuget.org/packages/DistillNET). Struktur data **O(1) domain→rules** (bukan scan linear).
- Fakta pendukung: >90% aturan daftar populer adalah network filter biasa → subset C# menutup mayoritas kasus.

### Lapisan 2 — Cosmetic/element hiding (MVP+)
- Kumpulkan selektor `##`/`###` dari daftar yang sama → injeksi via `AddScriptToExecuteOnDocumentCreatedAsync` (JS kecil: pasang `<style>` per domain + observer ringan). Menutup mayoritas gap iklan yang lolos network layer dengan biaya paling murah.

### Fase lanjut — pilih SATU (jangan keduanya sekaligus, jangan di MVP)
| Jalur | Detail | Catatan |
|---|---|---|
| (a) Engine native | [adblock-rust](https://github.com/brave/adblock-rust) (Brave, MPL-2.0) via **shim `extern "C"` buatan sendiri + P/Invoke** | Tidak ada C API resmi (hanya crate Rust, npm WASM, binding Python komunitas). Kerja sedang; hasil kelas produksi. Layak hanya jika procedural filter/scriptlet jadi kebutuhan nyata. |
| (b) Ekstensi unpacked | uBlock Origin via `AddBrowserExtensionAsync` | API **STABLE sejak SDK 1.0.2210.55 (Des 2023)** — bukan eksperimental. Tapi: hanya unpacked dari folder lokal (tanpa Web Store), folder tidak boleh berubah setelah instalasi, distribusi source uBO = GPLv3. Beban kepatuhan tinggi → opsi terakhir. |

## Bundel daftar filter (MVP)
| Daftar | Lisensi | Catatan |
|---|---|---|
| EasyList + EasyPrivacy | dual GPLv3 / CC BY-SA 3.0 | Kanonik global; atribusi wajib |
| [ABPindo](https://github.com/ABPindo/indonesianadblockrules) | dual GPL-3.0 / CC BY-SA 3.0 | Indonesia/Melayu; **sangat aktif** (push terakhir 10 Nov 2026) |

Hindari bundel uAssets di fase awal (GPLv3, update beberapa kali sehari, volume besar).

## Mekanisme update
Snapshot bundled sebagai fallback offline → saat online, unduh tiap 12–24 jam via GET berkondisi (ETag / If-Modified-Since) ke raw.githubusercontent.com dengan mirror jsDelivr → parse di background thread → **atomic swap** engine → jika fetch/parse gagal, pertahankan snapshot lama (**jangan pernah kosongkan blokir**). Re-parse penuh hanya saat isi benar-benar berubah.

## Risiko utama (peringkat)
1. **Performa handler `WebResourceRequested`:** dipanggil di UI thread per-request dan *memblokir* network sampai selesai → pencocokan lambat = lag di semua halaman. Mitigasi: struktur O(1) per domain, cek paling murah dulu.
2. **Celah service worker:** request lewat SW tidak memicu event ([WebView2Feedback #1114](https://github.com/MicrosoftEdge/WebView2Feedback/issues/1114)) — sebagian iklan (popunder) bisa lolos; tidak ada mitigasi level API, kelola ekspektasi.
3. **Salah arah:** jangan mulai MVP dari FFI adblock-rust atau bundling uBO — biaya integrasi/kepatuhan tinggi sebelum nilai terbukti.

## Sisa pertanyaan terbuka
- [ ] Apakah adblock-rust punya modul FFI/C API internal yang tak terdokumentasi? (perlu inspeksi repo)
- [ ] Cakupan parser DistillNET terhadap syntax uBO modern (`:has()`, scriptlet, redirect)
- [ ] Lisensi persis sub-list Fanboy/Annoyances; teks lengkap lisensi ABPindo (GPL.txt & CC-BY-SA.txt ada di repo, belum dibaca penuh)
- [ ] Benchmark latensi riil handler C# per-request di WebView2 (belum ada angka primer; hanya semantik "blocks until handler returns")

## Sumber primer
- [CoreWebView2.WebResourceRequested — Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2.webresourcerequested)
- [AddBrowserExtensionAsync — Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2profile.addbrowserextensionasync)
- [WebView2 SDK 1.0.2210.55 release notes (ekstensi → Stable)](https://learn.microsoft.com/en-us/microsoft-edge/webview2/release-notes/sdk/1-0-2210-55)
- [brave/adblock-rust](https://github.com/brave/adblock-rust)
- [ABPindo/indonesianadblockrules](https://github.com/ABPindo/indonesianadblockrules)
