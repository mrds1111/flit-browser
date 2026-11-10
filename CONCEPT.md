# LightBrowser — Konsep Produk (DRAFT v1)

> Browser ringan untuk **manusia dan AI agent** — "one browser, both of us."
> Dari riset: semua browser AI populer (Atlas/Comet/Dia) memikul Chromium penuh; semua engine ringan tidak melayani agent. Celah inilah yang kita isi.
> Per 2026-11-10 — lihat [REFERENCES.md](REFERENCES.md) untuk basis riset.

## 1. Pengalaman yang ingin dicapai

**Sebagai manusia:** buka dalam < 1 detik, UI minimalis (tab + address bar), idle ±100–150 MB, 30 tab latar belakang tetap enteng (tab tertidur otomatis), tanpa telemetri, tanpa bloat.

**Sebagai agent (atau developer yang memakai agent):** browser ini *muncul sebagai perangkat* — MCP server bawaan + CDP. Agent minta snapshot halaman → dapat **accessibility tree ringkas dengan ref elemen** dalam puluhan ms, klik/ketik lewat ref, ekstrak konten sebagai markdown. Semua aksi agent tercatat dan bisa dibatasi.

## 2. Arsitektur

```
┌────────────────────────────────────────────────────────────┐
│                     SHELL (UI native)                       │
│   tab bar • address bar • settings • activity log agent    │
├────────────────────────────────────────────────────────────┤
│  MANAJER TAB & MEMORI          LAPISAN AGENT               │
│  • suspend/discard per-tab     • MCP server bawaan         │
│    (CoreWebView2.TrySuspend)     (stdio + HTTP lokal)      │
│  • lazy load tab latar         • tools: snapshot/click/    │
│  • --renderer-process-limit      type/navigate/extract/    │
│  • target: ala "Memory Saver",    tabs/history/download    │
│    tapi lebih agresi & bisa     • CDP passthrough opt-in    │
│    dikendalikan pengguna         + token (Playwright dll)  │
│                                • log & izin aksi agent     │
├────────────────────────────────────────────────────────────┤
│              ENGINE: WebView2 (Chromium Evergreen)          │
│   kompat web penuh • CDP di-broker di dalam proses          │
│   (port remote debugging default MATI)                      │
└────────────────────────────────────────────────────────────┘
        │ (opsional, fase lanjut)
        └── backend agent-only yang bisa ditukar (mis. Lightpanda,
            CDP-compatible) untuk tugas scraping murah
```

Prinsip kunci: **lapisan agent engine-agnostic di atas CDP** — hari ini WebView2, besok backend lain bisa ditukar tanpa mengubah tools agent.

## 3. Fitur inti per sisi

### Sisi manusia (MVP dulu → matang kemudian)
- Multi-tab, address bar, back/forward/reload, find-in-page
- Tab sleeping otomatis + indikator tab tertidur, klik = bangun
- Vertical tabs (opsional), dark mode
- Tanpa telemetri; data lokal
- (Fase 2+) bookmark/history/download manager minimal, dukungan ekstensi Chromium (WebView2 mendukung), profile
- (Fase 2+) **fasilitas harian lengkap**: autosave & isi password + autofill form (`IsPasswordAutosaveEnabled` WebView2, tersimpan lokal terenkripsi), jendela privat, DevTools (F12) untuk pengguna, menu konteks (save image/link), save page MHTML/PDF, clear browsing data, PDF viewer (diverifikasi M1)

### Sisi agent (inti pembeda — sejak MVP)
- MCP server bawaan: `snapshot` (a11y tree + ref), `click(ref)`, `type(ref, text)`, `navigate(url)`, `extract_content()` → markdown, `tabs`, `history`, `console`, `network`
- **CDP passthrough** opt-in + token → Playwright/Puppeteer/chrome-devtools-mcp konek tanpa library tambahan
- Snapshot berbasis **accessibility tree** (pola agent-browser/Playwright MCP) — bukan screenshot, bukan DOM mentah
- Mode headless tanpa jendela untuk otomasi
- Keamanan: remote debugging default mati; izin per-domain untuk agent; **activity log** yang terbaca manusia; aksi sensitif (form pembayaran, submit kredensial) butuh konfirmasi

### Stealth (fase lanjut, opt-in)
- Higiene fingerprint + input ter-humanisasi untuk konteks yang diizinkan — pola Camoufox (patch level mesin bila suatu saat fork), solver captcha sebagai **sidecar pluggable terpisah**, bukan inti

## 4. Target terukur (bukan janji — diverifikasi benchmark)

