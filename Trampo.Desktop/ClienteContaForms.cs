using Trampo.Desktop.Api;
using Trampo.Desktop.Session;

namespace Trampo.Desktop;

internal sealed class ClientePerfilForm : ClientePage
{
    private readonly TextBox _nome = new() { Width = 340, MaxLength = 100, AccessibleName = "Nome" };
    private readonly TextBox _email = new() { Width = 340, ReadOnly = true, AccessibleName = "E-mail" };
    private readonly TextBox _telefone = new() { Width = 220, MaxLength = 20, AccessibleName = "Telefone" };
    private readonly TextBox _senhaAtual = new() { Width = 260, UseSystemPasswordChar = true, AccessibleName = "Senha atual" };
    private readonly TextBox _senhaNova = new() { Width = 260, UseSystemPasswordChar = true, AccessibleName = "Nova senha" };
    private readonly TextBox _senhaConfirmacao = new() { Width = 260, UseSystemPasswordChar = true, AccessibleName = "Confirmar nova senha" };

    internal ClientePerfilForm(UserSession session, TrampoApiClient api) : base(session, api, "Meu perfil")
    {
        var fields = Fields();
        Body.Controls.Add(fields);
        Row(fields, "Nome", _nome);
        Row(fields, "E-mail (não editável)", _email);
        Row(fields, "Telefone", _telefone);
        AddField(fields, "Alterar senha", "Preencha os três campos abaixo quando quiser trocar a senha.");
        Row(fields, "Senha atual", _senhaAtual);
        Row(fields, "Nova senha", _senhaNova);
        Row(fields, "Confirmação", _senhaConfirmacao);
        AddAction("Salvar perfil", async (_, _) => await SaveProfileAsync());
        AddAction("Alterar senha", async (_, _) => await SavePasswordAsync());
        Shown += async (_, _) => await LoadAsync();
    }

    private static void Row(TableLayoutPanel fields, string label, Control input)
    {
        var row = fields.RowCount++;
        fields.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        fields.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(0, 10, 14, 10) }, 0, row);
        input.Margin = new Padding(0, 7, 0, 7);
        fields.Controls.Add(input, 1, row);
    }

    private Task LoadAsync() => RunAsync(async () =>
    {
        var profile = await Api.GetPerfilAsync(Session);
        if (IsDisposed) return;
        _nome.Text = profile.Nome;
        _email.Text = profile.Email;
        _telefone.Text = profile.Telefone ?? "";
        Status("Dados de perfil carregados.");
    }, "Carregando perfil...");

    private Task SaveProfileAsync() => RunAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(_nome.Text)) { Status("Informe seu nome.", true); return; }
        await Api.AtualizarPerfilAsync(Session, new PerfilRequest(_nome.Text.Trim(), _telefone.Text.Trim()));
        if (IsDisposed) return;
        Session.UpdateName(_nome.Text.Trim());
        Status("Perfil atualizado.");
    }, "Salvando perfil...");

    private Task SavePasswordAsync() => RunAsync(async () =>
    {
        if (string.IsNullOrEmpty(_senhaAtual.Text) || _senhaNova.Text.Length < 8 ||
            _senhaNova.Text != _senhaConfirmacao.Text)
        { Status("Confira a senha atual e a confirmação. A nova senha deve ter ao menos 8 caracteres.", true); return; }
        await Api.AlterarSenhaAsync(Session, new SenhaRequest(_senhaAtual.Text, _senhaNova.Text, _senhaConfirmacao.Text));
        if (IsDisposed) return;
        _senhaAtual.Clear(); _senhaNova.Clear(); _senhaConfirmacao.Clear();
        Status("Senha alterada com sucesso.");
    }, "Alterando senha...");
}

internal sealed class ClienteNotificacoesForm : ClientePage
{
    private readonly DataGridView _grid;
    private int _pagina = 1;
    private readonly Button _proxima;

    internal ClienteNotificacoesForm(UserSession session, TrampoApiClient api)
        : base(session, api, "Notificações")
    {
        _grid = Grid(("Titulo", "Título", 25), ("Mensagem", "Mensagem", 55),
            ("Data", "Data", 17), ("Estado", "Estado", 12));
        Body.Controls.Add(_grid);
        AddAction("Marcar como lida", async (_, _) => await MarkReadAsync());
        AddAction("Atualizar", async (_, _) => await LoadAsync(), true);
        _proxima = AddAction("Próxima página", async (_, _) => { _pagina++; await LoadAsync(); });
        AddAction("Página anterior", async (_, _) => { if (_pagina > 1) { _pagina--; await LoadAsync(); } }, true);
        Shown += async (_, _) => await LoadAsync();
    }

