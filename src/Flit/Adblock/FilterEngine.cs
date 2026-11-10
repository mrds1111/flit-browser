using System.Text.RegularExpressions;

namespace Flit.Adblock;

/// <summary>
/// Engine filter ABP subset murni C# (M3) — mengikuti ADBLOCK-DESIGN.md.
/// Prinsip performa: handler WebResourceRequested jalan di UI thread dan MEMBLOKIR
/// network → pencocokan harus murah: aturan ||domain^ masuk dictionary O(1) per domain.
///
/// Dukungan v1 (jujur):
///   ✅ ||domain^ / ||domain/jalur  ( mayoritas aturan daftar populer )
///   ✅ |prefix  • pola literal berisi * dan ^ (di path, dicari hanya bila host cocok)
///   ✅ @@ pengecualian (network) • opsi $type, $third-party, $first-party, $important
///   ✅ Cosmetic ## / ### (global & per-domain) + pengecualian #@#
///   ❌ $domain= , $redirect= , $csp= , scriptlet #$# , procedural #?# , regex /…/  → dilewati
/// </summary>
public class FilterEngine
{
    private sealed class NetRule
    {
        public string? PathLiteral;      // null = host saja; else harus jadi awalan path
        public Regex? PathRegex;         // bila path punya * / ^
        public bool ThirdPartyOnly;
        public bool FirstPartyOnly;
        public bool Important;
        public HashSet<string>? Types;   // null = semua tipe
    }

    private sealed class PrefixRule   // |http… (tanpa anchor ||)
    {
        public string Literal;
        public bool Exception;
        public HashSet<string>? Types;
    }

    private readonly Dictionary<string, List<NetRule>> _block = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<NetRule>> _except = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<PrefixRule> _prefix = new();
    private readonly List<string> _cosmeticGlobal = new();
    private readonly Dictionary<string, List<string>> _cosmeticByDomain = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _unhide = new(StringComparer.Ordinal); // selektor yang dikecualikan

    public int NetRules { get; private set; }
    public int CosmeticRules { get; private set; }

    private static readonly string[] KnownTypes =
    {
        "script", "image", "stylesheet", "object", "xmlhttprequest", "subdocument",
        "document", "media", "font", "other", "ping", "websocket",
    };

    /// <summary>Pars satu file daftar filter (ABP format). Aman dipanggil berulang utk beberapa daftar.</summary>
    public void ParseFile(string path)
    {
        using var sr = new StreamReader(path);
        string? line;
        while ((line = sr.ReadLine()) is not null)
            ParseLine(line);
    }

