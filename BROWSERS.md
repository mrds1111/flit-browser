# BROWSERS.md — Katalog Referensi Browser

> Disusun 2026-11-10 dari riset terstruktur (6 peneliti paralel + agen cek kelengkapan) untuk proyek **Flit Browser** (nama kerja: LightBrowser). Pendamping: [REFERENCES.md](REFERENCES.md) (keputusan lolos/tolak per referensi), [FULL-FINAL.md](FULL-FINAL.md) (spesifikasi final).
>
> **Catatan metodologi (jujur):**
> - Peneliti kelompok *chromium-forks* gagal karena koneksi (ECONNREFUSED) — data Thorium/Ungoogled/Helium/Cromite diisi dari riset awal, sehingga lebih ringkas.
> - Semua angka "× lebih cepat / lebih hemat" adalah **klaim vendor kecuali dinyatakan lain**.
> - Browser mati ditandai 💀 dengan tanggal verifikasi web.
> - Urutan baca mengikuti saran cek kelengkapan: automasi → UX → privasi → engine → pelajaran kegagalan.

---

## 1. Siap-automasi — paling relevan untuk sisi agent kita

### Lightpanda ⭐
- **Basis:** engine baru dari nol (Zig + V8), murni headless (tanpa pipeline grafis).
- **Status & lisensi:** 1.0 GA 2 Okt 2026; **AGPL-3.0 (CLA)** — ⚠️ cek lisensi sebelum memakai kode apa pun; ~41,6k★; aktif.
- **Fitur kunci:** CDP server bawaan (port 9222) • **MCP native** (stdio + HTTP) • agent mode (perintah bahasa alami → PandaScript deterministik & replayable) • CLI dump (HTML/markdown/PDF/JSON-LD) • WebDriver BiDi • adblocker bawaan • `--obey-robots` • **isolasi sesi per-agent** (satu koneksi MCP = satu sesi terisolasi: page/cookie/RAM sendiri).
- **Platform:** Linux/macOS — **tidak ada Windows native (WSL2 saja)**.
- **Klaim:** 16× lebih hemat memori, 9× lebih cepat dari Chrome *(klaim vendor)*.
- **Pelajaran untuk Flit:** CDP+MCP harus first-class sejak hari pertama; pola *satu-agent-satu-sesi-terisolasi* kita adopsi untuk profil agent.

### CloakBrowser — ❌ DITOLAK sebagai basis (keputusan terdokumentasi di [REFERENCES.md](REFERENCES.md)); tetap sumber pelajaran desain
- **Basis:** Chromium 155 Pro + 87 patch level source C++; wrapper open-source MIT (Python/JS/.NET), binary **proprietary** (verifikasi Ed25519-pinned).
- **Fitur kunci:** `--fingerprint=<seed>` → identitas deterministik persisten • `humanize=True` (mouse Bézier dengan overshoot, ketik per-karakter dengan typo realistis) • `geoip=True` • cloakserve (CDP server Docker, multi-identitas per seed via query params) • **GUI profile manager yang mengekspos endpoint CDP** • klaim ~190 MB idle *(klaim vendor)*.
- **Model lisensi:** wrapper MIT gratis + binary proprietary berbayar per tier sesi konkuren.
- **Pelajaran untuk Flit:** pola "AI agent profiles" (seed → identitas, dikelola GUI); model monetisasi *open-wrapper/paid-engine* bila suatu saat perlu.

### Microsoft Edge + Copilot — referensi efisiensi RAM + keterbukaan automasi (bukan dikategorikan "AI browser")
- **Basis:** Chromium (propietary, Microsoft); v156 stable; gratis; aktif.
- **Fitur kunci:** **sleeping tabs • efficiency mode • startup boost** (= playbook resmi hemat RAM yang kita tiru) • Copilot Actions (agen menjalankan multi-langkah di tab) & Journeys • vertical tabs, workspaces.
- **Automasi:** **paling terbuka dari semua browser AI** — CDP/Playwright/WebDriver BiDi penuh (warisan Chromium).
- **Pelajaran untuk Flit:** pola lifecycle tab ala Edge jadi acuan target RAM kita; catatan: riwayat privasi Edge buruk — jangan tiru praktik telemetrinya.

