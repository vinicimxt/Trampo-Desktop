using System.Net;
using Trampo.Desktop.Api;
using Trampo.Desktop.Session;

namespace Trampo.Desktop;

internal sealed class LocaisForm : Form
{
    private readonly UserSession _session;
    private readonly TrampoApiClient _api;
    private DataGridView _grid = null!;
    private Label _status = null!, _addressPreview = null!;
    private Button _novo = null!, _editar = null!, _excluir = null!, _atualizar = null!, _voltar = null!;
    private bool _busy;

    public LocaisForm(UserSession session, TrampoApiClient api)
    {
        _session = session;
        _api = api;
        BuildUi();
        VisualIdentity.Apply(this);
        Shown += async (_, _) => await LoadAsync();
    }

    private void BuildUi()
    {
        Text = "TRAMPO | Meus locais";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(900, 560);
        ClientSize = new Size(1120, 650);
        Font = new Font("Segoe UI", 10);
        BackColor = Color.FromArgb(247, 248, 252);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 5, ColumnCount = 1, Padding = new Padding(24) };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
        Controls.Add(layout);
        var header = new Panel { Dock = DockStyle.Fill };
        layout.Controls.Add(header, 0, 0);
        header.Controls.Add(new Label { Text = "Meus locais", Font = new Font("Segoe UI", 22, FontStyle.Bold),
            ForeColor = Color.FromArgb(31, 35, 62), AutoSize = true, Location = new Point(0, 0) });
        header.Controls.Add(new Label { Text = "Gerencie os endereços dos seus atendimentos.",
            ForeColor = Color.FromArgb(102, 107, 131), AutoSize = true, Location = new Point(3, 47) });
        _status = new Label { Dock = DockStyle.Fill, Text = "Carregando locais...",
            ForeColor = Color.FromArgb(84, 65, 218), TextAlign = ContentAlignment.MiddleLeft };
        layout.Controls.Add(_status, 0, 1);
        _grid = new DataGridView
        {
            Dock = DockStyle.Fill, AutoGenerateColumns = false, ReadOnly = true,
            AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false,
            MultiSelect = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            RowHeadersVisible = false, BackgroundColor = Color.White, BorderStyle = BorderStyle.None,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, ColumnHeadersHeight = 42,
            RowTemplate = { Height = 42 }, EnableHeadersVisualStyles = false
        };
        _grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(45, 42, 99);
        _grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(224, 218, 252);
        _grid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(31, 35, 62);
        _grid.DefaultCellStyle.Padding = new Padding(7, 0, 7, 0);
        _grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(250, 249, 255);
        foreach (var (name, title, weight) in new (string, string, float)[]
        {
            ("Id", "ID", 8), ("Nome", "Nome", 20), ("Cep", "CEP", 12),
            ("Logradouro", "Logradouro", 25), ("Numero", "Número", 10),
            ("Bairro", "Bairro", 17), ("Cidade", "Cidade", 17), ("Uf", "UF", 7)
        })
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = name, HeaderText = title, FillWeight = weight });
        _grid.SelectionChanged += (_, _) => UpdateActions();
        _grid.CellDoubleClick += async (_, e) => { if (e.RowIndex >= 0) await OpenEditorAsync(); };
        layout.Controls.Add(_grid, 0, 2);
        _addressPreview = new Label { Dock = DockStyle.Fill, BackColor = Color.White,
            ForeColor = Color.FromArgb(73, 77, 103), Padding = new Padding(12, 5, 12, 5),
            Text = "Selecione um local para ver o endereço completo." };
        layout.Controls.Add(_addressPreview, 0, 3);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 14, 0, 0), WrapContents = false };
        layout.Controls.Add(actions, 0, 4);
        _voltar = Button("Voltar", Color.FromArgb(96, 101, 124));
        _voltar.Click += (_, _) => Close();
        actions.Controls.Add(_voltar);
        _atualizar = Button("Atualizar", Color.FromArgb(45, 42, 99));
        _atualizar.Click += async (_, _) => await LoadAsync();
        actions.Controls.Add(_atualizar);
        _excluir = Button("Excluir", Color.FromArgb(173, 45, 57));
        _excluir.Click += async (_, _) => await DeleteAsync();
        actions.Controls.Add(_excluir);
        _editar = Button("Editar", Color.FromArgb(65, 58, 120));
        _editar.Click += async (_, _) => await OpenEditorAsync();
        actions.Controls.Add(_editar);
        _novo = Button("Novo local", Color.FromArgb(84, 65, 218));
        _novo.Click += async (_, _) => await OpenEditorAsync(true);
        actions.Controls.Add(_novo);
        UpdateActions();
    }

    private static Button Button(string text, Color color)
    {
        var button = new Button { Text = text, Size = new Size(132, 40), BackColor = color,
            ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand,
            Margin = new Padding(8, 0, 0, 0) };
        button.FlatAppearance.BorderSize = 0;
        return button;
    }

    private LocalResponse? Selected => _grid.SelectedRows.Count == 1
        ? _grid.SelectedRows[0].Tag as LocalResponse : null;

    private void UpdateActions()
    {
        _novo.Enabled = !_busy;
        _editar.Enabled = !_busy && Selected is not null;
        _excluir.Enabled = !_busy && Selected is not null;
        _atualizar.Enabled = !_busy;
        _voltar.Enabled = !_busy;
        if (_addressPreview is not null) _addressPreview.Text = Selected is { } local
            ? LocalEnderecoFormatter.Format(local)
            : "Selecione um local para ver o endereço completo.";
    }

    private void ShowError(ApiException ex)
    {
        _status.ForeColor = Color.FromArgb(173, 45, 57);
        _status.Text = ex.Message;
        if (ex.IsUnauthorized)
        {
            MessageBox.Show(this, ex.Message, "Sessão encerrada", MessageBoxButtons.OK, MessageBoxIcon.Information);
            Close();
        }
    }

    private async Task LoadAsync()
    {
        if (_busy || IsDisposed) return;
        _busy = true;
        UpdateActions();
        _status.ForeColor = Color.FromArgb(84, 65, 218);
        _status.Text = "Carregando locais...";
        try
        {
            var items = await _api.ListarLocaisAsync(_session);
            if (IsDisposed) return;
            _grid.Rows.Clear();
            foreach (var item in items)
            {
                var address = item.DadosEndereco is { Estruturado: true } ? item.DadosEndereco : null;
                int index = _grid.Rows.Add(item.Id, item.Nome, address?.Cep ?? "",
                    address?.Logradouro ?? item.Endereco, address?.Numero ?? "", address?.Bairro ?? "",
                    address?.Cidade ?? "", address?.Uf ?? "");
                _grid.Rows[index].Tag = item;
                foreach (DataGridViewCell cell in _grid.Rows[index].Cells) cell.ToolTipText = LocalEnderecoFormatter.Format(item);
            }
            _grid.ClearSelection();
            _status.ForeColor = Color.FromArgb(73, 77, 103);
            _status.Text = items.Count == 0 ? "Nenhum local cadastrado." : $"{items.Count} local(is) encontrado(s).";
        }
        catch (ApiException ex) { if (!IsDisposed) ShowError(ex); }
        finally { _busy = false; if (!IsDisposed) UpdateActions(); }
    }

    private async Task OpenEditorAsync(bool novo = false)
    {
        if (_busy || (!novo && Selected is null)) return;
        _busy = true;
        UpdateActions();
        using var editor = new LocalForm(_session, _api, novo ? null : Selected!.Id);
        var result = editor.ShowDialog(this);
        _busy = false;
        if (string.IsNullOrWhiteSpace(_session.Jwt)) { Close(); return; }
        if (!IsDisposed)
        {
            UpdateActions();
            if (result == DialogResult.OK) await LoadAsync();
        }
    }

    private async Task DeleteAsync()
    {
        var item = Selected;
        if (_busy || item is null) return;
        if (MessageBox.Show(this, $"Excluir o local '{item.Nome}'? A exclusão só será possível se ele não estiver vinculado a serviços ou agendamentos.",
            "Confirmar exclusão", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        _busy = true;
        UpdateActions();
        _status.ForeColor = Color.FromArgb(84, 65, 218);
        _status.Text = "Excluindo local...";
        try
        {
            await _api.ExcluirLocalAsync(_session, item.Id);
            if (IsDisposed) return;
            MessageBox.Show(this, "Local excluído.", "TRAMPO", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (ApiException ex)
        {
            if (IsDisposed) return;
            var message = ex.StatusCode == HttpStatusCode.Conflict
                ? "Este local está vinculado a serviços ou agendamentos e não pode ser excluído."
                : ex.Message;
            _status.ForeColor = Color.FromArgb(173, 45, 57);
            _status.Text = message;
            MessageBox.Show(this, message, ex.IsUnauthorized ? "Sessão encerrada" : "Não foi possível excluir",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            if (ex.IsUnauthorized) { Close(); return; }
        }
        finally { _busy = false; if (!IsDisposed) UpdateActions(); }
        if (!IsDisposed) await LoadAsync();
    }
}
