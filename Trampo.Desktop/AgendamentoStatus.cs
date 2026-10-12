namespace Trampo.Desktop;

internal static class AgendamentoStatus
{
    public static string Text(string status) => status switch
    {
        "AguardandoCliente" => "Aguardando cliente",
        "CanceladoCliente" => "Cancelado pelo cliente",
        "CanceladoProfissional" => "Cancelado pelo profissional",
        _ => status
    };

    public static Color StatusColor(string status) => status switch
    {
        "Pendente" => System.Drawing.Color.FromArgb(154, 98, 22),
        "Confirmado" => System.Drawing.Color.FromArgb(49, 85, 166),
        "AguardandoCliente" => System.Drawing.Color.FromArgb(99, 73, 165),
        "Finalizado" => System.Drawing.Color.FromArgb(35, 119, 84),
        "Cancelado" or "CanceladoCliente" or "CanceladoProfissional" =>
            System.Drawing.Color.FromArgb(171, 48, 58),
        _ => System.Drawing.Color.FromArgb(53, 57, 78)
    };
}

