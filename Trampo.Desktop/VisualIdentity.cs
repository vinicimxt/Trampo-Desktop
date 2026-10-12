namespace Trampo.Desktop;

internal static class VisualIdentity
{
    private static readonly Lazy<Icon> IconResource = new(() =>
    {
        using var stream = typeof(VisualIdentity).Assembly.GetManifestResourceStream(
            "Trampo.Desktop.Assets.Trampo.ico")
            ?? throw new InvalidOperationException("Ícone TRAMPO não foi incorporado ao aplicativo.");
        return new Icon(stream);
    });

    private static readonly Lazy<Image> LogoResource = new(() =>
    {
        using var stream = typeof(VisualIdentity).Assembly.GetManifestResourceStream(
            "Trampo.Desktop.Assets.Trampo-Logo.png")
            ?? throw new InvalidOperationException("Logo TRAMPO não foi incorporada ao aplicativo.");
        using var image = Image.FromStream(stream);
        return new Bitmap(image);
    });

    public static Icon Icon => IconResource.Value;
    public static Image Logo => LogoResource.Value;

    public static void Apply(Form form) => form.Icon = Icon;
}