    public void ParseLine(string raw)
    {
        var line = raw.Trim();
        if (line.Length == 0) return;
        var c0 = line[0];
        if (c0 == '!' || c0 == '[') return; // komentar / header

        // ---- cosmetic: ## (tapi bukan #$# #?# #@?# dst.) ----
        int hIdx = line.IndexOf("##", StringComparison.Ordinal);
        if (hIdx >= 0 && hIdx + 2 < line.Length)
        {
            var after = line.Substring(hIdx + 2, 2);
            if (after is "?#" or "$#") return; // procedural / scriptlet → lewati
            var domainsPart = line[..hIdx];
            var selector = line[(hIdx + 2)..].Trim();
            if (selector.Length == 0) return;

            var isException = domainsPart.Contains("#@#", StringComparison.Ordinal);
            if (isException)
            {
                domainsPart = domainsPart.Replace("#@#", "");
                _unhide.Add(selector);
                return;
            }
            CosmeticRules++;

            if (domainsPart.Length == 0)
            {
                _cosmeticGlobal.Add(selector);
                return;
            }
            bool hasExclude = false;
            foreach (var d in domainsPart.Split(','))
            {
                var t = d.Trim();
                if (t.StartsWith('~')) { hasExclude = true; break; }
            }
            if (hasExclude) return; // v1: aturan dengan pengecualian domain → dilewati (hindari salah blok)
            foreach (var d in domainsPart.Split(','))
            {
                var t = d.Trim();
                if (t.Length == 0) continue;
                if (!_cosmeticByDomain.TryGetValue(t, out var list))
                    _cosmeticByDomain[t] = list = new List<string>();
                list.Add(selector);
            }
            return;
        }

        // ---- network rule ----
        bool exception = line.StartsWith("@@", StringComparison.Ordinal);
        if (exception) line = line[2..];

        HashSet<string>? types = null;
        bool thirdPartyOnly = false, firstPartyOnly = false, important = false;

        int dollar = line.LastIndexOf('$');
        if (dollar >= 0 && dollar < line.Length - 1)
        {
            var opts = line[(dollar + 1)..];
            // rule regex /…/$option terlihat sama — cek dulu pola regex asli → lewati
            var body = line[..dollar];
            if (body.Length > 2 && body[0] == '/' && body[^1] == '/') return;
            foreach (var optRaw in opts.Split(','))
            {
                var opt = optRaw.Trim();
                if (opt.Length == 0) continue;
                var neg = opt.StartsWith('~');
                var name = neg ? opt[1..] : opt;
                if (name.StartsWith("domain=", StringComparison.OrdinalIgnoreCase)) return; // dilewati v1
                if (name.StartsWith("redirect", StringComparison.OrdinalIgnoreCase)) return;
                if (name.StartsWith("csp", StringComparison.OrdinalIgnoreCase)) return;
                switch (name)
                {
                    case "third-party": thirdPartyOnly = !neg; firstPartyOnly = false; break;
                    case "first-party": firstPartyOnly = !neg; thirdPartyOnly = false; break;
                    case "important": important = true; break;
                    default:
                        if (KnownTypes.Contains(name))
                        {
                            types ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            if (neg) types.Remove(name); else types.Add(name);
                        }
                        else return; // opsi tak dikenal → jangan terapkan (hindari salah blok)
                        break;
                }
            }
            line = body;
        }

        // |prefix (bukan ||) → bandingkan awalan URL
        if (line.StartsWith("|") && !line.StartsWith("||", StringComparison.Ordinal))
        {
            var lit = line.TrimStart('|').TrimEnd('|');
            if (lit.Contains('*')) return;
            _prefix.Add(new PrefixRule { Literal = lit, Exception = exception, Types = types });
            NetRules++;
            return;
        }

        if (!line.StartsWith("||", StringComparison.Ordinal))
            return; // pola generik tanpa anchor → dilewati v1 (lihat catatan M3-RESULT)

        // ||host^/path*
        var rest = line[2..];
        int cut = 0;
        while (cut < rest.Length && (char.IsLetterOrDigit(rest[cut]) || rest[cut] == '.' || rest[cut] == '-'))
            cut++;
        var host = rest[..cut].TrimEnd('.');
        if (host.Length == 0 || host.Contains('*')) return;

        var path = rest[cut..];
        var rule = new NetRule { Types = types, ThirdPartyOnly = thirdPartyOnly, FirstPartyOnly = firstPartyOnly, Important = important };
        if (path.Length == 0 || path == "^" || path.StartsWith("^$", StringComparison.Ordinal))
        {
            rule.PathLiteral = null; // host saja
        }
        else
        {
            path = path.StartsWith('^') ? path[1..] : path;
            if (path.Length == 0) rule.PathLiteral = null;
            else if (!path.Contains('*') && !path.Contains('^')) rule.PathLiteral = path;
            else rule.PathRegex = CompilePath(path);
        }

        var bucket = exception ? _except : _block;
        if (!bucket.TryGetValue(host, out var rules))
            bucket[host] = rules = new List<NetRule>();
        rules.Add(rule);
        NetRules++;
    }

    private static Regex CompilePath(string path)
    {
        var sb = new System.Text.StringBuilder("^");
        foreach (var ch in path)
        {
            if (ch == '*') sb.Append(".*");
            else if (ch == '^') sb.Append("([/?#;:=&?]|$)");
            else sb.Append(Regex.Escape(ch.ToString()));
        }
        return new Regex(sb.ToString(), RegexOptions.Compiled | RegexOptions.IgnoreCase);
    }

