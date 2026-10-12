using Trampo.Desktop.Api;
using Trampo.Desktop.Session;

namespace Trampo.Desktop;

internal sealed class ServicoForm : Form
{
    private sealed record Choice(string Label, string Value)
    {
        public override string ToString() => Label;
    }

    private readonly UserSession _session;
    private readonly TrampoApiClient _api;
    private readonly ServicoProprioResponse? _original;
    private readonly List<CheckBox> _days = [];
    private TextBox _nome = null!;
    private TextBox _descricao = null!;
    private TextBox _link = null!;
    private ComboBox _atendimento = null!;
    private ComboBox _tipoPreco = null!;
    private ComboBox _categoria = null!;
    private ComboBox _subcategoria = null!;
    private ComboBox _local = null!;
    private NumericUpDown _preco = null!;
    private DateTimePicker _horaInicio = null!;
    private DateTimePicker _horaFim = null!;
    private Label _localLabel = null!;
    private Label _linkLabel = null!;
    private Label _localHint = null!;
    private Label _status = null!;
    private Button _salvar = null!;
    private Button _cancelar = null!;
    private bool _loading;
    private bool _loadingLocais;
    private bool _locaisLoaded;
    private bool _busy;

    public ServicoForm(UserSession session, TrampoApiClient api, ServicoProprioResponse? original)
    {
        _session = session;
        _api = api;
        _original = original;
        BuildUi();
        VisualIdentity.Apply(this);
        Shown += async (_, _) => await LoadAsync();
    }

