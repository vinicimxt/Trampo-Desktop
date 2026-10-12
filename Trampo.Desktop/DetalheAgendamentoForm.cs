using System.Globalization;
using Trampo.Desktop.Api;
using Trampo.Desktop.Session;

namespace Trampo.Desktop;

internal sealed class DetalheAgendamentoForm : Form
{
    private static readonly CultureInfo Brasil = CultureInfo.GetCultureInfo("pt-BR");
    private readonly UserSession _session;
    private readonly TrampoApiClient _api;
    private readonly int _id;
    private AgendamentoResponse? _agendamento;
    private ServicoResumoResponse? _servico;
    private Label _title = null!;
    private Label _status = null!;
    private Panel _body = null!;
    private Button _confirmar = null!;
    private Button _recusar = null!;
    private Button _finalizar = null!;
    private bool _busy;

    public DetalheAgendamentoForm(UserSession session, TrampoApiClient api, int id)
    {
        _session = session;
        _api = api;
        _id = id;
        BuildUi();
        VisualIdentity.Apply(this);
        Shown += async (_, _) => await LoadAsync();
    }

    private void BuildUi()
    {
        Text = "TRAMPO | Detalhe do agendamento";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(790, 560);
        ClientSize = new Size(900, 650);
        BackColor = Color.FromArgb(247, 248, 252);
        Font = new Font("Segoe UI", 10);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(24) };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        Controls.Add(layout);

