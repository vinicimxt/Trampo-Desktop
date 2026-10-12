using System.Globalization;
using Trampo.Desktop.Api;
using Trampo.Desktop.Session;

namespace Trampo.Desktop;

internal sealed class ServicosForm : Form
{
    private static readonly CultureInfo Brasil = CultureInfo.GetCultureInfo("pt-BR");
    private readonly UserSession _session;
    private readonly TrampoApiClient _api;
    private DataGridView _grid = null!;
    private Label _status = null!;
    private Button _novo = null!;
    private Button _editar = null!;
    private Button _remover = null!;
    private Button _atualizar = null!;
    private Button _voltar = null!;
    private bool _busy;

    public ServicosForm(UserSession session, TrampoApiClient api)
    {
        _session = session;
        _api = api;
        BuildUi();
        VisualIdentity.Apply(this);
        Shown += async (_, _) => await LoadAsync();
    }

    private void BuildUi()
    {
        Text = "TRAMPO | Meus serviços";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(900, 560);
        ClientSize = new Size(1120, 650);
        Font = new Font("Segoe UI", 10);
        BackColor = Color.FromArgb(247, 248, 252);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1, Padding = new Padding(24) };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
        Controls.Add(layout);

        var header = new Panel { Dock = DockStyle.Fill };
        layout.Controls.Add(header, 0, 0);
        header.Controls.Add(new Label
        {
            Text = "Meus serviços",
            Font = new Font("Segoe UI", 22, FontStyle.Bold),
            ForeColor = Color.FromArgb(31, 35, 62), AutoSize = true, Location = new Point(0, 0)
        });
        header.Controls.Add(new Label
        {
            Text = "Gerencie sua oferta, incluindo serviços inativos.",
            ForeColor = Color.FromArgb(102, 107, 131), AutoSize = true, Location = new Point(3, 47)
        });

        _status = new Label
        {
            Dock = DockStyle.Fill, Text = "Carregando serviços...",
            ForeColor = Color.FromArgb(84, 65, 218), TextAlign = ContentAlignment.MiddleLeft
        };
        layout.Controls.Add(_status, 0, 1);

