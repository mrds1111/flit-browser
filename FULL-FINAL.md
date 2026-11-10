# LightBrowser — Spesifikasi Final v1.0

> Disusun 2026-11-10 dari seluruh riset: [REFERENCES.md](REFERENCES.md), [CONCEPT.md](CONCEPT.md), [ADBLOCK-DESIGN.md](ADBLOCK-DESIGN.md).
> Status: keputusan arsitektur terkunci; verifikasi mekanik stealth (flags/injeksi/headless WebView2) dalam antrian riset lanjutan.

## 1. Identitas produk

- **Nama:** **Flit** — nama lengkap **Flit Browser** (dipilih 2026-11-10; nama kerja dokumen: "LightBrowser"). *To flit* = bergerak cepat & ringan — definisi literal produk. Tidak ada browser lain bernama Flit (bentrokan hanya produk kategori lain: tool packaging Python, app belanja iOS). Rencana repo: `flit-browser/flit`.
- **Tagline:** "One browser, both of us."
- **Satu kalimat:** Browser Windows ringan yang menjadi rumah bagi manusia (GUI minimalis, adblock, hemat RAM) *dan* perangkat kerja AI agent (MCP/CDP, snapshot a11y, input ter-humanisasi, profil stealth opt-in untuk automasi yang sah).
- **Anti-klaim (jujur sejak awal):** bukan alat "menembus semua anti-bot" (tidak ada yang bisa menjanjikan itu — CloakBrowser pun tidak); bukan pengganti ekstensi; tanpa sync cloud; tanpa telemetri.

## 2. Tumpukan arsitektur

```
┌───────────────────────────────────────────────────────────────┐
│ SHELL UI (C# .NET 10 + WinForms — TERKUNCI)                   │
│ tab bar • omnibox • activity log agent • settings             │
├───────────────────────────────────────────────────────────────┤
│ MANAJER TAB & MEMORI          │ LAPISAN AGENT                 │
│ • TrySuspend / discard        │ • MCP server (stdio+HTTP)     │
│ • lazy load tab latar         │ • tools: snapshot(a11y)/click/│
│ • --renderer-process-limit    │   type/navigate/extract/      │
│ • indikator tab tertidur      │   tabs/console/network        │
│                               │ • CDP passthrough opt-in+token│
│                               │ • izin per-domain + activity  │
│                               │   log + konfirmasi sensitif   │
│                               │ • DRIVER INPUT TER-HUMANISASI │
├───────────────────────────────┴───────────────────────────────┤
│ LAPISAN STEALTH (per-profil, opt-in) — lihat §4               │
├───────────────────────────────────────────────────────────────┤
│ BLOKIR IKLAN — parser C# + cosmetic + EasyList/ABPindo        │
│ (desain terverifikasi: ADBLOCK-DESIGN.md)                     │
├───────────────────────────────────────────────────────────────┤
│ ENGINE: WebView2 (Chromium Evergreen)                         │
│ CDP di-broker internal • remote debugging default MATI        │
└───────────────────────────────────────────────────────────────┘
        └── (opsional, M4) backend agent alternatif CDP-compatible
            (mis. Lightpanda) untuk scraping massal situs tak-berlindung
```

## 3. Dua mode, satu browser

| | Mode Manusia | Mode Agent |
|---|---|---|
| Antarmuka | GUI penuh (tab, omnibox) | Headless/GUI tanpa gangguan |
| Masuk lewat | Klik ikon | MCP server & CDP |
| Interaksi | Mouse/keyboard asli | Aksi via ref a11y-tree, ter-humanisasi |
| Profil | Profil harian | Profil terisolasi (bisa + identitas stealth + proxy) |
| Audit | — | Activity log terbaca manusia + izin per-domain |

## 4. Lapisan stealth (per-profil, OPT-IN) — 4 tingkat + batas jujur

| Lapis | Mekanisme | Status |
|---|---|---|
| **S1 — Shell/flags** | `--disable-blink-features=AutomationControlled` (navigator.webdriver), UA via `Settings.UserAgent`, identitas per-profil (user-data-folder terpisah + `--proxy-server` per identitas), `--lang`/locale, ukuran layar profil | Desain target — verifikasi mekanik dalam antrian |
| **S2 — Injeksi JS koheren** | Spoof navigator/screen/WebGL/canvas-noise/audio/fonts dari identitas yang *koheren* per-profil (dibangkitkan dari data device nyata, ala fpgen+coherence Camoufox) | Desain target; level JS = lebih lemah dari patch C++ (deteksi toString dsb.) — jujur soal ini |
| **S3 — Driver humanized** ⭐ | Klik/ketik/scroll dengan timing & kurva manusia **dibangun ke dalam tools MCP kita** — karena KITA driver-nya, semua aksi agent ter-humanisasi secara default | Keunggulan unik yang tidak dimiliki framework eksternal mana pun |
| **S4 — Patch C++ (fork engine)** | Satu-satunya cara mencapai kelas Camoufox/CloakBrowser | ⚠️ Tidak mungkin di WebView2 (binary tertutup). **Pemilik mengizinkan fork** dengan kredit + kepatuhan lisensi ([NOTICES.md](NOTICES.md)) → jalur "edisi stealth" berbasis fork Gecko (ala Camoufox) sebagai eskalasi masa depan, produk terpisah dari inti ringan |

