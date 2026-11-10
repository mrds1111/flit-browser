using System.Text.Json;

namespace Flit.Stealth;

/// <summary>
/// Identitas koheren S2 (M4): semua jejak JS menyatu jadi satu cerita —
/// UA, platform, bahasa, zona waktu, jumlah core/RAM.
/// Aktif HANYA saat UserAgent kustom dipasang; tanpa itu identitas native WebView2
/// sudah koheren dan Flit tidak menyentuh apa pun (prinsip: jangan nambah jejak).
/// </summary>
public sealed class IdentityProfile
{
    public string UserAgent = "";
    public string Platform = "Win32";       // navigator.platform
    public string UadPlatform = "Windows";  // navigator.userAgentData.platform
    public string Language = "id-ID";
    public string[] Languages = { "id-ID", "en-US" };
    public string Timezone = "";            // IANA; kosong = biarkan native
    public int TimezoneOffsetMinutes;       // hasil Date.prototype.getTimezoneOffset()
    public int HardwareConcurrency = 8;
    public int DeviceMemory = 8;            // GiB (Chrome melaporkan 2/4/8)
}

public static class Identity
{
    /// <summary>Script S2 kecil (&lt;3 KB) untuk AddScriptToExecuteOnDocumentCreatedAsync.
    /// Nilai dilewatkan sebagai JSON tunggal agar aman dari karakter aneh di config.</summary>
    public static string BuildScript(IdentityProfile p)
    {
        var payload = JsonSerializer.Serialize(new
        {
            ua = p.UserAgent,
            platform = p.Platform,
            uad = p.UadPlatform,
            lang = p.Language,
            langs = p.Languages,
            tz = p.Timezone,
            tzOff = p.TimezoneOffsetMinutes,
            cores = p.HardwareConcurrency,
            mem = p.DeviceMemory,
        });
        return """
(function(){
  try{
    var I = %P%;
    var def = function(obj, name, val){
      try{ Object.defineProperty(obj, name, { get: function(){ return val; }, configurable: true }); }catch(e){}
    };
    def(navigator, 'userAgent', I.ua);
    def(navigator, 'platform', I.platform);
    def(navigator, 'language', I.lang);
    def(navigator, 'languages', I.langs);
    def(navigator, 'hardwareConcurrency', I.cores);
    if(I.mem > 0) def(navigator, 'deviceMemory', I.mem);
    try{
      var uad = navigator.userAgentData;
      if(uad){
        def(uad, 'platform', I.uad);
        var orig = uad.getHighEntropyValues;
        if(orig){
          uad.getHighEntropyValues = function(hints){
            return orig.call(uad, hints).then(function(d){
              try{ d.platform = I.uad; }catch(e){}
              return d;
            });
          };
        }
      }
    }catch(e){}
    if(I.tz){
      try{
        var TZ = I.tz, OFF = I.tzOff;
        var ro = Intl.DateTimeFormat.prototype.resolvedOptions;
        Intl.DateTimeFormat.prototype.resolvedOptions = function(){
          var o = ro.call(this); o.timeZone = TZ; return o;
        };
        Date.prototype.getTimezoneOffset = function(){ return OFF; };
      }catch(e){}
    }
  }catch(e){}
})()
""".Replace("%P%", payload);
    }
}