### qutebrowser
- **Basis:** Python + Qt/QtWebEngine (Chromium); GPLv3+ (didanai donasi); aktif.
- **Fitur kunci:** modal vim-like • **hints mode** — semua elemen klik diberi label huruf di layar • konfigurasi penuh via Python • setting per-domain • adblock opsional gaya Brave.
- **Automasi:** tidak ada CDP/MCP resmi, tapi seluruh UI digerakkan via perintah teks.
- **Pelajaran untuk Flit:** hints mode = mekanisme disambiguasi klik terbaik untuk agent; lapisan perintah teks bisa jadi jembatan MCP langsung — **belum ada yang melakukan ini; peluang kita**.

## 2. Penjual UX — referensi fitur mode manusia

### Zen Browser
- **Basis:** fork Firefox; FF 157; MPL-2.0; aktif, sangat ramai (44,9k★).
- **Fitur kunci:** vertical tabs • workspaces • split view • Compact mode • Glance (preview tab tanpa berpindah) • Zen Mods (mod komunitas) • web panels.
- **Pelajaran untuk Flit:** UX adalah penjualan utama; workspaces layak diekspos ke agent sebagai konteks kerja terpisah.

### Floorp
- **Basis:** fork Firefox (Ablaze, Jepang); jalur ESR + Floorp12 (feles-build/Deno); MPL-2.0; aktif.
- **Fitur kunci:** dual sidebar • tab pohon (tree tabs) • layout paling fleksibel di antara fork • 5 tema Lepton • web panel di sidebar.
- **Catatan:** **code-signed (Certum)** — satu-satunya fork Firefox kecil dengan sertifikat kode; klaim "update lebih dulu dari Firefox" sebaiknya dibaca sebagai *patching ESR yang cepat*.
- **Pelajaran untuk Flit:** cukup 2–3 preset layout bagus, jangan 100 opsi; web-panel sidebar cocok untuk pemantauan oleh agent; code signing sejak hari satu.

### Waterfox
- **Basis:** fork Firefox; MPL-2.0; aktif; ada versi Android native.
- **Fitur kunci:** **per-tab private** (tab privat tunggal di jendela normal — unik) • ODoH default • tree tabs otomatis • dukungan PWA.
- **Pelajaran untuk Flit:** per-tab private = pola untuk "tab sesi agent" terisolasi; jejak riset agent tampil alami sebagai tree tabs.

### Dia (The Browser Company / Atlassian)
- **Basis:** Chromium; closed source; aktif; macOS + Windows; gratis; integrasi Claude diumumkan.
- **Fitur kunci:** chat sintesis lintas-tab • Morning Brief • profil dengan **memori AI terpisah per profil** • Live Work • Splits • Decks • Organized Tabs.
- **Automasi:** tidak ada API publik.
- **Pelajaran untuk Flit:** konteks = tab yang terbuka = private-by-design; memori AI di-scope per profil (bukan global) — lebih aman.

### Comet (Perplexity)
- **Basis:** Chromium; closed; aktif; gratis sejak Okt 2025; semua platform.
- **Fitur kunci:** perintah browser bahasa alami • email assistant • agent belanja/checkout • sidecar Perplexity.
- **Automasi:** tidak ada API publik. Kejadian keamanan: **CometJacking** (LayerX) — Perplexity awalnya menolak laporan.
- **Pelajaran untuk Flit:** perintah NL untuk tab = dampak besar biaya kecil; tombol Pause per-aksi untuk assistant; proses disclosure keamanan serius sejak hari satu.

## 3. Baseline privasi — kutip setelannya, jangan tiru pengunciannya

### LibreWolf
- **Basis:** fork Firefox komunitas; MPL-2.0; aktif.
- **Fitur kunci:** uBlock Origin bundled + terkunci • RFP (resist fingerprinting) • DRM off • tanpa telemetri.
- **Pelajaran untuk Flit:** *defaults sebagai identitas* produk; salin setelan arkenfox; terlalu terkunci sebagai basis agent → buat profil privasi berlapis.

### Mullvad Browser
- **Basis:** Firefox + Tor Project (tanpa jaringan Tor); aktif.
- **Fitur kunci:** fingerprint seragam + letterboxing • uBO + NoScript • mode privat permanen.
- **Arsitektur rilis:** rebase ESR + backport.
- **Pelajaran untuk Flit:** arah kebalikan dari kita (privasi maks, fitur minimal); ambil level keamanan + template rilis + mode portabel untuk profil agent sekali-pakai.

## 4. Strategi engine — pantauan jangka panjang

