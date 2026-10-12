using Trampo.Desktop.Api;
using Trampo.Desktop.Session;

namespace Trampo.Desktop;

internal sealed class AgendamentosForm : Form
{
    private readonly UserSession _session;
    private readonly TrampoApiClient _api;
    private DataGridView _grid = null!;
    private Button _atualizar = null!;
    private Button _detalhes = null!;
    private Button _voltar = null!;
    private Label _status = null!;
    private bool _busy;

    public AgendamentosForm(UserSession session, TrampoApiClient api)
    {
        _session = session;
        _api = api;
        BuildUi();
        VisualIdentity.Apply(this);
        Shown += async (_, _) => await LoadAsync();
    }

    private void BuildUi()
    {
        Text = "TRAMPO | Agendamentos recebidos";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(800, 520);
        ClientSize = new Size(1000, 610);
        Font = new Font("Segoe UI", 10);
        BackColor = Color.FromArgb(247, 248, 252);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1, Padding = new Padding(24) };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        Controls.Add(layout);

        var header = new Panel { Dock = DockStyle.Fill };
        layout.Controls.Add(header, 0, 0);
        header.Controls.Add(new Label
        {
            Text = "Agendamentos recebidos",
            Font = new Font("Segoe UI", 22, FontStyle.Bold),
            ForeColor = Color.FromArgb(31, 35, 62),
            AutoSize = true,
            Location = new Point(0, 0)
        });
        header.Controls.Add(new Label
        {
            Text = "Pedidos vinculados ao seu perfil profissional",
            ForeColor = Color.FromArgb(102, 107, 131),
            AutoSize = true,
            Location = new Point(3, 47)
        });

        _status = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Carregando...",
            ForeColor = Color.FromArgb(84, 65, 218),
            TextAlign = ContentAlignment.MiddleLeft
        };
        layout.Controls.Add(_status, 0, 1);

        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            ReadOnly = true,
            MultiSelect = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            RowHeadersVisible = false,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            ColumnHeadersHeight = 42,
            RowTemplate = { Height = 42 },
            EnableHeadersVisualStyles = false
        };
        _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(45, 42, 99);
        _grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(224, 218, 252);
        _grid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(31, 35, 62);
        _grid.DefaultCellStyle.Padding = new Padding(7, 0, 7, 0);
        _grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(250, 249, 255);
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", HeaderText = "ID", FillWeight = 12 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ServicoId", HeaderText = "Serviço ID", FillWeight = 22 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Data", HeaderText = "Data", FillWeight = 24 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Hora", HeaderText = "Horário", FillWeight = 18 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "Status", FillWeight = 28 });
        _grid.SelectionChanged += (_, _) => _detalhes.Enabled = !_busy && _grid.SelectedRows.Count > 0;
        _grid.CellDoubleClick += async (_, e) =>
        {
            if (e.RowIndex >= 0) await OpenDetailAsync();
        };
        layout.Controls.Add(_grid, 0, 2);

        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 12, 0, 0) };
        layout.Controls.Add(actions, 0, 3);
        _voltar = ActionButton("Voltar", Color.FromArgb(96, 101, 124));
        _voltar.Click += (_, _) => Close();
        actions.Controls.Add(_voltar);
        _detalhes = ActionButton("Ver detalhes", Color.FromArgb(84, 65, 218));
        _detalhes.Enabled = false;
        _detalhes.Click += async (_, _) => await OpenDetailAsync();
        actions.Controls.Add(_detalhes);
        _atualizar = ActionButton("Atualizar", Color.FromArgb(45, 42, 99));
        _atualizar.Click += async (_, _) => await LoadAsync();
        actions.Controls.Add(_atualizar);
    }

    private static Button ActionButton(string text, Color background)
    {
        var button = new Button
        {
            Text = text,
            Size = new Size(132, 40),
            BackColor = background,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Margin = new Padding(8, 0, 0, 0)
        };
        button.FlatAppearance.BorderSize = 0;
        return button;
    }

    private async Task LoadAsync()
    {
        if (_busy || IsDisposed) return;
        _busy = true;
        _atualizar.Enabled = false;
        _detalhes.Enabled = false;
        _status.ForeColor = Color.FromArgb(84, 65, 218);
        _status.Text = "Carregando agendamentos...";
        try
        {
            var items = await _api.GetAgendamentosRecebidosAsync(_session);
            if (IsDisposed) return;
            _grid.Rows.Clear();
            foreach (var item in items)
            {
                var rowIndex = _grid.Rows.Add(item.Id, item.ServicoId,
                    item.Data.ToString("dd/MM/yyyy"), item.Hora.ToString(@"hh\:mm"), AgendamentoStatus.Text(item.Status));
                _grid.Rows[rowIndex].Cells["Status"].Style.ForeColor = AgendamentoStatus.StatusColor(item.Status);
                _grid.Rows[rowIndex].Tag = item.Id;
            }
            _grid.ClearSelection();
            _status.Text = items.Count == 0 ? "Nenhum agendamento recebido." : $"{items.Count} agendamento(s) recebido(s).";
        }
        catch (ApiException ex)
        {
            if (IsDisposed) return;
            _status.ForeColor = Color.FromArgb(173, 45, 57);
            _status.Text = ex.Message;
            if (ex.IsUnauthorized)
            {
                MessageBox.Show(this, ex.Message, "Sessão encerrada", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Close();
            }
        }
        finally
        {
            _busy = false;
            if (!IsDisposed)
            {
                _atualizar.Enabled = true;
                _detalhes.Enabled = _grid.SelectedRows.Count > 0;
            }
        }
    }

    private async Task OpenDetailAsync()
    {
        if (_busy || _grid.SelectedRows.Count == 0 || _grid.SelectedRows[0].Tag is not int id) return;
        using var detail = new DetalheAgendamentoForm(_session, _api, id);
        detail.ShowDialog(this);
        if (string.IsNullOrWhiteSpace(_session.Jwt))
        {
            Close();
            return;
        }
        await LoadAsync();
    }
}

