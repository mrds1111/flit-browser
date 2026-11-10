# NOTICES — Kebijakan Kredit & Lisensi Pihak Ketiga

> Proyek ini akan di-publish open-source di GitHub. Pemilik mengizinkan fork/penurunan komponen pihak ketiga dengan syarat: **kredit + kepatuhan lisensi asal** (kredit saja tidak cukup — lihat tabel).
> Lisensi proyek (**TERKUNCI 2026-11-11**): **MPL-2.0** — per-file copyleft, kompatibel dengan dependensi BSD/Apache/MPL, tanpa efek "menular" seperti GPL. Kriteria pemilik: aman dari tuntutan hukum — MPL-2.0 memenuhinya: battle-tested via Firefox (12+ tahun), ada *patent grant* eksplisit, dan *disclaimer of warranty* (pengguna tidak bisa menuntut atas kerusakan/masalah software).

## 1. Kewajiban per basis yang berpotensi di-fork/dipakai

| Basis | Lisensi | Kewajiban saat fork/memakai | Catatan trademark |
|---|---|---|---|
| Firefox / Mercury / Camoufox | MPL-2.0 | Pertahankan notice lisensi per-file; dokumentasikan semua perubahan; bagikan file yang dimodifikasi | ⚠️ Fork Firefox **wajib rename** — dilarang pakai branding Firefox/Mozilla (preseden sah: Mercury, Waterfox, Zen) |
| Chromium | BSD-3-Clause + lainnya | Pertahankan copyright notice dalam source | Branding Chrome/Chromium dibatasi aturan Chromium |
| Servo | Apache-2.0 | Sertakan NOTICE + lisensi; nyatakan perubahan | — |
| Ladybird | BSD-2-Clause | Pertahankan notice | — |
| Lightpanda | (cek repo sebelum pakai) | Sesuai lisensinya | — |
| EasyList / EasyPrivacy | GPL-3.0 / CC BY-SA 3.0 | Pilih satu jalur lisensi + **atribusi** ("The EasyList authors") | Jalur CC disarankan agar tidak menularkan GPL ke bundel |
| ABPindo | GPL-3.0 / CC BY-SA 3.0 | Atribusi ("ABPindo") | Sama — jalur CC |
| DistillNET (kandidat parser) | (cek saat diadopsi) | Sesuai lisensinya | Verifikasi sebelum M1 |

**Aturan kunci:** komponen **GPL menular** ke keseluruhan karya. Jangan bundel kode GPL (mis. uBlock Origin) ke inti proyek — keputusan terkunci di [FULL-FINAL.md](FULL-FINAL.md).

## 2. THIRD-PARTY NOTICES (komponen yang benar-benar dipakai — M3)

Flit tidak membundel file daftar filter di repositori; daftar diunduh saat runtime dari sumber resminya dan di-cache lokal. Penggunaan datanya tetap mengikuti jalur **CC BY-SA 3.0** (bukan jalur GPL) sesuai keputusan terkunci:

| Komponen | Sumber | Lisensi | Perubahan kami | Kredit |
|---|---|---|---|---|
| EasyList | https://easylist.to/easylist/easylist.txt | CC BY-SA 3.0 | tidak dimodifikasi (di-parse subset aturan) | The EasyList authors |
| EasyPrivacy | https://easylist.to/easylist/easyprivacy.txt | CC BY-SA 3.0 | tidak dimodifikasi (di-parse subset aturan) | The EasyList authors |
| ABPindo | https://github.com/ABPindo/indonesianadblockrules | CC BY-SA 3.0 | tidak dimodifikasi (di-parse subset aturan) | ABPindo |
| Microsoft WebView2 SDK | nuget.org (Microsoft.Web.WebView2) | Ketentuan lisensi Microsoft (redistributable) | tidak dimodifikasi | Microsoft |

Kredit daftar filter juga ditampilkan di menu **Menu → Tentang**.

Catatan: referensi di [REFERENCES.md](REFERENCES.md) yang hanya *dipelajari* (bukan kode/aset yang diikutkan) **tidak** mewajibkan entri di sini — entri wajib hanya untuk kode/aset yang benar-benar dibundel atau diturunkan.

## 3. To-do lisensi

- [x] Konfirmasi pilihan lisensi proyek → **MPL-2.0, terkunci 2026-11-11**
- [ ] Tambahkan file `LICENSE` **dengan teks resmi** dari mozilla.org (jangan diketik ulang dari ingatan)
- [ ] Buat `NOTICE`/header file standar saat kode pertama ditulis
- [ ] Jika suatu saat fork Gecko untuk "edisi stealth": pilih nama baru (bukan turunan merek Firefox), dokumentasikan patch di repo publik
