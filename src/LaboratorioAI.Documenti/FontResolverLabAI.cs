using System.Reflection;
using PdfSharp.Fonts;

namespace LaboratorioAI.Documenti;

/// <summary>
/// Font resolver per PDFsharp (build cross-platform, priva di font di sistema). Fornisce i font da
/// risorse incorporate (Liberation Sans, metricamente compatibile con Arial), così la generazione
/// dei PDF funziona su qualsiasi server anche senza font installati. Qualunque famiglia richiesta
/// (Arial, Montserrat, ecc.) viene mappata su Liberation Sans nello stile corretto.
/// </summary>
public sealed class FontResolverLabAI : IFontResolver
{
    private static readonly object _lock = new();
    private static bool _registrato;

    /// <summary>Registra il resolver una sola volta (idempotente). Da chiamare prima di generare un PDF.</summary>
    public static void Registra()
    {
        if (_registrato) return;
        lock (_lock)
        {
            if (_registrato) return;
            GlobalFontSettings.FontResolver ??= new FontResolverLabAI();
            _registrato = true;
        }
    }

    public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        var face = (isBold, isItalic) switch
        {
            (true, true) => "LiberationSans-BoldItalic",
            (true, false) => "LiberationSans-Bold",
            (false, true) => "LiberationSans-Italic",
            _ => "LiberationSans-Regular",
        };
        return new FontResolverInfo(face);
    }

    public byte[]? GetFont(string faceName)
    {
        var asm = typeof(FontResolverLabAI).Assembly;
        var nome = $"LaboratorioAI.Documenti.Fonts.{faceName}.ttf";
        using var s = asm.GetManifestResourceStream(nome)
            ?? throw new InvalidOperationException($"Risorsa font mancante: {nome}");
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }
}