### Servo
- **Basis:** engine Rust dari nol; Layout2020 + WebRender; didukung **Linux Foundation Europe**; Apache-2.0/MIT.
- **Status:** prototipe; servoshell sebagai demo; **WebView API di crates.io sejak Apr 2026 (LTS)**.
- **Pelajaran untuk Flit:** pemisahan engine/shell adalah arsitektur yang benar; governance netral + pendanaan mempercepat perkembangan.

### Ladybird
- **Basis:** engine dari nol (LibWeb/LibJS); non-profit 501(c)(3); BSD-2-Clause.
- **Status:** Alpha "akhir 2026" (macOS/Linux); fitur harian (session restore/history/downloads) baru selesai pertengahan 2026; sandbox sedang dikerjakan.
- **Pelajaran untuk Flit:** fitur harian yang membosankan = higienitas wajib sejak sprint awal; posisi *"just a browser"* membangun kepercayaan.

## 5. Fork Chromium / privasi — data dari riset awal (peneliti workflow kelompok ini gagal; profil lebih ringkas)

### Thorium (Alex313031)
- Fork Chromium dengan flag kompilator optimasi (PGO/LTO/SIMD/AVX) — saudara proyek Mercury. Klaim lebih cepat *(klaim vendor)*. Aktif; lisensi warisan Chromium (BSD-3).
- **Pelajaran:** sama seperti Mercury — klaim kecepatan dari flag kompilator itu murah; selalu periksa tanggal klaim (cek kelengkapan menandai ketegangan klaim AVX2 antara profil Zen & Thorium).

### Ungoogled-Chromium
- Chromium tanpa ketergantungan Google (domain substitution, akses webstore via precomputation). Pemeliharaan per-platform oleh relawan → rilis sering tertinggal dari Chromium.
- **Pelajaran:** debloating harus punya pipeline otomatis; jangan biarkan pengguna merasakan "patch lama".

### Helium (imputnet)
- Fork Chromium aktif & mutakhir yang memadukan ungoogled + patch privasi di atas basis Chromium terbaru; target browser harian tanpa Google.
- **Pelajaran:** bukti fork Chromium kecil bisa tetap mutakhir dengan automasi rebase — pola yang harus kita punya untuk mengikuti Evergreen.

### Cromite (uazo)
- Fork Chromium (penerus Bromite) dengan adblock bawaan, anti-fingerprint dasar (mis. kebocoran IP WebRTC), dan info "adblock aktif". Aktif; **patch berlisensi GPL-3.0**.
- **Pelajaran:** referensi teknik adblock-level-patch paling bersih; tapi **GPL — jangan salin kodenya ke inti proyek** (aturan di [NOTICES.md](NOTICES.md)).

## 6. Minimalis & GUI kecil

### Min
- **Basis:** Electron (Chromium); Apache-2.0; aktif.
- **Fitur kunci:** Tasks (grup tab otomatis-collapse per tugas/pencarian) • full-text search history on-device • adblock bawaan • UI minimal.
- **Pelajaran untuk Flit:** Tasks lebih ringan daripada workspaces penuh; Electron = tanpa kode native tapi bayar RAM — **bukan jalan kita**.

### qutebrowser → lihat §1 (hints mode = pelajaran utama).

### Falkon
- **Basis:** KDE + QtWebEngine; GPL; aktif.
- **Fitur kunci:** adblock default on • vertical tabs baru ditambah di 25.04.
- **Pelajaran untuk Flit:** layout = keputusan UI, bukan engine; "ringan" bisa tetap kaya fitur.

### GNOME Web (Epiphany)
- **Basis:** WebKitGTK; GPL; aktif.
- **Fitur kunci:** ITP (Intelligent Tracking Prevention) • content filter • sandbox bubblewrap — semuanya default on. Tanpa ekosistem ekstensi.
- **Pelajaran untuk Flit:** engine non-Chromium = penghematan RAM nyata; pola *security-by-default* layak ditiru.

### Pale Moon
- **Basis:** Goanna/UXP (fork Gecko tua), single-process; MPL-2.0 + batasan branding; aktif (35.0.3).
- **Fitur kunci:** add-on XUL, UI klasik, sangat ringan RAM berkat single-process.
- **Pelajaran untuk Flit:** single-process memang hemat RAM tapi bayar gap kompatibilitas web modern — **"ringan" kita tidak boleh memotong standar modern**; sikap eksplisit menolak AI (Pale Moon) = kontras bagus untuk posisi agent-first kita.

## 7. Anti-detect — referensi teknik, bukan basis

