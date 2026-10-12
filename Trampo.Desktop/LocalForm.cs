using System.Net;
using Trampo.Desktop.Api;
using Trampo.Desktop.Session;

namespace Trampo.Desktop;

internal sealed class LocalForm : Form
{
    private readonly UserSession _session;
    private readonly TrampoApiClient _api;
    private readonly int? _id;
    private readonly Dictionary<string, TextBox> _fields = [];
    private Label _status = null!;
    private Button _salvar = null!, _buscar = null!, _cancelar = null!;
    private bool _busy;

    public LocalForm(UserSession session, TrampoApiClient api, int? id)
    {
        _session = session;
        _api = api;
        _id = id;
        BuildUi();
        VisualIdentity.Apply(this);
        if (id.HasValue) Shown += async (_, _) => await LoadAsync();
    }

    private void BuildUi()
    {
        Text = _id.HasValue ? $"TRAMPO | Editar local #{_id}" : "TRAMPO | Novo local";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(650, 550);
        ClientSize = new Size(760, 690);
        Font = new Font("Segoe UI", 10);
        BackColor = Color.FromArgb(247, 248, 252);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4,
            ColumnCount = 1, Padding = new Padding(24) };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        Controls.Add(layout);
        layout.Controls.Add(new Label { Text = _id.HasValue ? $"Editar local #{_id}" : "Novo local",
            Font = new Font("Segoe UI", 22, FontStyle.Bold),
            ForeColor = Color.FromArgb(31, 35, 62), Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        _status = new Label { Text = "Preencha os dados do local.", Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(84, 65, 218), TextAlign = ContentAlignment.MiddleLeft };
        layout.Controls.Add(_status, 0, 1);
        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.White };
        layout.Controls.Add(scroll, 0, 2);
        var fields = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top, ColumnCount = 2, Padding = new Padding(24) };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        scroll.Controls.Add(fields);
        AddField(fields, "Nome", "Nome", 100);
        var cepPanel = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 2, Height = 40 };
        cepPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        cepPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 142));
        var cep = new TextBox { MaxLength = 9, Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle };
        _fields["Cep"] = cep;
        cepPanel.Controls.Add(cep, 0, 0);
        _buscar = ActionButton("Buscar CEP", Color.FromArgb(65, 58, 120), 130);
        _buscar.Click += async (_, _) => await SearchCepAsync();
        cepPanel.Controls.Add(_buscar, 1, 0);
        AddControl(fields, "CEP", cepPanel);
        AddField(fields, "Logradouro", "Logradouro", 100);
        AddField(fields, "Número", "Numero", 20);
        AddField(fields, "Complemento", "Complemento", 60);
        AddField(fields, "Bairro", "Bairro", 80);
        AddField(fields, "Cidade", "Cidade", 80);
        AddField(fields, "UF", "Uf", 2);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 14, 0, 0),
            WrapContents = false };
        layout.Controls.Add(actions, 0, 3);
        _cancelar = ActionButton("Cancelar", Color.FromArgb(96, 101, 124), 132);
        _cancelar.Click += (_, _) => Close();
        actions.Controls.Add(_cancelar);
        _salvar = ActionButton(_id.HasValue ? "Salvar" : "Cadastrar", Color.FromArgb(84, 65, 218), 132);
        _salvar.Click += async (_, _) => await SaveAsync();
        actions.Controls.Add(_salvar);
        AcceptButton = _salvar;
        CancelButton = _cancelar;
    }

    private void AddField(TableLayoutPanel table, string label, string key, int max)
    {
        var input = new TextBox { MaxLength = max, Dock = DockStyle.Top,
            Height = 32, BorderStyle = BorderStyle.FixedSingle };
        _fields[key] = input;
        AddControl(table, label, input);
    }

    private static void AddControl(TableLayoutPanel table, string label, Control control)
    {
        int row = table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.Controls.Add(new Label { Text = label, Font = new Font("Segoe UI", 10, FontStyle.Bold),
            ForeColor = Color.FromArgb(45, 42, 99), AutoSize = true,
            Margin = new Padding(0, 12, 12, 12) }, 0, row);
        control.Margin = new Padding(0, 8, 0, 8);
        table.Controls.Add(control, 1, row);
    }

    private static Button ActionButton(string text, Color color, int width)
    {
        var button = new Button { Text = text, Size = new Size(width, 40),
            BackColor = color, ForeColor = Color.White, FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand, Margin = new Padding(8, 0, 0, 0) };
        button.FlatAppearance.BorderSize = 0;
        return button;
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        _salvar.Enabled = !_busy;
        _buscar.Enabled = !_busy;
        _cancelar.Enabled = !_busy;
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
        if (_busy || IsDisposed || !_id.HasValue) return;
        SetBusy(true);
        _status.Text = "Carregando local...";
        try
        {
            var local = await _api.BuscarLocalAsync(_session, _id.Value);
            if (IsDisposed) return;
            _fields["Nome"].Text = local.Nome;
            var address = local.DadosEndereco;
            if (address is { Estruturado: true })
            {
                _fields["Cep"].Text = address.Cep ?? "";
                _fields["Logradouro"].Text = address.Logradouro ?? "";
                _fields["Numero"].Text = address.Numero ?? "";
                _fields["Complemento"].Text = address.Complemento ?? "";
                _fields["Bairro"].Text = address.Bairro ?? "";
                _fields["Cidade"].Text = address.Cidade ?? "";
                _fields["Uf"].Text = address.Uf ?? "";
                _status.Text = "Confira os dados do local.";
            }
            else
            {
                _status.Text = "Endereço legado: preencha os campos estruturados antes de salvar.";
            }
        }
        catch (ApiException ex) { if (!IsDisposed) ShowError(ex); }
        finally { if (!IsDisposed) SetBusy(false); }
    }

    private async Task SearchCepAsync()
    {
        if (_busy || IsDisposed) return;
        string cep = _fields["Cep"].Text.Trim();
        if (!System.Text.RegularExpressions.Regex.IsMatch(cep, @"^[0-9]{5}-?[0-9]{3}$"))
        {
            _status.Text = "Informe um CEP com oito dígitos.";
            return;
        }
        SetBusy(true);
        _status.ForeColor = Color.FromArgb(84, 65, 218);
        _status.Text = "Buscando CEP...";
        try
        {
            var address = await _api.BuscarCepAsync(_session, cep);
            if (IsDisposed) return;
            _fields["Cep"].Text = address.Cep;
            _fields["Logradouro"].Text = address.Logradouro;
            _fields["Bairro"].Text = address.Bairro;
            _fields["Cidade"].Text = address.Cidade;
            _fields["Uf"].Text = address.Uf;
            _status.Text = "Endereço encontrado. Complete número e complemento, se houver.";
        }
        catch (ApiException ex)
        {
            if (IsDisposed) return;
            if (ex.StatusCode == HttpStatusCode.NotFound)
            {
                _status.ForeColor = Color.FromArgb(173, 45, 57);
                _status.Text = "CEP não encontrado. Preencha o endereço manualmente.";
            }
            else if (ex.IsUnauthorized) ShowError(ex);
            else
            {
                _status.ForeColor = Color.FromArgb(173, 45, 57);
                _status.Text = $"{ex.Message} Você pode preencher o endereço manualmente.";
            }
        }
        finally { if (!IsDisposed) SetBusy(false); }
    }

    private bool TryBuildRequest(out LocalRequest? request)
    {
        request = null;
        string V(string key) => _fields[key].Text.Trim();
        string cep = V("Cep"), uf = V("Uf").ToUpperInvariant();
        if (!System.Text.RegularExpressions.Regex.IsMatch(cep, @"^[0-9]{5}-?[0-9]{3}$"))
        {
            _status.Text = "Informe um CEP com oito dígitos.";
            return false;
        }
        if (V("Logradouro").Length == 0 || V("Numero").Length == 0 ||
            V("Bairro").Length == 0 || V("Cidade").Length == 0 ||
            !"AC AL AP AM BA CE DF ES GO MA MT MS MG PA PB PR PE PI RJ RN RS RO RR SC SP SE TO"
                .Split(' ').Contains(uf))
        {
            _status.Text = "Preencha logradouro, número, bairro, cidade e uma UF válida.";
            return false;
        }
        string addressText = $"{V("Logradouro")}, {V("Numero")}" +
            (V("Complemento").Length > 0 ? " - " + V("Complemento") : "") +
            $" - {V("Bairro")}, {V("Cidade")}/{uf} - CEP {cep.Replace("-", "")}";
        if (addressText.Length > 255)
        {
            _status.Text = "O endereço completo deve ter até 255 caracteres.";
            return false;
        }
        request = new LocalRequest(V("Nome"), new EnderecoRequest(cep, V("Logradouro"),
            V("Numero"), V("Complemento").Length == 0 ? null : V("Complemento"),
            V("Bairro"), V("Cidade"), uf));
        return true;
    }

    private async Task SaveAsync()
    {
        if (_busy || !TryBuildRequest(out var request) || request is null) return;
        SetBusy(true);
        _status.ForeColor = Color.FromArgb(84, 65, 218);
        _status.Text = "Salvando local...";
        try
        {
            if (_id.HasValue) await _api.EditarLocalAsync(_session, _id.Value, request);
            else await _api.CriarLocalAsync(_session, request);
            if (IsDisposed) return;
            MessageBox.Show(this, _id.HasValue ? "Local atualizado." : "Local cadastrado.",
                "TRAMPO", MessageBoxButtons.OK, MessageBoxIcon.Information);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (ApiException ex)
        {
            if (IsDisposed) return;
            if (ex.StatusCode == HttpStatusCode.Conflict)
            {
                _status.ForeColor = Color.FromArgb(173, 45, 57);
                _status.Text = "Este local tem histórico e não pode ser alterado. Cadastre outro local.";
            }
            else ShowError(ex);
        }
        finally { if (!IsDisposed) SetBusy(false); }
    }
}