        _title = new Label
        {
            Text = $"Agendamento #{_id}",
            Font = new Font("Segoe UI", 22, FontStyle.Bold),
            ForeColor = Color.FromArgb(31, 35, 62),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
        layout.Controls.Add(_title, 0, 0);
        _status = new Label
        {
            Text = "Carregando detalhes...",
            ForeColor = Color.FromArgb(84, 65, 218),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
        layout.Controls.Add(_status, 0, 1);
        _body = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, AutoScroll = true };
        layout.Controls.Add(_body, 0, 2);

        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 15, 0, 0) };
        layout.Controls.Add(actions, 0, 3);
        var voltar = ActionButton("Voltar", Color.FromArgb(96, 101, 124));
        voltar.Click += (_, _) => Close();
        actions.Controls.Add(voltar);
        _finalizar = ActionButton("Finalizar", Color.FromArgb(84, 65, 218));
        _finalizar.Click += async (_, _) => await ChangeAsync("finalizar");
        actions.Controls.Add(_finalizar);
        _recusar = ActionButton("Recusar", Color.FromArgb(173, 45, 57));
        _recusar.Click += async (_, _) => await ChangeAsync("recusar");
        actions.Controls.Add(_recusar);
        _confirmar = ActionButton("Confirmar", Color.FromArgb(40, 126, 94));
        _confirmar.Click += async (_, _) => await ChangeAsync("confirmar");
        actions.Controls.Add(_confirmar);
        UpdateActions();
    }

    private static Button ActionButton(string text, Color background)
    {
        var button = new Button
        {
            Text = text,
            Size = new Size(124, 42),
            BackColor = background,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Margin = new Padding(7, 0, 0, 0)
        };
        button.FlatAppearance.BorderSize = 0;
        return button;
    }

    private async Task LoadAsync()
    {
        if (_busy || IsDisposed) return;
        _busy = true;
        UpdateActions();
        _status.ForeColor = Color.FromArgb(84, 65, 218);
        _status.Text = "Carregando detalhes...";
        try
        {
            var item = await _api.GetAgendamentoAsync(_session, _id);
            if (IsDisposed) return;
            _agendamento = item;
            string? serviceWarning = null;
            try
            {
                _servico = await _api.GetServicoAtualAsync(item.ServicoId);
            }
            catch (ApiException)
            {
                _servico = null;
                serviceWarning = "Dados atuais do serviço indisponíveis.";
            }
            if (IsDisposed) return;
            RenderDetail(item, _servico);
            _status.ForeColor = Color.FromArgb(73, 77, 103);
            _status.Text = serviceWarning ?? "Dados atualizados.";
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

    private void RenderDetail(AgendamentoResponse item, ServicoResumoResponse? servico)
    {
        _body.Controls.Clear();
        var rows = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top,
            ColumnCount = 2,
            Padding = new Padding(24)
        };
        rows.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        rows.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _body.Controls.Add(rows);

        AddField(rows, "Serviço", servico is null ? $"#{item.ServicoId}" : $"{servico.Nome} (cadastro atual)");
        AddField(rows, "Cliente", "Nome não disponibilizado pela API");
        AddField(rows, "Data e horário", $"{item.Data:dd/MM/yyyy} às {item.Hora.ToString(@"hh\:mm")}");
        if (servico is not null)
            AddField(rows, "Modalidade", $"{servico.Atendimento} (cadastro atual)");
        AddField(rows, "Status", AgendamentoStatus.Text(item.Status), AgendamentoStatus.StatusColor(item.Status));
        if (!string.IsNullOrWhiteSpace(item.Descricao))
            AddField(rows, "Descrição", item.Descricao);
        var address = item.EnderecoAtendimento?.EnderecoFormatado;
        if (string.IsNullOrWhiteSpace(address)) address = item.EnderecoCliente;
        if (!string.IsNullOrWhiteSpace(address))
            AddField(rows, "Endereço do atendimento", address);
        if (item.ValorFinal.HasValue)
            AddField(rows, "Valor final", item.ValorFinal.Value.ToString("C", Brasil));
    }

    private static void AddField(TableLayoutPanel table, string name, string value, Color? valueColor = null)
    {
        var row = table.RowCount++;
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.Controls.Add(new Label
        {
            Text = name,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            ForeColor = Color.FromArgb(45, 42, 99),
            AutoSize = true,
            Margin = new Padding(0, 10, 12, 10)
        }, 0, row);
        table.Controls.Add(new Label
        {
            Text = value,
            ForeColor = valueColor ?? Color.FromArgb(53, 57, 78),
            AutoSize = true,
            MaximumSize = new Size(560, 0),
            Margin = new Padding(0, 10, 0, 10)
        }, 1, row);
    }

    private void UpdateActions()
    {
        var status = _agendamento?.Status;
        var pending = string.Equals(status, "Pendente", StringComparison.OrdinalIgnoreCase);
        var confirmed = string.Equals(status, "Confirmado", StringComparison.OrdinalIgnoreCase);
        var started = _agendamento is not null &&
            _agendamento.Data.ToDateTime(TimeOnly.MinValue).Add(_agendamento.Hora) <= DateTime.Now;
        _confirmar.Enabled = !_busy && pending;
        _recusar.Enabled = !_busy && pending;
        _finalizar.Enabled = !_busy && confirmed && started;
    }

    private async Task ChangeAsync(string action)
    {
        if (_busy || _agendamento is null) return;
        decimal? valor = null;
        if (action == "finalizar")
        {
            valor = AskFinalValue();
            if (!valor.HasValue) return;
        }

        var verb = action switch { "confirmar" => "confirmar", "recusar" => "recusar", _ => "finalizar" };
        if (MessageBox.Show(this, $"Deseja {verb} o agendamento #{_id}?", "Confirmar ação",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        _busy = true;
        UpdateActions();
        _status.ForeColor = Color.FromArgb(84, 65, 218);
        _status.Text = "Salvando alteração...";
        try
        {
            switch (action)
            {
                case "confirmar": await _api.ConfirmarAgendamentoAsync(_session, _id); break;
                case "recusar": await _api.RecusarAgendamentoAsync(_session, _id); break;
                default: await _api.FinalizarAgendamentoAsync(_session, _id, valor!.Value); break;
            }
            if (IsDisposed) return;
            MessageBox.Show(this, "Agendamento atualizado.", "TRAMPO", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (ApiException ex)
        {
            if (IsDisposed) return;
            MessageBox.Show(this, ex.Message, ex.IsUnauthorized ? "Sessão encerrada" : "Não foi possível atualizar",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            if (ex.IsUnauthorized)
            {
                Close();
                return;
            }
        }
        finally
        {
            _busy = false;
            if (!IsDisposed) UpdateActions();
        }
        if (!IsDisposed) await LoadAsync();
    }

    private decimal? AskFinalValue()
    {
        using var dialog = new Form
        {
            Text = "Valor final",
            Icon = VisualIdentity.Icon,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ClientSize = new Size(390, 195),
            Font = Font
        };
        var fixedPrice = string.Equals(_servico?.TipoPreco, "Fixo", StringComparison.OrdinalIgnoreCase)
            && _servico?.PrecoBase is > 0 and <= 99999999.99m;
        dialog.Controls.Add(new Label
        {
            Text = fixedPrice ? "Preço fixo atual do serviço (R$)" : "Informe o valor final (R$)",
            Bounds = new Rectangle(24, 22, 340, 30)
        });
        var input = new NumericUpDown
        {
            DecimalPlaces = 2,
            Minimum = 0.01m,
            Maximum = 99999999.99m,
            Increment = 1,
            Value = fixedPrice ? _servico!.PrecoBase!.Value : 1,
            Enabled = !fixedPrice,
            Bounds = new Rectangle(24, 58, 340, 32),
            ThousandsSeparator = true
        };
        dialog.Controls.Add(input);
        dialog.Controls.Add(new Label
        {
            Text = "A API valida o valor e as regras de finalização.",
            ForeColor = Color.FromArgb(96, 101, 124),
            Bounds = new Rectangle(24, 100, 340, 30)
        });
        var ok = ActionButton("Continuar", Color.FromArgb(84, 65, 218));
        ok.Location = new Point(120, 140);
        ok.DialogResult = DialogResult.OK;
        dialog.Controls.Add(ok);
        var cancel = ActionButton("Cancelar", Color.FromArgb(96, 101, 124));
        cancel.Location = new Point(252, 140);
        cancel.DialogResult = DialogResult.Cancel;
        dialog.Controls.Add(cancel);
        dialog.AcceptButton = ok;
        dialog.CancelButton = cancel;
        return dialog.ShowDialog(this) == DialogResult.OK ? input.Value : null;
    }
}