### Camoufox
- **Basis:** patch level C++ di Firefox (bukan injeksi JS); MPL-2.0; ~12k★; produksi = rilis lama (FF135), rebuild eksperimental di FF146; **source penuh publik sejak v146.0.1-beta.25**.
- **Fitur kunci:** BrowserForge (rotasi fingerprint dari **distribusi dunia-nyata**) • koherensi GeoIP (tz/locale/geo diturunkan dari IP proxy) • spoof WebRTC level protokol • font bundel + offset letter-spacing • Juggler/Playwright tertanam tersembunyi • HumanCursor (trayektori mouse level C++) • debloat ~200 MB.
- **Pelajaran untuk Flit:** spoof di level engine > injeksi JS; **identitas koheren + distribusi dunia-nyata > random**; jangan pakai sebagai basis — referensi teknik saja (sudah tercatat di §4 [FULL-FINAL.md](FULL-FINAL.md)).

## 8. 💀 Mati / sekarat — pelajaran kegagalan

### ChatGPT Atlas 💀
- OpenAI; macOS-only; **ditutup 9 Agu 2026** (terverifikasi web).
- Saat hidup: agent internal saja — tanpa API/CDP eksternal; memori server-side 30 hari; vuln **"tainted memories"** (CSRF ke memori).
- **Pelajaran:** API terstruktur yang diizinkan > simulasi klik; memori = attack surface (uji CSRF-ke-memori); sediakan CDP/MCP eksternal + izin eksplisit sejak awal — kebalikan dari Atlas.

### Verso 💀
- Shell tipis di atas Servo; **diarsipkan 8 Okt 2025** (terverifikasi web); kontribusi terserap ke Servo/tauri-runtime-verso.
- **Penyebab mati:** shell tipis di atas engine eksternal yang bergerak cepat + tanpa pendanaan.
- **Pelajaran:** buffer terhadap engine eksternal yang cepat berubah; bangun agar kerja tetap bernilai bila produk mati.

### Mercury 💀 (praktis mati — inspirasi asal proyek ini)
- Fork Firefox oleh Alex313031 (PGO/LTO/SIMD); **membeku di Firefox 129 (pertengahan 2024) — tertinggal ~2 tahun**; installer unsigned sering ditandai antivirus; satu maintainer.
- Tetap motivasi asal: RAM-nya hemat berkat **model proses Gecko** (proses konten terbatas), bukan trik ajaib — analisis ini yang melahirkan pilihan WebView2 + lifecycle tab agresif.
- **Pelajaran:** klaim kecepatan dari flag kompilator itu murah; risiko single-maintainer → butuh pipeline update + code signing sejak hari satu.

### Konteks pasar: Arc
- The Browser Company mengumumkan **sunset Arc 22 Des 2026** (fokus penuh ke Dia) → slot pasar "browser berbentuk baru" terbuka; pengguna Arc mencari rumah baru — momen posisi yang baik untuk produk baru.

## 9. Backlog riset — belum masuk katalog (urut prioritas)

1. **BrowserOS** — kompetitor open-source *agentic* terdekat dengan kita (agent "neo" dengan MCP + Claude/Codex). **Paling penting untuk di-riset.**
2. Tor Browser — otoritas anti-fingerprint.
3. Nyxt — Common Lisp, kontrol programatik penuh — filosofi terdekat dengan konsep agent-first kita.
4. Brave, Vivaldi — baseline fitur mainstream.
5. Opera Neon ($19,99/bln), Fellou, Genspark — gelombang AI browser 2026.
6. Arc — pelajaran sunset (lihat §8).
7. NetSurf, Dillo — ultraringan (batas bawah kompatibilitas).
8. Ghostery Private Browser, IceCat, Supermium (Chromium utk Windows lama) — minor.

## 10. Catatan penyusun (dari cek kelengkapan)

- Urutan baca disarankan: automasi → UX → privasi → engine → pelajaran mati.
- Semua benchmark "× lebih cepat/hemat" di dokumen ini = klaim vendor kecuali dinyatakan lain.
- Jangan menyajikan browser anti-detect sebagai rekomendasi privasi harian — kategorinya automasi.
- Periksa tanggal klaim kompilator sebelum mengutip (Zen vs Thorium — ketegangan AVX2 terdeteksi).
- Klaim yang sudah ditandai kedaluwarsa dan dibuang: catatan lama "Atlas mengintersepsi Canary Jan 2026" (Atlas sudah mati sebelumnya).