| Metrik | Target |
|---|---|
| RAM idle (1 tab kosong) | ≤ 150 MB |
| 20 tab latar tertidur | ≤ 400 MB total tambahan |
| Startup dingin | < 1,5 s |
| `snapshot` a11y halaman umum | < 50 ms |
| Ukuran installer shell | < 15 MB (engine dari Evergreen) |

Protokol benchmark memakai metode ala Lightpanda (situs nyata, chromedp/CDP, pembanding Chrome & Mercury).

## 5. Keputusan terkunci vs terbuka

**Terkunci (dari riset):**
- Engine: **WebView2/Chromium** (CDP penuh, tanpa beban rawat engine; RAM dicapai via tab lifecycle)
- Integrasi agent: **CDP + MCP bawaan**, a11y-tree-first, passthrough opt-in
- Stealth/solver: **opt-in/sidecar**, bukan inti
- Engine from-scratch (Lightpanda/Servo/Ladybird) & fork Firefox penuh: **bukan basis** (kompat & beban maintenance)

**Terbuka:**
- [ ] Stack shell: C# .NET 8 (WinForms/WPF) vs Rust (webview2-com/tao) vs C++ Win32
- [ ] Nama produk final
- [ ] Detail UI (framework GUI, bahasa UI id/en)
- [ ] Lisensi proyek (MIT / MPL / GPL?)

## 6. Peta jalan kasar

| Fase | Isi | Keluaran |
|---|---|---|
| M0 | Prototype shell 1-tab + CDP endpoint + benchmark RAM vs Chrome/Mercury | Data Go/No-Go terukur |
| M1 | MVP: multi-tab + suspend/discard + MCP tools inti (navigate/snapshot/click/type/extract) | Browser terpakai harian, agent bisa mengoperasikan |
| M2 | Keamanan agent: izin per-domain, activity log, konfirmasi aksi sensitif; headless mode | Aman untuk otomasi nyata |
| M3 | Sisi manusia matang: bookmark/history/downloads, ekstensi, settings | Pengganti browser sehari-hari |
| M4 | Backend agent alternatif (Lightpanda), stealth opt-in, sidecar solver | Diferensiasi penuh |

## 7. Lingkup: ✅ MASUK vs ❌ DIBUANG

### ✅ Masuk
- **M1 (manusia inti):** multi-tab, omnibox, navigasi standar, find-in-page, zoom, dark mode, multi-window, download dasar, **tab sleeping otomatis + indikator**, settings minimal.
- **M3 (manusia lengkap):** bookmark & history minimal, download manager, **ekstensi Chromium**, vertical tabs, profile, print/PDF, **blokir iklan/tracker bawaan** (filter network via `WebResourceRequested`), import bookmark, **autosave password + autofill form (lokal), jendela privat, DevTools F12, save image/link & save page (MHTML/PDF), clear browsing data, PDF viewer**.
- **M1–M2 (agent — pembeda inti):** MCP server bawaan (`snapshot`/`click`/`type`/`navigate`/`extract_content`/`tabs`/`console`/`network`/`screenshot`), CDP passthrough opt-in + token, headless mode, izin per-domain, activity log terbaca manusia, konfirmasi aksi sensitif, profil agent terisolasi.
- **Semua fase:** tanpa telemetri, data lokal, HTTPS-upgrade, remote debugging default mati.
- **M4 (opt-in):** higiene fingerprint + input humanized, backend agent alternatif (Lightpanda), solver captcha sebagai sidecar terpisah.

### ❌ Dibuang (dengan alasan)
- **Sync cloud/akun** — infrastruktur besar, bertentangan dengan data lokal.
- **Telemetri/crash reporting** — prinsip privasi: nol.
- **Chatbot AI bawaan** (ala Copilot/Atlas) — filosofi kebalikan: browser sebagai *perangkat* bagi agent milik pengguna (bring-your-own-agent via MCP/CDP), bukan menanam LLM.
- **Feed berita/shopping/widget new tab** — bloat; new tab minimal.
- **Melemahkan site isolation demi RAM** — pakai suspend/discard, jangan turunkan keamanan.
- **Fork/patch engine sendiri** — beban maintenance (pelajaran Mercury & Verso).
- **VPN/wallet/kripto bawaan** — bukan urusan browser.
- **Mouse gesture/tema store** — selera minor, permukaan kode besar.
- **Windows 7/8** — WebView2 butuh Win10+.
- **macOS/Linux fase awal** — fokus Windows; pertimbangan masa depan.
- **API plugin buatan sendiri** — cukup ekstensi Chromium + MCP.

### ⏸️ Ditunda (belum diputuskan — tidak memblokir MVP, ditinjau setelah M3)
Reader mode, speed dial, split view, cast media, Mac/Linux.
