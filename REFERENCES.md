# Referensi Proyek LightBrowser

> Tujuan proyek: browser **ringan** untuk dipakai manusia **dan** AI agent.
> Catatan lapangan pemilik: Mercury (banyak tab) ≈ 1,1 GB RAM; Chrome (3 tab) ≈ 1,1 GB RAM.
> Terakhir diperbarui: 2026-11-10

## A. Fork engine dengan optimisasi (browser harian, RAM rendah)

| Proyek | Basis | RAM / profil | Integrasi agent | Status & catatan |
|---|---|---|---|---|
| [Mercury](https://github.com/Alex313031/Mercury) ([situs](https://thorium.rocks/mercury)) | Firefox | Sangat hemat (model proses Gecko: content process dibatasi ±8) | BiDi via `--remote-debugging-port` | Optimisasi compiler AVX/LTO/PGO + patch privacy (LibreWolf/BetterFox). Klaim 8–20% di atas Firefox. Risiko klasik fork: [tertinggal patch keamanan](https://aur.archlinux.org/packages/mercury-browser-avx2-bin). |
| [Thorium](https://github.com/Alex313031/Thorium) | Chromium | Ala Chromium | CDP penuh | Kakak Mercury; bukti bahwa optimisasi compiler hanya memberi gain kecil — model proseslah yang dominan. |
| [Ungoogled-Chromium](https://github.com/ungoogled-software/ungoogled-chromium) | Chromium | Lebih ramping dari Chromium stok (layanan Google + telemetri dibuang) | CDP penuh | Kumpulan patch *debloat* kanonik; biner disediakan kontributor dan sering telat rilis. Menjadi basis Helium. |
| [Helium](https://github.com/imputnet/helium) ([situs](https://helium.computer)) | Chromium (fork Ungoogled) | Ala Chromium tanpa bloat | CDP penuh | Dari tim Cobalt. Adblock + proteksi fingerprint default, tab vertikal, auto-update Win/macOS — **contoh fork Chromium yang benar-benar terawat dan enak dipakai harian**. |
| [Cromite](https://github.com/uazo/cromite) | Chromium (penerus Bromite) | Adblock bawaan → hemat saat browsing | CDP penuh | Aktif (rilis Sep 2026), biasanya tertinggal beberapa versi dari Chrome; ⚠️ cromite.org mati berbulan-bulan — unduh lewat GitHub releases. |
| [Mullvad Browser](https://github.com/mullvad/mullvad-browser) | Firefox ESR + patch Tor Browser | Ala Firefox | BiDi | Referensi hardening privasi (kolaborasi Mullvad + Tor) tanpa jaringan Tor. |
| [Zen Browser](https://zen-browser.app) / [Floorp](https://floorp.app) ([benchmark](https://browserating.com/compare/floorp-vs-zen-browser)) | Firefox | Lebih ringan dari Firefox stok | BiDi | Fork Firefox **terawat & populer** — kandidat basis kalau mau jalur Gecko tanpa memikul seluruh maintenance. |
| Waterfox / LibreWolf | Firefox | Ala Firefox | BiDi | Referensi pola fork yang langgeng. |

## B. Browser AI (GUI + agent) — semua Chromium, semuanya berat

| Proyek | Pembuat | Catatan |
|---|---|---|
| [ChatGPT Atlas](https://openai.com/atlas/) | OpenAI | Chromium; agent mode berbayar; (per pertengahan 2026 masih macOS-only — cek ulang). |
| [Comet](https://www.perplexity.ai/comet) | Perplexity | Chromium; Chrome + sidebar assistant. |
| [Dia](https://www.diabrowser.com) | The Browser Company | Chromium; paling disukai pengguna umum di perbandingan komunitas. |
| Edge Copilot Mode | Microsoft | Chromium + integrasi Copilot. |

Perbandingan: [MarkTechPost](https://www.marktechpost.com/2025/11/15/comparing-the-top-4-agentic-ai-browsers-in-2025-atlas-vs-copilot-mode-vs-dia-vs-comet) · [searchviu](https://www.searchviu.com/en/ai-browsers-2026-compared) · [Human Security (keamanan agentic)](https://www.humansecurity.com/learn/blog/chatgpt-atlas-vs-perplexity-comet-agentic-browsers)

**Pelajaran:** arah pasar "browser + AI" sudah ramai, tapi semua pesaing memikul Chromium penuh → RAM tinggi. **Celah positioning LightBrowser: ringan + ramah agent.**

## C. Engine/browser machine-first & engine baru

| Proyek | Basis | GUI? | Integrasi agent | Status & catatan |
|---|---|---|---|---|
| [Lightpanda](https://lightpanda.io/) ([repo](https://github.com/lightpanda-io/browser)) | Engine from-scratch di **Zig** | ❌ headless-only | **CDP native** (klien Puppeteer/Playwright konek langsung) | v1.0, ±37k★, ada backing perusahaan + cloud offering (lebih sehat dari Verso). Klaim (benchmark sendiri, chromedp 933 halaman): 9x lebih cepat, 16x lebih hemat memori. Risiko: kompatibilitas situs nyata karena engine baru. **Peran untuk LightBrowser:** ❌ bukan basis GUI (tanpa GUI + risiko kompat); ✅ kandidat *backend opsional* tugas agent-only — karena sama-sama CDP, lapisan MCP kita bisa engine-agnostic; ✅ angka & metodologi benchmark-nya = target efisiensi mode agent. |
| [Servo](https://servo.org) ([repo](https://github.com/servo/servo)) | Engine from-scratch di **Rust** | servoshell (demo) | WebDriver | Aktif 2025–2026 ([blog bulanan](https://servo.org/blog/2025/10/24/this-month-in-servo)); strategi *embeddable engine*. Pendanaan kecil tapi progres konsisten. |
| [Verso](https://github.com/versotile-org/verso) | Servo (Rust) | ✅ eksperimental | WebDriver (via Servo) | **❌ ARSIP — tidak dirawat sejak 8 Okt 2025** (minim tenaga & dana; kontribusi besar sudah dilebur ke Servo upstream; servoshell diakui UX-nya lebih baik). Pelajaran penting: membangun browser GUI di atas engine yang masih cepat berubah = beban yang tidak tertanggung proyek kecil. |
| [Ladybird](https://ladybird.org) | Engine from-scratch (LibWeb) | ✅ penuh | ❌ (belum) | [Pre-alpha → alpha](https://ladybird.org/newsletter/2025-10-31); [klaim RAM turun ~80%, ~1/3 Chromium](https://piunikaweb.com/2026/06/09/ladybird-browser-cuts-memory-usage). Menarik untuk manusia, belum untuk agent. |
| Chrome Headless Shell / Firefox headless | Chromium / Gecko | ❌ | CDP / BiDi | Baseline industri pembanding. |

## D. Anti-detect / stealth & captcha (agar agent tidak diblok)

| Proyek | Basis | Pendekatan | Integrasi agent | Status & catatan |
|---|---|---|---|---|
| [Camoufox](https://github.com/daijro/camoufox) | **Firefox** fork (patch C++ level) | Spoof fingerprint **di C++, bukan JS** (lolos cek `toString()`); identitas per-launch koheren dari data device nyata (fpgen + pemeriksa coherence); canvas/audio sengaja tidak diberi noise | Playwright via Juggler tersembunyi (bukan CDP); `navigator.webdriver=false`; kursor humanized | 13,2k★, MPL-2.0 (browser) + MIT (binding Py/TS). Masih "under development". Keterbatasan: identitas Firefox saja. |
| [CloakBrowser](https://github.com/CloakHQ/cloakbrowser) | **Chromium** custom (87 patch source-level) | Fingerprint diubah di source C++ (canvas/WebGL/font/WebRTC); seed fingerprint acak/stabil; mode humanize (mouse, ketikan, scroll) | Drop-in Playwright replacement; `cloakserve` CDP server (Docker) | **❌ DITOLAK sebagai basis** — binary closed-source + lisensi per key (free = 1 sesi), patch tidak publik, harus percaya binary pihak lain; bertentangan dengan prinsip proyek. Tetap dicatat sebagai bukti pasar (32,8k★) & pola arsitektur stealth. Wrapper-nya saja MIT. |
| [captcha-solver (cipher-x)](https://github.com/cipher-x-sudo/captcha-solver) | Sidecar HTTP (FastAPI) di atas CloakBrowser | Solver captcha **lokal**: 24 model ONNX (Arkose), OpenCV (slide), harvest cookie clearance (cf_clearance, AWS WAF, DataDome) | REST `POST /solve` — bisa dipanggil browser/siat apa pun | 3★, baru, tanpa lisensi. **Mewarisi status "tidak layak basis"** karena bergantung CloakBrowser; pola sidecar-nya sendiri tetap menarik. ⚠️ Token terikat IP/TLS fingerprint (butuh residential proxy), hanya silent challenge untuk AWS WAF. |
| [FuckCaptcha (vsmutok)](https://github.com/vsmutok/FuckCaptcha) | Bukan tool — **dokumentasi** | "Field guide" 39 tipe captcha (Turnstile, reCAPTCHA, hCaptcha, GeeTest, Arkose, DataDome, ...) + katalog solver | — | 130★, ensiklopedia referensi berguna; lisensi tidak konsisten (CC0 vs MIT). |

Catatan posisi untuk LightBrowser: masalah "agent diblok" nyata, tapi bypass captcha/anti-detect menyentuh ToS situs & area hukum abu-abu — pendekatan arsitektural yang lebih aman adalah **mode stealth opt-in** dan/atau **solver sebagai sidecar opsional terpisah**, bukan fitur inti browser.

## E. Lapisan integrasi agent (pola arsitektur untuk disalin)

| Proyek | Pola |
|---|---|
| [Playwright](https://playwright.dev) / [Puppeteer](https://pptr.dev) | Driver CDP (Chromium) & WebDriver BiDi (Firefox). Playwright punya fork Firefox sendiri. |
| [agent-browser (Vercel Labs)](https://github.com/vercel-labs/agent-browser) | CLI Rust (+ mode MCP) untuk agent: `snapshot` **accessibility-tree** → ref elemen → klik/ketik — tanpa screenshot. Sibling: [agent-desktop](https://www.marktechpost.com/2026/10/31/vercel-labs-agent-desktop-computer-use) (computer use, ~60 perintah a11y). **Ini tipe klien yang harus dilayani cepat & akurat oleh LightBrowser** → a11y tree adalah output kunci, bukan sekadar DOM mentah. |
| [Playwright MCP](https://github.com/microsoft/playwright-mcp) | **MCP server resmi Microsoft di atas Playwright** — pola paling relevan: aksesibilitas-tree snapshot + aksi terstruktur, bukan screenshot mentah. |
| [browser-use](https://github.com/browser-use/browser-use) / [Stagehand](https://github.com/browserbase/stagehand) / Skyvern | Framework agent di atas Chromium: ekstrak DOM/a11y → LLM memutuskan aksi. |

## F. GUI minimalis (referensi UI ringan)

- [Min](https://minbrowser.org) — minimalis, tapi basis Electron (tidak sehemat kelihatannya).
- [qutebrowser](https://qutebrowser.org) — keyboard-driven, QtWebEngine (Chromium).
- [Falkon](https://www.falkon.org) — shell ringan ala KDE di atas QtWebEngine.
- [GNOME Web / Epiphany](https://apps.gnome.org/Epiphany/) — WebKitGTK; ada tech preview Windows; kompatibilitas web di bawah Chromium/Gecko.
- Pale Moon / NetSurf / Dillo — sangat ringan, kompatibilitas web rendah.

## Sintesis (per 2026-11-10)

1. **RAM rendah dari model proses + tab-sleeping**, bukan (hanya) optimisasi compiler — bukti: Mercury vs Thorium.
2. **Semua browser AI populer = Chromium penuh** → tidak ada pemain "ringan + agent-first" di ruang GUI.
3. **CDP adalah pintu integrasi agent terluas** (seluruh ekosistem Playwright/Puppeteer/MCP tools); Gecko hanya punya BiDi.
4. Engine from-scratch (Lightpanda/Servo/Ladybird) hemat tapi menanggung risiko kompatibilitas jangka panjang.
5. Ruang stealth/captcha terbelah dua: **fork terbuka ala Camoufox** (patch C++, Playwright/Juggler, GPL-friendly) vs **binary closed-source ala CloakBrowser** (populer tapi black box + lisensi per sesi). Keduanya membuktikan bahwa patch level C++ > injeksi JS untuk stealth.
6. Pattern klien agent modern (agent-browser, Playwright MCP) berbasis **accessibility-tree snapshot**, bukan screenshot/DOM mentah → browser kita harus bisa memproduksi a11y tree yang cepat, ringkas, dan stabil.
7. **Kebutuhan desain (dikonfirmasi pemilik): agent harus bisa "memakai DevTools"** → shell mem-broker CDP internal dan mengeksposnya dua tingkat: (a) MCP server bawaan ber-tools tingkat tinggi, (b) passthrough CDP opt-in + token untuk Playwright/Puppeteer/MCP eksternal. Remote debugging port default **mati** (risiko: proses lokal mana pun bisa mengambil alih browser). Jalur Gecko (BiDi) tidak bisa memenuhi ini setara — memperkuat jalur Chromium/WebView2.

## Shortlist kandidat basis (DRAFT — belum final)

| Kandidat | Kelebihan | Kekurangan |
|---|---|---|
| Fork Firefox terawat (Zen/Floorp) | RAM ala Mercury, siap pakai untuk manusia, komunitas besar | Integrasi agent via BiDi (ekosistem lebih tipis dari CDP) |
| Shell sendiri di WebView2 | Kontrol penuh, CDP native, RAM idle terendah, dev cepat di Windows | Per-tab ala Chromium (diatasi tab-sleeping); terikat platform Windows |
| Chromium debloat (Ungoogled/Helium) | CDP penuh + bebas bloat Google; Helium contoh fork terawat | Usaha build Chromium besar; RAM inti tetap ala Chromium |

Gugur untuk saat ini: CloakBrowser (closed-source), captcha-solver (bergantung CloakBrowser), Verso (arsip). Dipantau saja: Lightpanda & Servo (potensi mode agent-only), Ladybird (jangka panjang).

## Pertanyaan terbuka

- [ ] Jalur engine: shell WebView2 + tab-sleeping vs fork Firefox (sendiri / di atas Zen/Floorp)?
- [ ] Stack shell: C# .NET / Rust / C++?
- [ ] Level integrasi agent MVP: CDP saja, atau CDP + MCP server bawaan?
- [ ] Target pengguna utama versi 1: manusia dulu, agent dulu, atau keduanya sekaligus?
- [ ] Apakah stealth/anti-detect masuk lingkup inti (opt-in "agent mode") atau sidecar terpisah? Solver captcha: bawaan atau pluggable?
