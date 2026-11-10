# Flit Browser

> *One browser, both of us.*

Browser Windows ringan untuk manusia **dan** AI agent — shell C# .NET 10 + WinForms di atas WebView2 (Chromium Evergreen).

**Status: M4 — stealth S2 (identitas koheren) + profil & proxy + gerbang confirm** (bukti uji: [M4-RESULT.md](M4-RESULT.md), adblock: [M3-RESULT.md](M3-RESULT.md), agent: [M2-RESULT.md](M2-RESULT.md), benchmark: [M1-RESULT.md](M1-RESULT.md))

## Untuk AI agent (MCP)

Flit bisa dikendalikan agent apa pun lewat MCP — headless, 11 tools, izin per-domain, log aktivitas. Lihat [MCP.md](MCP.md).

```bash
claude mcp add flit -- C:\path\ke\Flit.exe --mcp
```

| Dokumen | Isi |
|---|---|
| [FULL-FINAL.md](FULL-FINAL.md) | Spesifikasi final v1.0 |
| [MCP.md](MCP.md) | Server MCP: tools, keamanan agent, konfigurasi |
| [CONCEPT.md](CONCEPT.md) | Konsep & keputusan fitur (masuk/dibuang/ditunda) |
| [ADBLOCK-DESIGN.md](ADBLOCK-DESIGN.md) | Desain adblock terverifikasi |
| [BROWSERS.md](BROWSERS.md) | Katalog referensi 21 browser |
| [REFERENCES.md](REFERENCES.md) | Referensi & keputusan lolos/tolak |
| [NOTICES.md](NOTICES.md) | Kredit & kebijakan lisensi pihak ketiga |
| [M0-RESULT.md](M0-RESULT.md) / [M1-RESULT.md](M1-RESULT.md) / [M2-RESULT.md](M2-RESULT.md) / [M3-RESULT.md](M3-RESULT.md) / [M4-RESULT.md](M4-RESULT.md) | Hasil & bukti uji per milestone |

## Lisensi

MPL-2.0 — lihat [LICENSE](LICENSE) dan [NOTICES.md](NOTICES.md).

*Dokumen proyek ditulis dalam Bahasa Indonesia.*