    /// <summary>Keputusan akhir: blokir atau tidak. refHost boleh null (navigasi langsung).
    /// Urutan: $important > pengecualian (@@, |…|) > blokir.</summary>
    public bool ShouldBlock(string url, string? typeName, string? refHost)
    {
        Uri uri;
        try { uri = new Uri(url); } catch { return false; }
        var host = uri.Host.ToLowerInvariant();
        if (host.Length == 0) return false;
        var pathQ = uri.PathAndQuery;
        var thirdParty = refHost is not null && !string.Equals(refHost, host, StringComparison.OrdinalIgnoreCase);

        if (MatchBucket(_block, host, pathQ, typeName, refHost, thirdParty, importantOnly: true))
            return true; // $important menimpa pengecualian
        if (MatchBucket(_except, host, pathQ, typeName, refHost, thirdParty, importantOnly: false))
            return false;
        if (PrefixAllows(url, typeName))
            return false;
        return MatchBucket(_block, host, pathQ, typeName, refHost, thirdParty, importantOnly: false)
               || PrefixBlocks(url, typeName);
    }

    private bool MatchBucket(Dictionary<string, List<NetRule>> bucket, string host, string pathQ,
        string? typeName, string? refHost, bool thirdParty, bool importantOnly)
    {
        // cek host + seluruh domain induk: a.b.c.com → a.b.c.com, b.c.com, c.com, com
        int start = 0;
        while (true)
        {
            var key = host[start..];
            if (bucket.TryGetValue(key, out var rules))
            {
                foreach (var r in rules)
                {
                    if (importantOnly && !r.Important) continue;
                    if (r.ThirdPartyOnly && !thirdParty) continue;
                    if (r.FirstPartyOnly && thirdParty) continue;
                    if (r.Types is not null && typeName is not null && !r.Types.Contains(typeName)) continue;
                    if (r.PathLiteral is null) return true;
                    if (r.PathRegex is not null) { if (r.PathRegex.IsMatch(pathQ)) return true; }
                    else if (pathQ.StartsWith(r.PathLiteral, StringComparison.OrdinalIgnoreCase)) return true;
                }
            }
            int dot = host.IndexOf('.', start);
            if (dot < 0) break;
            start = dot + 1;
        }
        // aturan |prefix (pengecualian & blokir digabung di sini utk kesederhanaan)
        return false;
    }

    public bool PrefixAllows(string url, string? typeName)
    {
        foreach (var p in _prefix)
        {
            if (p.Exception && url.StartsWith(p.Literal, StringComparison.OrdinalIgnoreCase))
            {
                if (p.Types is null || typeName is null || p.Types.Contains(typeName)) return true;
            }
        }
        return false;
    }

    public bool PrefixBlocks(string url, string? typeName)
    {
        foreach (var p in _prefix)
        {
            if (!p.Exception && url.StartsWith(p.Literal, StringComparison.OrdinalIgnoreCase))
            {
                if (p.Types is null || typeName is null || p.Types.Contains(typeName)) return true;
            }
        }
        return false;
    }

    /// <summary>Kumpulan selektor CSS utk sebuah host (global + rantai domain), minus pengecualian #@#.</summary>
    public List<string> CosmeticSelectorsFor(string host)
    {
        List<string> outSel = new();
        foreach (var s in _cosmeticGlobal)
            if (!_unhide.Contains(s)) outSel.Add(s);

        int start = 0;
        while (true)
        {
            var key = host[start..];
            if (_cosmeticByDomain.TryGetValue(key, out var list))
                foreach (var s in list)
                    if (!_unhide.Contains(s)) outSel.Add(s);
            int dot = host.IndexOf('.', start);
            if (dot < 0) break;
            start = dot + 1;
        }
        return outSel;
    }

    // Akses utk pembangun script injeksi (sudah difilter _unhide)
    public IEnumerable<string> CosmeticSelectorsGlobal()
    {
        foreach (var s in _cosmeticGlobal)
            if (!_unhide.Contains(s)) yield return s;
    }

    public IEnumerable<KeyValuePair<string, List<string>>> CosmeticByDomain()
    {
        foreach (var (domain, list) in _cosmeticByDomain)
        {
            var filtered = new List<string>(list.Count);
            foreach (var s in list)
                if (!_unhide.Contains(s)) filtered.Add(s);
            if (filtered.Count > 0) yield return new(domain, filtered);
        }
    }
}