        _grid = new DataGridView
        {
            Dock = DockStyle.Fill, AutoGenerateColumns = false, ReadOnly = true,
            AllowUserToAddRows = false, AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false, MultiSelect = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            RowHeadersVisible = false, BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            ColumnHeadersHeight = 42, RowTemplate = { Height = 42 },
            EnableHeadersVisualStyles = false
        };
        _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(45, 42, 99);
        _grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(224, 218, 252);
        _grid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(31, 35, 62);
        _grid.DefaultCellStyle.Padding = new Padding(7, 0, 7, 0);
        _grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(250, 249, 255);
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", HeaderText = "ID", FillWeight = 8 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Nome", HeaderText = "Nome", FillWeight = 24 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Atendimento", HeaderText = "Atendimento", FillWeight = 16 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "TipoPreco", HeaderText = "Preço", FillWeight = 14 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "PrecoBase", HeaderText = "Valor", FillWeight = 15 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Classificacao", HeaderText = "Categoria / subcategoria", FillWeight = 29 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "Status", FillWeight = 12 });
        _grid.SelectionChanged += (_, _) => UpdateActions();
        _grid.CellDoubleClick += async (_, e) => { if (e.RowIndex >= 0) await OpenEditorAsync(); };
        layout.Controls.Add(_grid, 0, 2);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 14, 0, 0), WrapContents = false
        };
        layout.Controls.Add(actions, 0, 3);
        _voltar = ActionButton("Voltar", Color.FromArgb(96, 101, 124));
        _voltar.Click += (_, _) => Close();
        actions.Controls.Add(_voltar);
        _atualizar = ActionButton("Atualizar", Color.FromArgb(45, 42, 99));
        _atualizar.Click += async (_, _) => await LoadAsync();
        actions.Controls.Add(_atualizar);
        _remover = ActionButton("Remover da oferta", Color.FromArgb(173, 45, 57), 165);
        _remover.Click += async (_, _) => await RemoveAsync();
        actions.Controls.Add(_remover);
        _editar = ActionButton("Editar", Color.FromArgb(65, 58, 120));
        _editar.Click += async (_, _) => await OpenEditorAsync();
        actions.Controls.Add(_editar);
        _novo = ActionButton("Novo serviço", Color.FromArgb(84, 65, 218));
        _novo.Click += async (_, _) => await OpenEditorAsync(newItem: true);
        actions.Controls.Add(_novo);
        UpdateActions();
    }

    private static Button ActionButton(string text, Color background, int width = 132)
    {
        var button = new Button
        {
            Text = text, Size = new Size(width, 40), BackColor = background,
            ForeColor = Color.White, FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand, Margin = new Padding(8, 0, 0, 0)
        };
        button.FlatAppearance.BorderSize = 0;
        return button;
    }

    private ServicoProprioResponse? Selected =>
        _grid.SelectedRows.Count == 1 ? _grid.SelectedRows[0].Tag as ServicoProprioResponse : null;

    private void UpdateActions()
    {
        _novo.Enabled = !_busy;
        _editar.Enabled = !_busy && Selected is not null;
        _remover.Enabled = !_busy && Selected is { Ativo: true };
        _atualizar.Enabled = !_busy;
        _voltar.Enabled = !_busy;
    }

    private async Task LoadAsync()
    {
        if (_busy || IsDisposed) return;
        _busy = true;
        UpdateActions();
        _status.ForeColor = Color.FromArgb(84, 65, 218);
        _status.Text = "Carregando serviços...";
        try
        {
            var items = await _api.ListarMeusServicosAsync(_session);
            if (IsDisposed) return;
            _grid.Rows.Clear();
            foreach (var item in items)
            {
                string price = item.TipoPreco == "Fixo" && item.PrecoBase.HasValue
                    ? item.PrecoBase.Value.ToString("C", Brasil) : "A combinar";
                string modality = item.Atendimento == "Domicilio" ? "Domicílio" : item.Atendimento;
                int index = _grid.Rows.Add(item.Id, item.Nome, modality, item.TipoPreco,
                    price, $"{item.Categoria} / {item.Subcategoria}", item.Ativo ? "Ativo" : "Inativo");
                var row = _grid.Rows[index];
                row.Tag = item;
                if (!item.Ativo)
                    row.Cells["Status"].Style.ForeColor = Color.FromArgb(143, 74, 38);
            }
            _grid.ClearSelection();
            _status.ForeColor = Color.FromArgb(73, 77, 103);
            _status.Text = items.Count == 0 ? "Nenhum serviço cadastrado." : $"{items.Count} serviço(s) encontrado(s).";
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
            if (!IsDisposed) UpdateActions();
        }
    }

    private async Task OpenEditorAsync(bool newItem = false)
    {
        if (_busy || (!newItem && Selected is null)) return;
        using var editor = new ServicoForm(_session, _api, newItem ? null : Selected);
        var result = editor.ShowDialog(this);
        if (string.IsNullOrWhiteSpace(_session.Jwt))
        {
            Close();
            return;
        }
        if (result == DialogResult.OK) await LoadAsync();
    }

    private async Task RemoveAsync()
    {
        var item = Selected;
        if (_busy || item is not { Ativo: true }) return;
        if (MessageBox.Show(this, $"Remover '{item.Nome}' da oferta? Conforme o histórico, o serviço será excluído ou desativado.",
                "Confirmar remoção", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;
        _busy = true;
        UpdateActions();
        _status.ForeColor = Color.FromArgb(84, 65, 218);
        _status.Text = "Removendo serviço da oferta...";
        try
        {
            var resultado = await _api.RemoverServicoAsync(_session, item.Id);
            if (IsDisposed) return;
            var message = resultado.Resultado == "desativado"
                ? "Serviço desativado. O histórico de agendamentos foi preservado."
                : "Serviço removido da oferta.";
            MessageBox.Show(this, message, "TRAMPO", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (ApiException ex)
        {
            if (IsDisposed) return;
            MessageBox.Show(this, ex.Message, ex.IsUnauthorized ? "Sessão encerrada" : "Não foi possível remover",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            if (ex.IsUnauthorized) { Close(); return; }
        }
        finally
        {
            _busy = false;
            if (!IsDisposed) UpdateActions();
        }
        if (!IsDisposed) await LoadAsync();
    }
}