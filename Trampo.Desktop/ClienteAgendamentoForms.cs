using Trampo.Desktop.Api;
using Trampo.Desktop.Session;

namespace Trampo.Desktop;

internal sealed class ClienteAgendamentosForm : ClientePage
{
    private readonly bool _avaliacoes;
    private readonly DataGridView _grid;

    internal ClienteAgendamentosForm(UserSession session, TrampoApiClient api, bool avaliacoes = false)
        : base(session, api, avaliacoes ? "Avaliações" : "Meus agendamentos")
    {
        _avaliacoes = avaliacoes;
        _grid = Grid(("Servico", "Serviço", 28), ("Profissional", "Profissional", 23),
            ("Data", "Data e hora", 20), ("Status", "Status", 17), ("Modalidade", "Modalidade", 15));
        Body.Controls.Add(_grid);
        _grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) OpenDetail(); };
        AddAction("Ver detalhes", (_, _) => OpenDetail());
        AddAction("Atualizar", async (_, _) => await LoadAsync(), true);
        Shown += async (_, _) => await LoadAsync();
    }

    private Task LoadAsync() => RunAsync(async () =>
    {
        var items = await Api.GetMeusAgendamentosAsync(Session);
        if (IsDisposed) return;
        _grid.Rows.Clear();
        foreach (var item in _avaliacoes ? items.Where(x => x.Status == "Finalizado") : items)
        {
            var index = _grid.Rows.Add(item.ServicoNome ?? "Serviço sem nome", item.ProfissionalNome ?? "",
                $"{item.Data:dd/MM/yyyy} às {item.Hora:hh\\:mm}", AgendamentoStatus.Text(item.Status),
                Modality(item.Atendimento));
            _grid.Rows[index].Tag = item.Id;
            _grid.Rows[index].Cells["Status"].Style.ForeColor = AgendamentoStatus.StatusColor(item.Status);
        }
        _grid.ClearSelection();
        Status(_grid.Rows.Count == 0 ? (_avaliacoes ? "Nenhum atendimento finalizado." : "Nenhum agendamento encontrado.") :
            $"{_grid.Rows.Count} pedido(s) encontrado(s).");
    }, "Carregando agendamentos...");

    private void OpenDetail()
    {
        if (_grid.SelectedRows.Count != 1 || _grid.SelectedRows[0].Tag is not int id) return;
        using var detail = new ClienteDetalheAgendamentoForm(Session, Api, id);
        detail.ShowDialog(this);
        if (string.IsNullOrWhiteSpace(Session.Jwt)) { Close(); return; }
        _ = LoadAsync();
    }
}

internal sealed class ClienteDetalheAgendamentoForm : ClientePage
{
    private readonly int _id;
    private AgendamentoResponse? _agendamento;
    private readonly Button _cancelar;
    private readonly Button _concluir;
    private readonly Button _avaliar;

    internal ClienteDetalheAgendamentoForm(UserSession session, TrampoApiClient api, int id)
        : base(session, api, "Detalhe do agendamento")
    {
        _id = id;
        _cancelar = AddAction("Cancelar pedido", async (_, _) => await ChangeAsync(false));
        _concluir = AddAction("Confirmar conclusão", async (_, _) => await ChangeAsync(true));
        _avaliar = AddAction("Avaliar", (_, _) => OpenEvaluation());
        UpdateActions(false);
        Shown += async (_, _) => await LoadAsync();
        AddAction("Atualizar", async (_, _) => await LoadAsync(), true);
    }

    private Task LoadAsync() => RunAsync(async () =>
    {
        var item = await Api.GetAgendamentoAsync(Session, _id);
        if (IsDisposed) return;
        _agendamento = item;
        decimal? currentPrice = null;
        if (!item.ValorFinal.HasValue)
        {
            try
            {
                var service = await Api.GetServicoAtualAsync(item.ServicoId);
                if (service?.TipoPreco == "Fixo") currentPrice = service.PrecoBase;
            }
            catch (ApiException) { /* Preço público atual é opcional para histórico. */ }
        }
        if (IsDisposed) return;
        Body.Controls.Clear();
        var fields = Fields();
        Body.Controls.Add(fields);
        AddField(fields, "Serviço", item.ServicoNome ?? "Serviço sem nome");
        AddField(fields, "Status", AgendamentoStatus.Text(item.Status));
        AddField(fields, "Profissional", item.ProfissionalNome);
        AddField(fields, "Data e horário", $"{item.Data:dd/MM/yyyy} às {item.Hora:hh\\:mm}");
        AddField(fields, "Modalidade", Modality(item.Atendimento));
        AddField(fields, "Endereço/local", item.EnderecoAtendimento?.EnderecoFormatado ?? item.EnderecoCliente);
        AddField(fields, "Observações", item.Descricao);
        if (currentPrice.HasValue) AddField(fields, "Preço atual do serviço", Money(currentPrice));
        if (item.ValorFinal.HasValue) AddField(fields, "Valor final", Money(item.ValorFinal));
        var canEvaluate = false;
        if (item.Status == "Finalizado")
        {
            var state = await Api.EstadoAvaliacaoAsync(Session, _id);
            canEvaluate = state.PodeAvaliar;
            AddField(fields, "Avaliação", state.JaAvaliado ? "Este atendimento já foi avaliado." :
                canEvaluate ? "Você pode avaliar este atendimento." : "Avaliação indisponível.");
        }
        UpdateActions(canEvaluate);
        Status("Dados atualizados.");
    }, "Carregando detalhes...");