    private void BuildUi()
    {
        Text = _original is null ? "TRAMPO | Novo serviço" : $"TRAMPO | Editar serviço #{_original.Id}";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(760, 600);
        ClientSize = new Size(850, 740);
        Font = new Font("Segoe UI", 10);
        BackColor = Color.FromArgb(247, 248, 252);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1, Padding = new Padding(24) };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        Controls.Add(layout);
        layout.Controls.Add(new Label
        {
            Text = _original is null ? "Novo serviço" : $"Editar serviço #{_original.Id}",
            Font = new Font("Segoe UI", 22, FontStyle.Bold),
            ForeColor = Color.FromArgb(31, 35, 62), Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        _status = new Label
        {
            Dock = DockStyle.Fill, ForeColor = Color.FromArgb(84, 65, 218),
            Text = "Carregando categorias...", TextAlign = ContentAlignment.MiddleLeft
        };
        layout.Controls.Add(_status, 0, 1);

        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.White };
        layout.Controls.Add(scroll, 0, 2);
        var fields = new TableLayoutPanel
        {
            AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top, ColumnCount = 2, Padding = new Padding(24)
        };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        scroll.Controls.Add(fields);

        _nome = TextField(100);
        AddField(fields, "Nome", _nome);
        _descricao = TextField(255);
        _descricao.Multiline = true;
        _descricao.ScrollBars = ScrollBars.Vertical;
        _descricao.Height = 76;
        AddField(fields, "Descrição", _descricao);

        _atendimento = SelectField();
        _atendimento.Items.AddRange([
            new Choice("Local", "Local"), new Choice("Domicílio", "Domicilio"), new Choice("Online", "Online")
        ]);
        _atendimento.SelectedIndexChanged += async (_, _) =>
        {
            if (_loading) return;
            UpdateConditionalFields();
            if (Atendimento == "Local") await LoadLocaisAsync();
        };
        AddField(fields, "Atendimento", _atendimento);

        _tipoPreco = SelectField();
        _tipoPreco.Items.AddRange([new Choice("Fixo", "Fixo"), new Choice("A combinar", "Combinar")]);
        _tipoPreco.SelectedIndexChanged += (_, _) => UpdateConditionalFields();
        AddField(fields, "Tipo de preço", _tipoPreco);
        _preco = new NumericUpDown
        {
            DecimalPlaces = 2, Minimum = 0, Maximum = 99999999.99m,
            Increment = 1, ThousandsSeparator = true, Width = 250
        };
        AddField(fields, "Preço base (R$)", _preco);

        _categoria = SelectField();
        _categoria.DisplayMember = nameof(CategoriaResponse.Nome);
        _categoria.SelectedIndexChanged += (_, _) => { if (!_loading) PopulateSubcategories(); };
        AddField(fields, "Categoria", _categoria);
        _subcategoria = SelectField();
        _subcategoria.DisplayMember = nameof(SubcategoriaResponse.Nome);
        AddField(fields, "Subcategoria", _subcategoria);
        if (_original is not null)
        {
            AddField(fields, "Classificação", new Label
            {
                Text = "Categoria e subcategoria não podem ser alteradas nesta versão.",
                ForeColor = Color.FromArgb(96, 101, 124), AutoSize = true
            });
        }

        _local = SelectField();
        _local.DisplayMember = nameof(LocalResponse.Nome);
        _localLabel = AddField(fields, "Local", _local);
        _localHint = new Label
        {
            AutoSize = true, MaximumSize = new Size(520, 0),
            ForeColor = Color.FromArgb(143, 74, 38),
            Text = "Sem locais cadastrados. Cadastre um local em Locais antes de usar esta modalidade."
        };
        AddField(fields, "", _localHint);
        _link = TextField(255);
        _linkLabel = AddField(fields, "Link online", _link);

        var dayPanel = new FlowLayoutPanel
        {
            AutoSize = true, Dock = DockStyle.Fill, WrapContents = true,
            Margin = new Padding(0, 8, 0, 8)
        };
        string[] dayNames = ["Domingo", "Segunda", "Terça", "Quarta", "Quinta", "Sexta", "Sábado"];
        for (int day = 0; day < dayNames.Length; day++)
        {
            var check = new CheckBox { Text = dayNames[day], Tag = day, Width = 94, AutoSize = false };
            _days.Add(check);
            dayPanel.Controls.Add(check);
        }
        AddField(fields, "Dias disponíveis", dayPanel);
        _horaInicio = TimeField(8);
        AddField(fields, "Hora inicial", _horaInicio);
        _horaFim = TimeField(18);
        AddField(fields, "Hora final", _horaFim);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 14, 0, 0), WrapContents = false
        };
        layout.Controls.Add(actions, 0, 3);
        _cancelar = ActionButton("Cancelar", Color.FromArgb(96, 101, 124));
        _cancelar.Click += (_, _) => Close();
        actions.Controls.Add(_cancelar);
        _salvar = ActionButton(_original is null ? "Cadastrar" : "Salvar", Color.FromArgb(84, 65, 218));
        _salvar.Click += async (_, _) => await SaveAsync();
        actions.Controls.Add(_salvar);
        AcceptButton = _salvar;
        CancelButton = _cancelar;
        _loading = true;
        _atendimento.SelectedIndex = 2;
        _tipoPreco.SelectedIndex = 0;
        _loading = false;
        UpdateConditionalFields();
        UpdateActions();
    }

    private static Label AddField(TableLayoutPanel table, string title, Control control)
    {
        int row = table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var label = new Label
        {
            Text = title, Font = new Font("Segoe UI", 10, FontStyle.Bold),
            ForeColor = Color.FromArgb(45, 42, 99), AutoSize = true,
            Margin = new Padding(0, 12, 12, 12)
        };
        table.Controls.Add(label, 0, row);
        control.Margin = new Padding(0, 8, 0, 8);
        table.Controls.Add(control, 1, row);
        return label;
    }

    private static TextBox TextField(int maxLength) => new()
    {
        MaxLength = maxLength, Dock = DockStyle.Top, Height = 32,
        BorderStyle = BorderStyle.FixedSingle
    };

    private static ComboBox SelectField() => new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Top,
        IntegralHeight = false, Height = 32
    };

    private static DateTimePicker TimeField(int hour) => new()
    {
        Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm",
        ShowUpDown = true, Width = 120, Value = DateTime.Today.AddHours(hour)
    };

    private static Button ActionButton(string text, Color background)
    {
        var button = new Button
        {
            Text = text, Size = new Size(132, 40), BackColor = background,
            ForeColor = Color.White, FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand, Margin = new Padding(8, 0, 0, 0)
        };
        button.FlatAppearance.BorderSize = 0;
        return button;
    }

    private string? Atendimento => (_atendimento.SelectedItem as Choice)?.Value;
    private string? TipoPreco => (_tipoPreco.SelectedItem as Choice)?.Value;

    private void UpdateConditionalFields()
    {
        if (_local is null || _link is null || _preco is null) return;
        bool local = Atendimento == "Local";
        bool online = Atendimento == "Online";
        _local.Visible = _localLabel.Visible = local;
        _localHint.Visible = local && _locaisLoaded && _local.Items.Count == 0;
        _link.Visible = _linkLabel.Visible = online;
        _preco.Enabled = TipoPreco == "Fixo";
    }

    private void UpdateActions()
    {
        _salvar.Enabled = !_busy && !_loadingLocais && _categoria.SelectedItem is CategoriaResponse;
        _cancelar.Enabled = !_busy;
    }

    private void PopulateSubcategories()
    {
        var category = _categoria.SelectedItem as CategoriaResponse;
        _subcategoria.Items.Clear();
        if (category?.Subcategorias is not null)
            foreach (var sub in category.Subcategorias) _subcategoria.Items.Add(sub);
        if (_subcategoria.Items.Count > 0) _subcategoria.SelectedIndex = 0;
    }

    private async Task LoadAsync()
    {
        if (_busy || IsDisposed) return;
        _busy = true;
        UpdateActions();
        try
        {
            var categories = await _api.ListarCategoriasAsync();
            if (IsDisposed) return;
            _loading = true;
            foreach (var category in categories) _categoria.Items.Add(category);
            if (_original is null)
            {
                if (_categoria.Items.Count > 0) _categoria.SelectedIndex = 0;
                PopulateSubcategories();
            }
            else
            {
                _nome.Text = _original.Nome;
                _descricao.Text = _original.Descricao;
                _link.Text = _original.LinkOnline ?? "";
                _preco.Value = Math.Clamp(_original.PrecoBase ?? 0, 0, _preco.Maximum);
                SelectChoice(_atendimento, _original.Atendimento);
                SelectChoice(_tipoPreco, _original.TipoPreco);
                var category = categories.FirstOrDefault(c =>
                    c.Subcategorias.Any(s => s.Id == _original.SubcategoriaId));
                if (category is null)
                    throw new ApiException("A classificação atual do serviço não está no catálogo.");
                _categoria.SelectedItem = category;
                PopulateSubcategories();
                _subcategoria.SelectedItem = category.Subcategorias.First(s => s.Id == _original.SubcategoriaId);
                _categoria.Enabled = false;
                _subcategoria.Enabled = false;
                var days = (_original.DiasSemana ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries);
                foreach (var checkbox in _days)
                    checkbox.Checked = days.Contains(((int)checkbox.Tag!).ToString());
                if (_original.HoraInicio.HasValue)
                    _horaInicio.Value = DateTime.Today.Add(_original.HoraInicio.Value);
                if (_original.HoraFim.HasValue)
                    _horaFim.Value = DateTime.Today.Add(_original.HoraFim.Value);
            }
            _loading = false;
            UpdateConditionalFields();
            if (Atendimento == "Local" && !await LoadLocaisAsync()) return;
            if (IsDisposed) return;
            _status.ForeColor = Color.FromArgb(73, 77, 103);
            _status.Text = categories.Count == 0 ? "Nenhuma categoria disponível para cadastro." :
                _original is { HoraInicio: null } or { HoraFim: null }
                    ? "Confira a disponibilidade antes de salvar este serviço." : "Preencha os dados do serviço.";
        }
        catch (ApiException ex)
        {
            if (IsDisposed) return;
            ShowError(ex);
        }
        finally
        {
            _loading = false;
            _busy = false;
            if (!IsDisposed) UpdateActions();
        }
    }

    private async Task<bool> LoadLocaisAsync()
    {
        if (_locaisLoaded) return true;
        if (_loadingLocais || IsDisposed) return false;
        _loadingLocais = true;
        UpdateActions();
        _status.Text = "Carregando locais...";
        try
        {
            var locais = await _api.ListarLocaisAsync(_session);
            if (IsDisposed) return false;
            _local.Items.Clear();
            foreach (var local in locais) _local.Items.Add(local);
            if (_original?.LocalId is int localId)
                _local.SelectedItem = locais.FirstOrDefault(l => l.Id == localId);
            else if (_local.Items.Count > 0) _local.SelectedIndex = 0;
            _locaisLoaded = true;
            UpdateConditionalFields();
            return true;
        }
        catch (ApiException ex)
        {
            if (!IsDisposed) ShowError(ex);
            return false;
        }
        finally
        {
            _loadingLocais = false;
            if (!IsDisposed) UpdateActions();
        }
    }

    private static void SelectChoice(ComboBox combo, string value)
    {
        foreach (Choice choice in combo.Items)
            if (choice.Value == value) { combo.SelectedItem = choice; return; }
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

    private bool TryBuildRequest(out ServicoRequest? request)
    {
        request = null;
        string nome = _nome.Text.Trim();
        string descricao = _descricao.Text.Trim();
        string? atendimento = Atendimento;
        string? tipoPreco = TipoPreco;
        var subcategoria = _subcategoria.SelectedItem as SubcategoriaResponse;
        if (string.IsNullOrWhiteSpace(nome) || nome.Length > 100)
        {
            _status.Text = "Informe um nome de até 100 caracteres.";
            return false;
        }
        if (descricao.Length > 255 || subcategoria is null || atendimento is null || tipoPreco is null)
        {
            _status.Text = "Confira a descrição, modalidade, preço e subcategoria.";
            return false;
        }
        int? localId = atendimento == "Local" ? (_local.SelectedItem as LocalResponse)?.Id : null;
        if (atendimento == "Local" && !localId.HasValue)
        {
            _status.Text = "Cadastre um local em Locais antes de usar atendimento Local.";
            return false;
        }
        string? link = atendimento == "Online" ? _link.Text.Trim() : null;
        if (atendimento == "Online" &&
            (link is null || link.Length > 255 || !Uri.TryCreate(link, UriKind.Absolute, out var uri) ||
             (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)))
        {
            _status.Text = "Informe um link online HTTP ou HTTPS válido (até 255 caracteres).";
            return false;
        }
        decimal? preco = tipoPreco == "Fixo" ? _preco.Value : null;
        if (preco is <= 0)
        {
            _status.Text = "O preço fixo deve ser maior que zero.";
            return false;
        }
        string days = string.Join(",", _days.Where(c => c.Checked).Select(c => (int)c.Tag!));
        var inicio = _horaInicio.Value.TimeOfDay;
        var fim = _horaFim.Value.TimeOfDay;
        if (days.Length == 0 || inicio == fim)
        {
            _status.Text = "Selecione ao menos um dia e horários inicial e final diferentes.";
            return false;
        }
        request = new ServicoRequest(_original?.SubcategoriaId ?? subcategoria.Id,
            nome, descricao, atendimento, localId, link, tipoPreco, preco,
            days, inicio, fim);
        return true;
    }

    private async Task SaveAsync()
    {
        if (_busy || _loadingLocais || !TryBuildRequest(out var dados) || dados is null) return;
        _busy = true;
        UpdateActions();
        _status.ForeColor = Color.FromArgb(84, 65, 218);
        _status.Text = "Salvando serviço...";
        try
        {
            if (_original is null)
                await _api.CriarServicoAsync(_session, dados);
            else
                await _api.EditarServicoAsync(_session, _original.Id, dados);
            if (IsDisposed) return;
            MessageBox.Show(this, _original is null ? "Serviço cadastrado." : "Serviço atualizado.",
                "TRAMPO", MessageBoxButtons.OK, MessageBoxIcon.Information);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (ApiException ex)
        {
            if (!IsDisposed) ShowError(ex);
        }
        finally
        {
            _busy = false;
            if (!IsDisposed) UpdateActions();
        }
    }
}