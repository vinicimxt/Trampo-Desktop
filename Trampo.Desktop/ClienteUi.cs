using System.Globalization;
using Trampo.Desktop.Api;
using Trampo.Desktop.Session;

namespace Trampo.Desktop;

internal abstract class ClientePage : Form
{
    protected readonly UserSession Session;
    protected readonly TrampoApiClient Api;
    protected readonly Panel Body;
    protected readonly FlowLayoutPanel Actions;
    private readonly Label _status;
    private bool _busy;

    protected ClientePage(UserSession session, TrampoApiClient api, string title)
    {
        if (!string.Equals(session.Perfil, "cliente", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Esta tela exige perfil Cliente.", nameof(session));
        Session = session;
        Api = api;
        Text = "TRAMPO | " + title;
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(800, 520);
        ClientSize = new Size(1000, 650);
        Font = new Font("Segoe UI", 10);
        BackColor = Color.FromArgb(247, 248, 252);
        VisualIdentity.Apply(this);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), RowCount = 4 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
        Controls.Add(layout);
        layout.Controls.Add(new Label { Text = title, Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 22, FontStyle.Bold), ForeColor = Color.FromArgb(31, 35, 62),
            TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        _status = new Label { Dock = DockStyle.Fill, ForeColor = Color.FromArgb(73, 77, 103),
            TextAlign = ContentAlignment.MiddleLeft, AccessibleName = "Estado da tela" };
        layout.Controls.Add(_status, 0, 1);
        Body = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.White };
        layout.Controls.Add(Body, 0, 2);
        Actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft,
            WrapContents = true, Padding = new Padding(0, 6, 0, 0) };
        layout.Controls.Add(Actions, 0, 3);
        CancelButton = AddAction("Voltar", (_, _) => Close(), secondary: true);
        Shown += (_, _) => { if (Body.Controls.Count > 0) Body.Controls[0].Focus(); };
    }

    protected Button AddAction(string title, EventHandler click, bool secondary = false)
    {
        var button = new Button { Text = title, Size = new Size(150, 42),
            BackColor = secondary ? Color.FromArgb(96, 101, 124) : Color.FromArgb(84, 65, 218),
            ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand,
            Margin = new Padding(4, 0, 0, 4), AccessibleName = title };
        button.FlatAppearance.BorderSize = 0;
        button.Click += click;
        Actions.Controls.Add(button);
        return button;
    }

    protected void Status(string message, bool error = false)
    {
        _status.Text = message;
        _status.ForeColor = error ? Color.FromArgb(173, 45, 57) : Color.FromArgb(73, 77, 103);
    }

    protected async Task RunAsync(Func<Task> operation, string loading)
    {
        if (_busy || IsDisposed) return;
        _busy = true;
        UseWaitCursor = true;
        Status(loading);
        try { await operation(); }
        catch (ApiException ex)
        {
            if (IsDisposed) return;
            Status(ex.Message, true);
            if (ex.IsUnauthorized)
            {
                MessageBox.Show(this, ex.Message, "Sessão encerrada", MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                Close();
            }
        }
        finally { _busy = false; if (!IsDisposed) UseWaitCursor = false; }
    }

    protected static DataGridView Grid(params (string Name, string Title, float Weight)[] columns)
    {
        var grid = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false,
            AllowUserToAddRows = false, AllowUserToDeleteRows = false, ReadOnly = true,
            MultiSelect = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            RowHeadersVisible = false, BackgroundColor = Color.White, BorderStyle = BorderStyle.None,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, ColumnHeadersHeight = 42,
            RowTemplate = { Height = 42 }, EnableHeadersVisualStyles = false,
            AccessibleName = "Resultados" };
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(45, 42, 99);
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(224, 218, 252);
        grid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(31, 35, 62);
        foreach (var column in columns)
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = column.Name,
                HeaderText = column.Title, FillWeight = column.Weight });
        return grid;
    }

    protected static TableLayoutPanel Fields() => new() { Dock = DockStyle.Top,
        AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 2,
        Padding = new Padding(18) };

    protected static void AddField(TableLayoutPanel table, string title, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        var row = table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.Controls.Add(new Label { Text = title, AutoSize = true,
            Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = Color.FromArgb(45, 42, 99),
            Margin = new Padding(0, 10, 14, 10) }, 0, row);
        table.Controls.Add(new Label { Text = value, AutoSize = true, MaximumSize = new Size(620, 0),
            ForeColor = Color.FromArgb(53, 57, 78), Margin = new Padding(0, 10, 0, 10) }, 1, row);
    }

    protected static string Money(decimal? value) => value.HasValue
        ? value.Value.ToString("C", CultureInfo.GetCultureInfo("pt-BR")) : "A combinar";
    protected static string Modality(string? value) => value == "Domicilio" ? "Domicílio" : value ?? "";
}