    private void UpdateActions(bool canEvaluate)
    {
        _cancelar.Enabled = _agendamento?.Status is "Pendente" or "Confirmado";
        _concluir.Enabled = _agendamento?.Status == "AguardandoCliente";
        _avaliar.Enabled = canEvaluate;
    }

    private async Task ChangeAsync(bool conclusion)
    {
        if (_agendamento is null) return;
        var message = conclusion ? "Confirmar que o atendimento foi concluído?" :
            "Deseja realmente cancelar este agendamento?";
        if (MessageBox.Show(this, message, "Confirmar ação", MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) != DialogResult.Yes) return;
        _cancelar.Enabled = _concluir.Enabled = _avaliar.Enabled = false;
        await RunAsync(async () =>
        {
            if (conclusion) await Api.ConfirmarConclusaoAsync(Session, _id);
            else await Api.CancelarAgendamentoAsync(Session, _id);
        }, "Atualizando agendamento...");
        if (!IsDisposed && !string.IsNullOrWhiteSpace(Session.Jwt)) await LoadAsync();
    }

    private void OpenEvaluation()
    {
        if (_agendamento is null || !_avaliar.Enabled) return;
        using var form = new AvaliacaoForm(Session, Api, _agendamento);
        if (form.ShowDialog(this) == DialogResult.OK) _ = LoadAsync();
        if (string.IsNullOrWhiteSpace(Session.Jwt)) Close();
    }
}

internal sealed class AvaliacaoForm : ClientePage
{
    private readonly AgendamentoResponse _booking;
    private readonly NumericUpDown _nota = new() { Minimum = 1, Maximum = 5, Value = 5,
        Width = 80, AccessibleName = "Nota de 1 a 5" };
    private readonly TextBox _comentario = new() { Multiline = true, Width = 480, Height = 130,
        MaxLength = 500, AccessibleName = "Comentário opcional" };
    private readonly Button _enviar;

    internal AvaliacaoForm(UserSession session, TrampoApiClient api, AgendamentoResponse booking)
        : base(session, api, "Avaliar atendimento")
    {
        _booking = booking;
        var fields = Fields();
        Body.Controls.Add(fields);
        AddField(fields, "Serviço", booking.ServicoNome);
        AddField(fields, "Profissional", booking.ProfissionalNome);
        fields.Controls.Add(new Label { Text = "Nota", AutoSize = true }, 0, fields.RowCount);
        fields.Controls.Add(_nota, 1, fields.RowCount++);
        fields.Controls.Add(new Label { Text = "Comentário (opcional)", AutoSize = true }, 0, fields.RowCount);
        fields.Controls.Add(_comentario, 1, fields.RowCount++);
        _enviar = AddAction("Enviar avaliação", async (_, _) => await SubmitAsync());
        AcceptButton = _enviar;
        Shown += async (_, _) => await RunAsync(async () =>
        {
            var state = await Api.EstadoAvaliacaoAsync(Session, booking.Id);
            _enviar.Enabled = state.PodeAvaliar;
            Status(state.PodeAvaliar ? "Informe sua nota." : state.JaAvaliado ?
                "Este atendimento já foi avaliado." : "Avaliação indisponível.");
        }, "Conferindo permissão para avaliar...");
    }

    private async Task SubmitAsync()
    {
        _enviar.Enabled = false;
        await RunAsync(async () =>
        {
            await Api.AvaliarAsync(Session, _booking.Id, new AvaliacaoRequest((int)_nota.Value,
                _comentario.Text.Trim()));
            if (IsDisposed) return;
            MessageBox.Show(this, "Avaliação enviada com sucesso.", "TRAMPO", MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            DialogResult = DialogResult.OK;
            Close();
        }, "Enviando avaliação...");
    }
}
