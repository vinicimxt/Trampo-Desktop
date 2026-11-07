using Trampo.Desktop.Api;
using Trampo.Desktop.Session;

namespace Trampo.Desktop;

internal sealed class BuscarServicosForm : ClientePage
{
    private readonly TextBox _busca = new() { Width = 210, PlaceholderText = "Serviço ou palavra-chave", AccessibleName = "Busca textual" };
    private readonly TextBox _profissional = new() { Width = 180, PlaceholderText = "Nome do profissional", AccessibleName = "Profissional" };
    private readonly ComboBox _modalidade = new() { Width = 140, DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Modalidade" };
    private readonly ComboBox _categoria = new() { Width = 180, DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Categoria" };
    private readonly ComboBox _subcategoria = new() { Width = 180, DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Subcategoria" };
    private readonly DataGridView _grid;
    private readonly Button _proxima;
    private int _pagina = 1;
    private IReadOnlyList<CategoriaResponse> _categorias = [];

    internal BuscarServicosForm(UserSession session, TrampoApiClient api) : base(session, api, "Buscar serviços")
    {
        var filters = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 112, Padding = new Padding(8),
            AutoScroll = true, WrapContents = true };
        foreach (var control in new Control[] { _busca, _profissional, _modalidade, _categoria, _subcategoria })
            filters.Controls.Add(control);
        _modalidade.Items.AddRange(["Todas as modalidades", "Local", "Domicilio", "Online"]);
        _modalidade.SelectedIndex = 0;
        _categoria.SelectedIndexChanged += (_, _) => FillSubcategories();
        var search = new Button { Text = "Buscar", Width = 100, Height = 32, AccessibleName = "Buscar serviços" };
        search.Click += async (_, _) => { _pagina = 1; await LoadServicesAsync(); };
        filters.Controls.Add(search);
        AcceptButton = search;
        _grid = Grid(("Nome", "Serviço", 28), ("Profissional", "Profissional", 22),
            ("Categoria", "Categoria", 20), ("Modalidade", "Modalidade", 13),
            ("Preco", "Preço", 15), ("Nota", "Avaliação", 12));
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Body.Controls.Add(layout);
        layout.Controls.Add(filters, 0, 0);
        layout.Controls.Add(_grid, 0, 1);
        _grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) OpenDetail(); };
        AddAction("Ver detalhes", (_, _) => OpenDetail());
        _proxima = AddAction("Próxima página", async (_, _) => { _pagina++; await LoadServicesAsync(); });
        AddAction("Página anterior", async (_, _) => { if (_pagina > 1) { _pagina--; await LoadServicesAsync(); } }, true);
        Shown += async (_, _) => await RunAsync(async () =>
        {
            _categorias = await Api.ListarCategoriasAsync();
            if (IsDisposed) return;
            _categoria.Items.Add(new FilterOption(0, "Todas as categorias"));
            foreach (var c in _categorias) _categoria.Items.Add(new FilterOption(c.Id, c.Nome));
            _categoria.SelectedIndex = 0;
            await FetchServicesAsync();
        }, "Carregando catálogo...");
    }

    private void FillSubcategories()
    {
        _subcategoria.Items.Clear();
        _subcategoria.Items.Add(new FilterOption(0, "Todas as subcategorias"));
        var id = (_categoria.SelectedItem as FilterOption)?.Id ?? 0;
        foreach (var sub in _categorias.FirstOrDefault(c => c.Id == id)?.Subcategorias ?? [])
            _subcategoria.Items.Add(new FilterOption(sub.Id, sub.Nome));
        _subcategoria.SelectedIndex = 0;
    }

