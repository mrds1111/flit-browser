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
    public string UadPlatformVersion = "";  // getHighEntropyValues("platformVersion") — kosong = teruskan native
    public (string Brand, string Version)[] UadBrands =
        { ("Not-A.Brand", "99"), ("Chromium", "131"), ("Google Chrome", "131") };
    public bool UadMobile;
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
            uadVer = p.UadPlatformVersion,
            brands = p.UadBrands.Select(b => new { brand = b.Brand, version = b.Version }).ToArray(),
            mobile = p.UadMobile,
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
      // userAgentData: properti platform/brands adalah getter milik prototype —
      // menimpa di objeknya GAGAL SENYAP (terbukti uji M6: masih "Windows" saat UA Mac).
      // Solusi: bangun replika dan pasang sebagai properti own di navigator (menutupi getter prototype).
      // Brands/mobile/platformVersion diturunkan dari UA — brands asli ("Microsoft Edge WebView2")
      // membocorkan mesin saat UA menyamar jadi Chrome/Mac (pelajaran pola Camoufox: satu cerita).
      var fake = {
        brands: I.brands,
        mobile: I.mobile,
        platform: I.uad,
        toJSON: function(){ return {brands:this.brands, mobile:this.mobile, platform:this.platform}; },
        getHighEntropyValues: function(hints){
          return Promise.resolve({platform: I.uad, platformVersion: I.uadVer || '0.0.0', architecture: 'x86', model: '', uaFullVersion: '', fullVersionList: I.brands});
        }
      };
      def(navigator, 'userAgentData', fake);
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
