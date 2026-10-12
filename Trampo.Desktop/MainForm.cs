using Trampo.Desktop.Api;
using Trampo.Desktop.Session;

namespace Trampo.Desktop;

internal sealed class MainForm : Form
{
    private readonly UserSession _session;
    private readonly TrampoApiClient _api;

    public MainForm(UserSession session, TrampoApiClient api)
    {
        if (!string.Equals(session.Perfil, "profissional", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("O acesso ao Desktop exige perfil profissional.", nameof(session));

        _session = session;
        _api = api;
        BuildUi();
        VisualIdentity.Apply(this);
    }

    private void BuildUi()
    {
        Text = "TRAMPO | Painel profissional";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(860, 520);
        ClientSize = new Size(1000, 620);
        BackColor = Color.FromArgb(247, 248, 252);
        Font = new Font("Segoe UI", 10);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Controls.Add(layout);
        var sidebar = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(27, 25, 65) };
        layout.Controls.Add(sidebar, 0, 0);
        sidebar.Controls.Add(new PictureBox
        {
            Image = VisualIdentity.Logo,
            SizeMode = PictureBoxSizeMode.Zoom,
            Bounds = new Rectangle(24, 24, 56, 56)
        });

        sidebar.Controls.Add(new Label
        {
            Text = "TRAMPO",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 22, FontStyle.Bold),
            Bounds = new Rectangle(84, 28, 150, 48)
        });
        sidebar.Controls.Add(new Label
        {
            Text = "PAINEL PROFISSIONAL",
            ForeColor = Color.FromArgb(178, 181, 211),
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            Bounds = new Rectangle(30, 82, 195, 28)
        });
        sidebar.Controls.Add(new Label
        {
            Text = "Início",
            ForeColor = Color.White,
            BackColor = Color.FromArgb(65, 58, 120),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(18, 0, 0, 0),
            Bounds = new Rectangle(16, 150, 218, 44)
        });

        var agendamentos = new Button
        {
            Text = "Agendamentos",
            ForeColor = Color.White,
            BackColor = Color.FromArgb(65, 58, 120),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(14, 0, 0, 0),
            Bounds = new Rectangle(16, 208, 218, 44)
        };
        agendamentos.FlatAppearance.BorderSize = 0;
        agendamentos.Click += (_, _) =>
        {
            using var form = new AgendamentosForm(_session, _api);
            form.ShowDialog(this);
            if (string.IsNullOrEmpty(_session.Jwt)) Close();
        };
        sidebar.Controls.Add(agendamentos);
        var servicos = new Button
        {
            Text = "Serviços",
            ForeColor = Color.White,
            BackColor = Color.FromArgb(65, 58, 120),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(14, 0, 0, 0),
            Bounds = new Rectangle(16, 266, 218, 44)
        };
        servicos.FlatAppearance.BorderSize = 0;
        servicos.Click += (_, _) =>
        {
            using var form = new ServicosForm(_session, _api);
            form.ShowDialog(this);
            if (string.IsNullOrEmpty(_session.Jwt)) Close();
        };
        sidebar.Controls.Add(servicos);
        var locais = new Button
        {
            Text = "Locais", ForeColor = Color.White,
            BackColor = Color.FromArgb(65, 58, 120), FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand, TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(14, 0, 0, 0), Bounds = new Rectangle(16, 324, 218, 44)
        };
        locais.FlatAppearance.BorderSize = 0;
        locais.Click += (_, _) =>
        {
            using var form = new LocaisForm(_session, _api);
            form.ShowDialog(this);
            if (string.IsNullOrEmpty(_session.Jwt)) Close();
        };
        sidebar.Controls.Add(locais);        var sair = new Button
        {
            Text = "Sair",
            ForeColor = Color.White,
            BackColor = Color.FromArgb(47, 43, 91),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Bounds = new Rectangle(16, 0, 218, 44),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left
        };
        sair.FlatAppearance.BorderSize = 0;
        sair.Click += (_, _) =>
        {
            _session.Clear();
            Close();
        };
        sidebar.Controls.Add(sair);
        sidebar.Resize += (_, _) => sair.Top = sidebar.ClientSize.Height - sair.Height - 22;

        var content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(44), BackColor = BackColor };
        layout.Controls.Add(content, 1, 0);

        content.Controls.Add(new Label
        {
            Text = "Painel profissional",
            Font = new Font("Segoe UI", 24, FontStyle.Bold),
            ForeColor = Color.FromArgb(31, 35, 62),
            AutoSize = true,
            Location = new Point(44, 40)
        });
        content.Controls.Add(new Label
        {
            Text = $"Olá, {_session.Nome}.",
            Font = new Font("Segoe UI", 15),
            ForeColor = Color.FromArgb(73, 77, 103),
            AutoSize = true,
            Location = new Point(47, 105)
        });

        var card = new Panel
        {
            Bounds = new Rectangle(46, 168, 600, 150),
            BackColor = Color.White,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        content.Controls.Add(card);
        card.Controls.Add(new Label
        {
            Text = "Acesso validado",
            Font = new Font("Segoe UI", 16, FontStyle.Bold),
            ForeColor = Color.FromArgb(46, 41, 99),
            AutoSize = true,
            Location = new Point(24, 22)
        });
        card.Controls.Add(new Label
        {
            Text = "Sua identidade profissional foi confirmada pela API TRAMPO.",
            ForeColor = Color.FromArgb(90, 95, 115),
            AutoSize = true,
            Location = new Point(26, 68)
        });
    }
}



