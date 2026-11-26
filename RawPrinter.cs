using System.Drawing.Printing;
using System.Runtime.InteropServices;

namespace LivrePrintAgent;

/// <summary>
/// Envia dados crus (RAW) direto para uma impressora pelo spooler do Windows, sem diálogo.
/// Serve para ZPL (Zebra) e ESC/POS (térmicas, como a Bematech). Cada chamada é um trabalho de impressão,
/// então a impressora com guilhotina corta ao fim de cada um.
/// </summary>
public sealed class PrinterNotFoundException : Exception
{
    public PrinterNotFoundException(string printerName)
        : base($"Impressora \"{printerName}\" não encontrada neste computador.")
    {
    }
}

public static class RawPrinter
{
    // O Windows pode demorar para listar as impressoras (principalmente com impressoras de rede),
    // então a lista fica em cache por alguns segundos. A impressão em si não depende dela.
    private static readonly object CacheGate = new();
    private static IReadOnlyList<string>? _cachedPrinters;
    private static DateTime _cachedAt = DateTime.MinValue;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(10);

    private const int ErrorInvalidPrinterName = 1801;

    public static IReadOnlyList<string> ListPrinters()
    {
        lock (CacheGate)
        {
            if (_cachedPrinters is not null && DateTime.UtcNow - _cachedAt < CacheTtl)
            {
                return _cachedPrinters;
            }

            _cachedPrinters = LoadPrinters();
            _cachedAt = DateTime.UtcNow;
            return _cachedPrinters;
        }
    }

    private static IReadOnlyList<string> LoadPrinters()
    {
        var names = new List<string>();
        foreach (string name in PrinterSettings.InstalledPrinters)
        {
            names.Add(name);
        }

        names.Sort(StringComparer.CurrentCultureIgnoreCase);
        return names;
    }

    public static string? DefaultPrinter()
    {
        var settings = new PrinterSettings();
        return settings.IsValid ? settings.PrinterName : null;
    }

    public static void Send(string printerName, byte[] data, string documentName = "Livre Core")
    {
        if (!OpenPrinter(printerName, out var printer, IntPtr.Zero))
        {
            var error = Marshal.GetLastWin32Error();

            // Não precisa listar as impressoras antes: o próprio Windows diz se o nome não existe.
            if (error == ErrorInvalidPrinterName)
            {
                throw new PrinterNotFoundException(printerName);
            }

            throw new InvalidOperationException($"Não foi possível abrir a impressora \"{printerName}\" (erro {error}).");
        }

        var buffer = IntPtr.Zero;
        var docStarted = false;
        var pageStarted = false;

        try
        {
            var docInfo = new DocInfo { DocName = documentName, OutputFile = null, DataType = "RAW" };

            if (!StartDocPrinter(printer, 1, ref docInfo))
            {
                throw new InvalidOperationException($"A impressora recusou o trabalho (erro {Marshal.GetLastWin32Error()}).");
            }
            docStarted = true;

            if (!StartPagePrinter(printer))
            {
                throw new InvalidOperationException($"A impressora recusou a página (erro {Marshal.GetLastWin32Error()}).");
            }
            pageStarted = true;

            buffer = Marshal.AllocCoTaskMem(data.Length);
            Marshal.Copy(data, 0, buffer, data.Length);

            if (!WritePrinter(printer, buffer, data.Length, out var written) || written != data.Length)
            {
                throw new InvalidOperationException($"Falha ao enviar os dados para a impressora (erro {Marshal.GetLastWin32Error()}).");
            }
        }
        finally
        {
            if (buffer != IntPtr.Zero) Marshal.FreeCoTaskMem(buffer);
            if (pageStarted) EndPagePrinter(printer);
            if (docStarted) EndDocPrinter(printer);
            ClosePrinter(printer);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DocInfo
    {
        [MarshalAs(UnmanagedType.LPWStr)] public string DocName;
        [MarshalAs(UnmanagedType.LPWStr)] public string? OutputFile;
        [MarshalAs(UnmanagedType.LPWStr)] public string DataType;
    }

    [DllImport("winspool.drv", EntryPoint = "OpenPrinterW", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern bool OpenPrinter(string printerName, out IntPtr printer, IntPtr defaults);

    [DllImport("winspool.drv", SetLastError = true, ExactSpelling = true)]
    private static extern bool ClosePrinter(IntPtr printer);

    [DllImport("winspool.drv", EntryPoint = "StartDocPrinterW", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern bool StartDocPrinter(IntPtr printer, int level, ref DocInfo docInfo);

    [DllImport("winspool.drv", SetLastError = true, ExactSpelling = true)]
    private static extern bool EndDocPrinter(IntPtr printer);

    [DllImport("winspool.drv", SetLastError = true, ExactSpelling = true)]
    private static extern bool StartPagePrinter(IntPtr printer);

    [DllImport("winspool.drv", SetLastError = true, ExactSpelling = true)]
    private static extern bool EndPagePrinter(IntPtr printer);

    [DllImport("winspool.drv", SetLastError = true, ExactSpelling = true)]
    private static extern bool WritePrinter(IntPtr printer, IntPtr bytes, int count, out int written);
}
