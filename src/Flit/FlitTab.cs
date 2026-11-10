namespace Flit;

// Satu tab = satu model + (lazy) satu kontrol WebView2.
// Tab latar otomatis ditidurkan (TrySuspend) setelah sekian detik — thesis RAM inti.
public class FlitTab
{
    public string Title = "Tab Baru";
    public string Url = string.Empty;
    public Microsoft.Web.WebView2.WinForms.WebView2? Web;

    public bool IsLoading;
    public bool IsSleeping;

    // Kapan tab latar selesai memuat (mulai hitung mundur menuju tidur).
    // DateTime.MaxValue = sedang aktif / belum selesai load — tidak boleh tidur.
    public DateTime BackgroundReadyAt = DateTime.MaxValue;

    // M5 deep-discard: kapan tab mulai tidur (hitung mundur menuju dibuang) + URL pemulihan.
    public DateTime SleepingSince = DateTime.MaxValue;
    public string RestoreUrl = string.Empty;
}