    private Task LoadServicesAsync() => RunAsync(FetchServicesAsync, "Buscando serviços...");
    private async Task FetchServicesAsync()
    {
        var categoria = (_categoria.SelectedItem as FilterOption)?.Id ?? 0;
        var subcategoria = (_subcategoria.SelectedItem as FilterOption)?.Id ?? 0;
        var modalidade = _modalidade.SelectedIndex <= 0 ? null : _modalidade.SelectedItem?.ToString();
        var results = await Api.BuscarServicosAsync(_pagina, _busca.Text, modalidade,
            categoria == 0 ? null : categoria, subcategoria == 0 ? null : subcategoria, _profissional.Text);
        if (IsDisposed) return;
        _grid.Rows.Clear();
        foreach (var service in results)
        {
            var row = _grid.Rows[_grid.Rows.Add(service.Nome, service.NomeProfissional ?? "",
                string.Join(" / ", new[] { service.Categoria, service.Subcategoria }.Where(x => !string.IsNullOrWhiteSpace(x))),
                Modality(service.Atendimento), service.TipoPreco == "Fixo" ? Money(service.PrecoBase) : "A combinar",
                service.TotalAvaliacoes > 0 ? $"{service.MediaAvaliacoes:0.0} / 5" : "Sem avaliações")];
            row.Tag = service;
        }
        _grid.ClearSelection();
        _proxima.Enabled = results.Count == 20;
        Status(results.Count == 0 ? "Nenhum serviço encontrado." : $"Página {_pagina}: {results.Count} serviço(s)." );
    }

    private void OpenDetail()
    {
        if (_grid.SelectedRows.Count != 1 || _grid.SelectedRows[0].Tag is not CatalogoServicoResponse service) return;
        using var detail = new DetalheServicoForm(Session, Api, service.Id);
        detail.ShowDialog(this);
        if (string.IsNullOrWhiteSpace(Session.Jwt)) Close();
    }

    private sealed record FilterOption(int Id, string Label)
    {
        public override string ToString() => Label;
    }
}

internal sealed class DetalheServicoForm : ClientePage
{
    private readonly int _id;
    private CatalogoServicoResponse? _servico;
    private readonly Button _horarios;

    internal DetalheServicoForm(UserSession session, TrampoApiClient api, int id) : base(session, api, "Detalhe do serviço")
    {
        _id = id;
        _horarios = AddAction("Ver horários", (_, _) =>
        {
            if (_servico is null) return;
            using var form = new NovoAgendamentoForm(Session, Api, _servico);
            form.ShowDialog(this);
            if (string.IsNullOrWhiteSpace(Session.Jwt)) Close();
        });
        AddAction("Ver avaliações", (_, _) =>
        {
            if (_servico is null) return;
            var summary = _servico.TotalAvaliacoes == 0
                ? "Ainda não há avaliações para este profissional."
                : $"Média: {_servico.MediaAvaliacoes:0.0} / 5 — {_servico.TotalAvaliacoes} avaliação(ões).";
            MessageBox.Show(this, summary + "\nComentários individuais não estão disponíveis na API REST.",
                "Avaliações", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }, true);
        _horarios.Enabled = false;
        Shown += async (_, _) => await RunAsync(async () =>
        {
            _servico = await Api.GetServicoPublicoAsync(_id);
            if (IsDisposed) return;
            Body.Controls.Clear();
            var fields = Fields();
            Body.Controls.Add(fields);
            AddField(fields, "Serviço", _servico.Nome);
            AddField(fields, "Profissional", _servico.NomeProfissional);
            AddField(fields, "Categoria", _servico.Categoria);
            AddField(fields, "Subcategoria", _servico.Subcategoria);
            AddField(fields, "Modalidade", Modality(_servico.Atendimento));
            AddField(fields, "Preço atual", _servico.TipoPreco == "Fixo" ? Money(_servico.PrecoBase) : "A combinar");
            AddField(fields, "Descrição", _servico.Descricao);
            AddField(fields, "Avaliações", _servico.TotalAvaliacoes == 0
                ? "Ainda não há avaliações para este profissional."
                : $"{_servico.MediaAvaliacoes:0.0} / 5 — {_servico.TotalAvaliacoes} avaliação(ões)");
            _horarios.Enabled = true;
            Status("Dados do serviço carregados.");
        }, "Carregando serviço...");
    }
}

internal sealed class NovoAgendamentoForm : ClientePage
{
    private readonly CatalogoServicoResponse _servico;
    private readonly DateTimePicker _data = new() { Format = DateTimePickerFormat.Short, Width = 160, AccessibleName = "Data do agendamento" };
    private readonly ComboBox _hora = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160, AccessibleName = "Horário disponível" };
    private readonly TextBox _observacoes = new() { Multiline = true, Width = 380, Height = 70,
        MaxLength = 255, AccessibleName = "Observações" };
    private readonly TextBox _cep = new() { Width = 100, MaxLength = 9, AccessibleName = "CEP" };
    private readonly TextBox _rua = new() { Width = 290, AccessibleName = "Rua" };
    private readonly TextBox _numero = new() { Width = 100, AccessibleName = "Número" };
    private readonly TextBox _complemento = new() { Width = 180, AccessibleName = "Complemento" };
    private readonly TextBox _bairro = new() { Width = 200, AccessibleName = "Bairro" };
    private readonly TextBox _cidade = new() { Width = 220, AccessibleName = "Cidade" };
    private readonly TextBox _uf = new() { Width = 60, MaxLength = 2, AccessibleName = "UF" };
    private readonly Button _confirmar;