    private Task LoadAsync() => RunAsync(async () =>
    {
        var items = await Api.GetNotificacoesAsync(Session, _pagina);
        if (IsDisposed) return;
        _grid.Rows.Clear();
        foreach (var item in items)
        {
            var row = _grid.Rows[_grid.Rows.Add(item.Titulo, item.Mensagem,
                item.DataCriacao.ToString("dd/MM/yyyy HH:mm"), item.Lida ? "Lida" : "Nova")];
            row.Tag = item;
        }
        _grid.ClearSelection();
        _proxima.Enabled = items.Count == 20;
        Status(items.Count == 0 ? "Nenhuma notificação nesta página." : $"{items.Count} notificação(ões).");
    }, "Carregando notificações...");

    private async Task MarkReadAsync()
    {
        if (_grid.SelectedRows.Count != 1 || _grid.SelectedRows[0].Tag is not NotificacaoResponse item || item.Lida) return;
        await RunAsync(async () => await Api.LerNotificacaoAsync(Session, item.Id), "Marcando notificação...");
        if (!IsDisposed && !string.IsNullOrWhiteSpace(Session.Jwt)) await LoadAsync();
    }
}

internal sealed class ClienteSuporteForm : ClientePage
{
    private readonly DataGridView _grid;
    private readonly ComboBox _tipo = new() { Width = 150, DropDownStyle = ComboBoxStyle.DropDownList,
        AccessibleName = "Tipo de solicitação" };
    private readonly TextBox _assunto = new() { Width = 250, MaxLength = 150, AccessibleName = "Assunto" };
    private readonly TextBox _mensagem = new() { Width = 360, Height = 90, Multiline = true,
        MaxLength = 10000, AccessibleName = "Mensagem de suporte" };

    internal ClienteSuporteForm(UserSession session, TrampoApiClient api) : base(session, api, "Suporte")
    {
        _tipo.Items.AddRange(["Dúvida", "Problema", "Sugestão", "Outro"]);
        _tipo.SelectedIndex = 0;
        var compose = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 142, Padding = new Padding(8),
            WrapContents = true };
        compose.Controls.Add(new Label { Text = "Tipo", Width = 40, TextAlign = ContentAlignment.MiddleLeft });
        compose.Controls.Add(_tipo);
        compose.Controls.Add(new Label { Text = "Assunto", Width = 65, TextAlign = ContentAlignment.MiddleLeft });
        compose.Controls.Add(_assunto);
        compose.Controls.Add(new Label { Text = "Mensagem", Width = 85, TextAlign = ContentAlignment.MiddleLeft });
        compose.Controls.Add(_mensagem);
        _grid = Grid(("Data", "Data", 16), ("Tipo", "Tipo", 14),
            ("Assunto", "Assunto", 36), ("Status", "Status", 16));
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 142));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Body.Controls.Add(layout);
        layout.Controls.Add(compose, 0, 0);
        layout.Controls.Add(_grid, 0, 1);
        _grid.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) ShowDetail(); };
        AddAction("Enviar solicitação", async (_, _) => await SendAsync());
        AddAction("Ver solicitação", (_, _) => ShowDetail());
        AddAction("Atualizar", async (_, _) => await LoadAsync(), true);
        Shown += async (_, _) => await LoadAsync();
    }

    private Task LoadAsync() => RunAsync(async () =>
    {
        var items = await Api.GetChamadosAsync(Session);
        if (IsDisposed) return;
        _grid.Rows.Clear();
        foreach (var item in items)
        {
            var row = _grid.Rows[_grid.Rows.Add(item.DataCriacao.ToString("dd/MM/yyyy HH:mm"),
                item.Tipo, item.Assunto, item.Status)];
            row.Tag = item;
        }
        _grid.ClearSelection();
        Status(items.Count == 0 ? "Nenhuma solicitação de suporte." : $"{items.Count} solicitação(ões).");
    }, "Carregando suporte...");

    private async Task SendAsync()
    {
        if (string.IsNullOrWhiteSpace(_assunto.Text) || string.IsNullOrWhiteSpace(_mensagem.Text))
        { Status("Informe assunto e mensagem.", true); return; }
        await RunAsync(async () =>
        {
            await Api.EnviarSuporteAsync(Session, new SuporteRequest(_tipo.Text, _assunto.Text.Trim(), _mensagem.Text.Trim()));
            if (IsDisposed) return;
            _assunto.Clear(); _mensagem.Clear();
            Status("Solicitação enviada com sucesso.");
        }, "Enviando solicitação...");
        if (!IsDisposed && !string.IsNullOrWhiteSpace(Session.Jwt)) await LoadAsync();
    }

    private void ShowDetail()
    {
        if (_grid.SelectedRows.Count != 1 || _grid.SelectedRows[0].Tag is not ChamadoSuporteResponse item) return;
        MessageBox.Show(this, $"Assunto: {item.Assunto}\nTipo: {item.Tipo}\nStatus: {item.Status}\n" +
            $"Criado em: {item.DataCriacao:dd/MM/yyyy HH:mm}\n\n{item.Mensagem}",
            "Solicitação de suporte", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}
