using Trampo.Desktop.Api;
using Trampo.Desktop.Session;

namespace Trampo.Desktop;

internal sealed class ClienteMainForm : Form
{
    private readonly UserSession _session;
    private readonly TrampoApiClient _api;
    private readonly FlowLayoutPanel _menu;
    private readonly FlowLayoutPanel _quick;

    internal ClienteMainForm(UserSession session, TrampoApiClient api)
    {
        if (!string.Equals(session.Perfil, "cliente", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("O painel exige perfil Cliente.", nameof(session));
        _session = session;
        _api = api;
        Text = "TRAMPO | Painel do cliente";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(860, 520);
        ClientSize = new Size(1000, 620);
        BackColor = Color.FromArgb(247, 248, 252);
        Font = new Font("Segoe UI", 10);
        VisualIdentity.Apply(this);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Controls.Add(layout);
        var sidebar = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(27, 25, 65) };
        layout.Controls.Add(sidebar, 0, 0);
        sidebar.Controls.Add(new PictureBox { Image = VisualIdentity.Logo, SizeMode = PictureBoxSizeMode.Zoom,
            Bounds = new Rectangle(24, 24, 56, 56), AccessibleName = "Logo TRAMPO" });
        sidebar.Controls.Add(new Label { Text = "TRAMPO", ForeColor = Color.White,
            Font = new Font("Segoe UI", 22, FontStyle.Bold), Bounds = new Rectangle(84, 28, 150, 48) });
        sidebar.Controls.Add(new Label { Text = "PAINEL DO CLIENTE", ForeColor = Color.FromArgb(178, 181, 211),
            Font = new Font("Segoe UI", 9, FontStyle.Bold), Bounds = new Rectangle(30, 82, 195, 28) });
        _menu = new FlowLayoutPanel { Bounds = new Rectangle(16, 145, 218, 380), FlowDirection = FlowDirection.TopDown,
            WrapContents = false, AutoScroll = true, Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left };
        sidebar.Controls.Add(_menu);
        var sair = MenuButton("Sair");
        sair.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
        sair.Click += (_, _) => { _session.Clear(); Close(); };
        sidebar.Controls.Add(sair);
        sidebar.Resize += (_, _) => { _menu.Height = Math.Max(160, sidebar.ClientSize.Height - 225);
            sair.Location = new Point(16, sidebar.ClientSize.Height - 66); };

        var content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(44) };
        layout.Controls.Add(content, 1, 0);
        var stack = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
            WrapContents = false, AutoScroll = true };
        content.Controls.Add(stack);
        stack.Controls.Add(new Label { Text = "Painel do cliente", Font = new Font("Segoe UI", 24, FontStyle.Bold),
            ForeColor = Color.FromArgb(31, 35, 62), AutoSize = true, Margin = new Padding(0, 0, 0, 18) });
        stack.Controls.Add(new Label { Text = $"Olá, {_session.Nome}.", Font = new Font("Segoe UI", 15),
            ForeColor = Color.FromArgb(73, 77, 103), AutoSize = true, Margin = new Padding(0, 0, 0, 22) });
        stack.Controls.Add(new Label { Text = "Acesso validado. Encontre serviços e acompanhe seus pedidos.",
            ForeColor = Color.FromArgb(53, 57, 78), AutoSize = true, Margin = new Padding(0, 0, 0, 22) });
        _quick = new FlowLayoutPanel { Width = 500, Height = 110, AutoSize = false, WrapContents = true };
        stack.Controls.Add(_quick);
        AddPage("Buscar serviços", () => new BuscarServicosForm(_session, _api), quick: true);
        AddPage("Meus agendamentos", () => new ClienteAgendamentosForm(_session, _api), quick: true);
        AddPage("Avaliações", () => new ClienteAgendamentosForm(_session, _api, avaliacoes: true));
        AddPage("Meu perfil", () => new ClientePerfilForm(_session, _api), quick: true);
        AddPage("Notificações", () => new ClienteNotificacoesForm(_session, _api));
        AddPage("Suporte", () => new ClienteSuporteForm(_session, _api));
    }

    private static Button MenuButton(string text) => new() { Text = text, Size = new Size(218, 44),
        BackColor = Color.FromArgb(65, 58, 120), ForeColor = Color.White, FlatStyle = FlatStyle.Flat,
        TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(14, 0, 0, 0),
        Margin = new Padding(0, 0, 0, 8), AccessibleName = text, Cursor = Cursors.Hand };

    internal void AddPage(string title, Func<Form> create, bool quick = false)
    {
        var button = MenuButton(title);
        button.Click += (_, _) => Open(create);
        _menu.Controls.Add(button);
        if (quick)
        {
            var shortcut = new Button { Text = title, Size = new Size(180, 44), Margin = new Padding(0, 0, 12, 0),
                BackColor = Color.FromArgb(84, 65, 218), ForeColor = Color.White, FlatStyle = FlatStyle.Flat,
                AccessibleName = title, Cursor = Cursors.Hand };
            shortcut.Click += (_, _) => Open(create);
            _quick.Controls.Add(shortcut);
        }
    }

    private void Open(Func<Form> create)
    {
        using var form = create();
        form.ShowDialog(this);
        if (string.IsNullOrWhiteSpace(_session.Jwt)) Close();
    }
}