**Framing "bypass Cloudflare/bot detection" yang jujur:**
- Sinyal penentu anti-bot modern ≈ **IP reputation > TLS/HTTP2 fingerprint > behavior > fingerprint JS**.
- Keunggulan struktural kita: engine Chromium **asli** → TLS/HTTP-2 fingerprint identik browser sungguhan (unggul besar vs scraper berbasis HTTP client), dukungan proxy per-identitas, dan S3.
- Yang dijanjikan: *lolos pemeriksaan standar dalam automasi yang sah dengan IP yang layak*. Yang TIDAK dijanjikan: *menembus sistem agresif (DataDome dst.) di datacenter IP* — itu mustahil untuk produk apa pun tanpa residential/mobile proxy.
- **Captcha:** sidecar pluggable TERPISAH, opt-in (keputusan terkunci — lihat CONCEPT.md §7). LightBrowser inti tidak menyertakan solver.
- **Etika:** mode stealth = opt-in eksplisit, tercatat di activity log, untuk konteks yang diizinkan (scraping sah, QA, akun sendiri).

## 5. Blokir iklan — final

Lapisan 1 (network, parser C#, O(1)) + Lapisan 2 (cosmetic CSS) + EasyList/EasyPrivacy/ABPindo; update 12–24 jam dengan atomic swap; eskalasi tunggal ke adblock-rust (MPL-2.0) hanya jika gap >5–10% vs uBlock. Detail terverifikasi: [ADBLOCK-DESIGN.md](ADBLOCK-DESIGN.md).

## 6. Target terukur

RAM idle ≤150 MB • 20 tab latar tidur ≤ +400 MB • startup <1,5 s • snapshot a11y <50 ms • installer shell <15 MB • adblock gap vs uBlock <5–10%.

## 7. Peta jalan final

| Fase | Isi | Stealth |
|---|---|---|
| **M0** | Prototype 1-tab + CDP + benchmark RAM vs Chrome/Mercury (Go/No-Go) | — |
| **M1** | MVP: multi-tab + suspend/discard + MCP tools inti + find-in-page | S1 dasar (webdriver flag off) |
| **M2** | Keamanan agent: izin per-domain, activity log, konfirmasi sensitif, headless mode | S1 lengkap (identitas per-profil + proxy) |
| **M3** | Manusia lengkap: bookmark/sandi/ekstensi/history/save + **adblock penuh** | — |
| **M4** | Stealth S2 (injeksi koheren) + backend Lightpanda opsional + sidecar captcha + eskalasi adblock-rust jika perlu | S2–S3 |

## 8. Keputusan terkunci vs terbuka

**Terkunci:** engine WebView2 • agent = CDP+MCP a11y-first • adblock = engine sendiri bertahap • stealth = opt-in per-profil di level shell/injeksi/driver (fork Gecko untuk "edisi stealth" **diizinkan pemilik** sebagai eskalasi masa depan dengan kredit + lisensi) • captcha = sidecar terpisah • tanpa sync/telemetri/chatbot bawaan • nama produk = **Flit / Flit Browser** (terkunci 2026-11-10) • lisensi proyek = **MPL-2.0** (terkunci 2026-11-11; kriteria pemilik: aman dari tuntutan hukum) • stack shell = **C# .NET 10 + WinForms** (terkunci 2026-11-11; naik dari .NET 8 karena dukungan .NET 8 berakhir Nov 2026, .NET 10 LTS s/d Nov 2028 + dark mode built-in).

**Terbuka:** [ ] verifikasi mekanik stealth S1/headless (riset dalam antrian).

## 9. Risiko terbesar (peringkat)

1. **Performa handler blocking di UI thread** → O(1) sejak baris pertama (ADBLOCK-DESIGN.md).
2. **Mode headless WebView2** — belum terverifikasi resmi; fallback: jendela tersembunyi/minimized. (Riset dalam antrian.)
3. **Over-promise stealth** → jaga framing §4; deteksi adalah perlombaan senjata, IP menentukan.
4. **Ekor panjang fitur harian** (M3) → jaga disiplin §7 CONCEPT.md (masuk/dibuang/ditunda).