    internal NovoAgendamentoForm(UserSession session, TrampoApiClient api, CatalogoServicoResponse servico)
        : base(session, api, "Novo agendamento")
    {
        _servico = servico;
        _data.MinDate = DateTime.Today;
        _data.MaxDate = DateTime.Today.AddMonths(3);
        var stack = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
            WrapContents = false, AutoScroll = true, Padding = new Padding(18) };
        Body.Controls.Add(stack);
        stack.Controls.Add(new Label { Text = $"{servico.Nome} — {servico.NomeProfissional}",
            Font = new Font("Segoe UI", 16, FontStyle.Bold), AutoSize = true });
        stack.Controls.Add(new Label { Text = $"{Modality(servico.Atendimento)} · " +
            (servico.TipoPreco == "Fixo" ? Money(servico.PrecoBase) : "Valor a combinar"), AutoSize = true });
        stack.Controls.Add(Row("Data", _data));
        stack.Controls.Add(Row("Horário", _hora));
        if (servico.Atendimento == "Domicilio")
        {
            stack.Controls.Add(new Label { Text = "Endereço do atendimento", Font = new Font("Segoe UI", 12, FontStyle.Bold), AutoSize = true });
            var cepRow = Row("CEP", _cep);
            var consultar = new Button { Text = "Consultar CEP", Width = 130, AccessibleName = "Consultar CEP" };
            consultar.Click += async (_, _) => await ConsultarCepAsync();
            cepRow.Controls.Add(consultar);
            stack.Controls.Add(cepRow);
            foreach (var (label, box) in new[] { ("Rua", _rua), ("Número", _numero),
                ("Complemento", _complemento), ("Bairro", _bairro), ("Cidade", _cidade), ("UF", _uf) })
                stack.Controls.Add(Row(label, box));
        }
        stack.Controls.Add(Row("Observações", _observacoes));
        var refresh = AddAction("Ver horários", async (_, _) => await LoadTimesAsync());
        _confirmar = AddAction("Confirmar agendamento", async (_, _) => await ConfirmAsync());
        _confirmar.Width = 200;
        _confirmar.Enabled = false;
        _data.ValueChanged += async (_, _) => await LoadTimesAsync();
        Shown += async (_, _) => await LoadTimesAsync();
        AcceptButton = refresh;
    }

    private static FlowLayoutPanel Row(string label, Control input)
    {
        var row = new FlowLayoutPanel { Width = 680, Height = Math.Max(44, input.Height + 10), WrapContents = false };
        row.Controls.Add(new Label { Text = label, Width = 145, Height = 32, TextAlign = ContentAlignment.MiddleLeft });
        row.Controls.Add(input);
        return row;
    }

    private Task LoadTimesAsync() => RunAsync(async () =>
    {
        _hora.Items.Clear();
        _confirmar.Enabled = false;
        var date = DateOnly.FromDateTime(_data.Value);
        var agenda = await Api.ConsultarAgendaAsync(Session, _servico.Id, date);
        if (IsDisposed) return;
        foreach (var time in agenda.Horarios) _hora.Items.Add(time);
        if (_hora.Items.Count > 0) _hora.SelectedIndex = 0;
        _confirmar.Enabled = _hora.Items.Count > 0;
        Status(_hora.Items.Count == 0 ? "Sem horários disponíveis nesta data." :
            $"{_hora.Items.Count} horário(s) disponível(is).");
    }, "Consultando disponibilidade...");

    private Task ConsultarCepAsync() => RunAsync(async () =>
    {
        var cep = new string(_cep.Text.Where(char.IsDigit).ToArray());
        if (cep.Length != 8) { Status("Informe um CEP com 8 dígitos.", true); return; }
        var address = await Api.BuscarCepAsync(Session, cep);
        if (IsDisposed) return;
        _cep.Text = address.Cep;
        _rua.Text = address.Logradouro;
        _bairro.Text = address.Bairro;
        _cidade.Text = address.Cidade;
        _uf.Text = address.Uf;
        _numero.Focus();
        Status("CEP consultado. Confira e complete o número.");
    }, "Consultando CEP...");

    private async Task ConfirmAsync()
    {
        if (_hora.SelectedItem is not TimeSpan time) return;
        EnderecoRequest? address = null;
        if (_servico.Atendimento == "Domicilio")
        {
            var cep = new string(_cep.Text.Where(char.IsDigit).ToArray());
            if (cep.Length != 8 || new[] { _rua.Text, _numero.Text, _bairro.Text, _cidade.Text }.Any(string.IsNullOrWhiteSpace) ||
                _uf.Text.Trim().Length != 2)
            { Status("Preencha o endereço completo do atendimento.", true); return; }
            address = new EnderecoRequest(cep, _rua.Text.Trim(), _numero.Text.Trim(),
                _complemento.Text.Trim(), _bairro.Text.Trim(), _cidade.Text.Trim(), _uf.Text.Trim().ToUpperInvariant());
        }
        var date = DateOnly.FromDateTime(_data.Value);
        var summary = $"Serviço: {_servico.Nome}\nProfissional: {_servico.NomeProfissional}\n" +
            $"Data: {date:dd/MM/yyyy} às {time:hh\\:mm}\nModalidade: {Modality(_servico.Atendimento)}\n" +
            $"Preço atual: {(_servico.TipoPreco == "Fixo" ? Money(_servico.PrecoBase) : "A combinar")}";
        if (address is not null) summary += $"\nEndereço: {address.Logradouro}, {address.Numero} — {address.Cidade}/{address.Uf}";
        if (MessageBox.Show(this, summary + "\n\nConfirmar agendamento?", "Revisar pedido",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        _confirmar.Enabled = false;
        await RunAsync(async () =>
        {
            var id = await Api.CriarAgendamentoAsync(Session,
                new CriarAgendamentoRequest(_servico.Id, date, time, _observacoes.Text.Trim(), address));
            if (IsDisposed) return;
            MessageBox.Show(this, $"Agendamento solicitado com sucesso. Pedido #{id}.", "TRAMPO",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            DialogResult = DialogResult.OK;
            Close();
        }, "Enviando agendamento...");
        if (!IsDisposed && DialogResult != DialogResult.OK)
            Status("Confira Meus agendamentos antes de tentar novamente.", true);
    }
}
