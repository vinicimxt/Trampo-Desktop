using Trampo.Desktop.Api;
using Trampo.Desktop.Session;

namespace Trampo.Desktop;

public partial class LoginForm : Form
{
    private readonly TrampoApiClient _api = new();
    private TextBox _email = null!;
    private TextBox _senha = null!;
    private Button _entrar = null!;
    private Label _status = null!;

    public LoginForm()
    {
        InitializeComponent();
        BuildUi();
        VisualIdentity.Apply(this);
        FormClosed += (_, _) => _api.Dispose();
    }

    private void BuildUi()
    {
        Text = "TRAMPO | Acesso profissional";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(820, 510);
        ClientSize = new Size(940, 560);
        BackColor = Color.White;
        Font = new Font("Segoe UI", 10);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 43));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 57));
        Controls.Add(layout);

        var brand = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(27, 25, 65) };
        layout.Controls.Add(brand, 0, 0);
        brand.Controls.Add(new PictureBox
        {
            Image = VisualIdentity.Logo,
            SizeMode = PictureBoxSizeMode.Zoom,
            Location = new Point(42, 92),
            Size = new Size(156, 130)
        });
        brand.Controls.Add(new Label
        {
            Text = "TRAMPO",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 30, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(46, 228)
        });
        brand.Controls.Add(new Label
        {
            Text = "Seu espaço de trabalho,\nconectado aos seus clientes.",
            ForeColor = Color.FromArgb(199, 200, 222),
            Font = new Font("Segoe UI", 12),
            AutoSize = true,
            Location = new Point(48, 300)
        });

        var right = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        layout.Controls.Add(right, 1, 0);
        var card = new Panel { Size = new Size(380, 390), BackColor = Color.White };
        right.Controls.Add(card);
        right.Resize += (_, _) => card.Location = new Point(
            Math.Max(24, (right.ClientSize.Width - card.Width) / 2),
            Math.Max(24, (right.ClientSize.Height - card.Height) / 2));

        card.Controls.Add(new Label
        {
            Text = "Acesse sua conta",
            Font = new Font("Segoe UI", 22, FontStyle.Bold),
            ForeColor = Color.FromArgb(31, 35, 62),
            AutoSize = true,
            Location = new Point(0, 0)
        });
        card.Controls.Add(new Label
        {
            Text = "Área exclusiva para profissionais",
            ForeColor = Color.FromArgb(107, 111, 132),
            AutoSize = true,
            Location = new Point(2, 52)
        });

        card.Controls.Add(FieldLabel("E-mail", 104));
        _email = FieldBox(132, false);
        _email.MaxLength = 100;
        card.Controls.Add(_email);

        card.Controls.Add(FieldLabel("Senha", 194));
        _senha = FieldBox(222, true);
        _senha.MaxLength = 1024;
        card.Controls.Add(_senha);

        _entrar = new Button
        {
            Text = "Entrar",
            Bounds = new Rectangle(0, 294, 360, 44),
            BackColor = Color.FromArgb(84, 65, 218),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        _entrar.FlatAppearance.BorderSize = 0;
        _entrar.Click += EntrarClick;
        card.Controls.Add(_entrar);
        AcceptButton = _entrar;

        _status = new Label
        {
            Bounds = new Rectangle(0, 348, 360, 42),
            ForeColor = Color.FromArgb(173, 45, 57),
            AutoEllipsis = true
        };
        card.Controls.Add(_status);
    }

    private static Label FieldLabel(string text, int y) => new()
    {
        Text = text,
        Bounds = new Rectangle(0, y, 360, 24),
        Font = new Font("Segoe UI", 10, FontStyle.Bold),
        ForeColor = Color.FromArgb(47, 51, 76)
    };

    private static TextBox FieldBox(int y, bool password) => new()
    {
        Bounds = new Rectangle(0, y, 360, 32),
        BorderStyle = BorderStyle.FixedSingle,
        UseSystemPasswordChar = password
    };

    private async void EntrarClick(object? sender, EventArgs e)
    {
        var email = _email.Text.Trim();
        var senha = _senha.Text;
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(senha))
        {
            _status.Text = "Informe o e-mail e a senha.";
            return;
        }

        _entrar.Enabled = false;
        _status.ForeColor = Color.FromArgb(84, 65, 218);
        _status.Text = "Validando acesso...";
        try
        {
            var session = await _api.SignInProfessionalAsync(email, senha);
            if (IsDisposed)
            {
                session.Clear();
                return;
            }

            Hide();
            try
            {
                using var main = new MainForm(session, _api);
                main.ShowDialog(this);
            }
            finally
            {
                session.Clear();
                _senha.Clear();
                _status.Text = "";
                if (!IsDisposed)
                {
                    Show();
                    _senha.Focus();
                }
            }
        }
        catch (ApiException ex)
        {
            if (!IsDisposed)
            {
                _status.ForeColor = Color.FromArgb(173, 45, 57);
                _status.Text = ex.Message;
            }
        }
        finally
        {
            if (!IsDisposed) _entrar.Enabled = true;
        }
    }
}

