using System.Net;
using System.Text;
using System.Text.Json;
using Trampo.Desktop;
using Trampo.Desktop.Api;
using Trampo.Desktop.Session;

if (args.Contains("--preview"))
{
    string email = Environment.GetEnvironmentVariable("TRAMPO_SMOKE_EMAIL") ?? throw new Exception("E-mail de fixture ausente.");
    string password = Environment.GetEnvironmentVariable("TRAMPO_SMOKE_PASSWORD") ?? throw new Exception("Senha de fixture ausente.");
    using var api = new TrampoApiClient();
    var previewSession = await api.SignInProfessionalAsync(email, password);
    try
    {
        var uiThread = new Thread(() =>
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm(previewSession, api));
        });
        uiThread.SetApartmentState(ApartmentState.STA);
        uiThread.Start();
        uiThread.Join();
    }
    finally { previewSession.Clear(); }
    return;
}
if (args.Contains("--smoke-final"))
{
    string email = Environment.GetEnvironmentVariable("TRAMPO_SMOKE_EMAIL") ?? throw new Exception("E-mail de fixture ausente.");
    string password = Environment.GetEnvironmentVariable("TRAMPO_SMOKE_PASSWORD") ?? throw new Exception("Senha de fixture ausente.");
    using var api = new TrampoApiClient();
    var smokeSession = await api.SignInProfessionalAsync(email, password);
    try
    {
        Console.WriteLine("SMOKE FINAL: login profissional e /auth/me válidos.");
        var appointments = await api.GetAgendamentosRecebidosAsync(smokeSession);
        Console.WriteLine($"SMOKE FINAL: Agendamentos carregado ({appointments.Count}).");
        var services = await api.ListarMeusServicosAsync(smokeSession);
        Console.WriteLine($"SMOKE FINAL: Serviços carregado ({services.Count}).");
        var places = await api.ListarLocaisAsync(smokeSession);
        Console.WriteLine($"SMOKE FINAL: Locais carregado ({places.Count}).");
    }
    finally { smokeSession.Clear(); }
    if (smokeSession.Jwt != "") throw new Exception("Logout não limpou o token.");
    Console.WriteLine("SMOKE FINAL: logout limpou JWT em memória.");
    return;
}
if (args.Contains("--smoke-local"))
{
    string email = Environment.GetEnvironmentVariable("TRAMPO_SMOKE_EMAIL") ?? throw new Exception("E-mail de fixture ausente.");
    string password = Environment.GetEnvironmentVariable("TRAMPO_SMOKE_PASSWORD") ?? throw new Exception("Senha de fixture ausente.");
    string tag = Environment.GetEnvironmentVariable("TRAMPO_SMOKE_TAG") ?? throw new Exception("Tag de fixture ausente.");
    using var api = new TrampoApiClient();
    var smokeSession = await api.SignInProfessionalAsync(email, password);
    int createdId = 0;
    try
    {
        Console.WriteLine("SMOKE LOCAL: login profissional confirmado.");
        var before = await api.ListarLocaisAsync(smokeSession);
        Console.WriteLine($"SMOKE LOCAL: lista inicial com {before.Count} local(is).");
        var cep = await api.BuscarCepAsync(smokeSession, "01001000");
        if (cep.Cep != "01001000" || cep.Logradouro.Length == 0)
            throw new Exception("Consulta CEP não retornou endereço.");
        Console.WriteLine("SMOKE LOCAL: CEP consultado pela API.");
        var request = new LocalRequest(tag + " local", new EnderecoRequest(
            cep.Cep, cep.Logradouro, "12", null, cep.Bairro, cep.Cidade, cep.Uf));
        createdId = await api.CriarLocalAsync(smokeSession, request);
        var listed = (await api.ListarLocaisAsync(smokeSession)).Single(x => x.Id == createdId);
        if (listed.Nome != request.Nome || listed.DadosEndereco?.Numero != "12")
            throw new Exception("Cadastro não persistiu.");
        Console.WriteLine($"SMOKE LOCAL: local #{createdId} persistido.");
        var loaded = await api.BuscarLocalAsync(smokeSession, createdId);
        if (loaded.DadosEndereco?.Cep != cep.Cep) throw new Exception("GET por ID não retornou CEP.");
        var changed = request with { Nome = tag + " editado",
            DadosEndereco = request.DadosEndereco with { Numero = "25" } };
        await api.EditarLocalAsync(smokeSession, createdId, changed);
        var edited = await api.BuscarLocalAsync(smokeSession, createdId);
        if (edited.Nome != changed.Nome || edited.DadosEndereco?.Numero != "25")
            throw new Exception("Edição não persistiu.");
        Console.WriteLine("SMOKE LOCAL: edição persistida.");
        await api.ExcluirLocalAsync(smokeSession, createdId);
        createdId = 0;
        if ((await api.ListarLocaisAsync(smokeSession)).Any(x => x.Nome.StartsWith(tag)))
            throw new Exception("Exclusão não persistiu.");
        Console.WriteLine("SMOKE LOCAL: exclusão confirmada.");
        Console.WriteLine("SMOKE REAL DE LOCAIS PASSOU.");
    }
    finally
    {
        if (createdId > 0) await api.ExcluirLocalAsync(smokeSession, createdId);
        smokeSession.Clear();
    }
    return;
}if (args.Contains("--smoke"))
{
    string email = Environment.GetEnvironmentVariable("TRAMPO_SMOKE_EMAIL") ?? throw new Exception("E-mail de fixture ausente.");
    string password = Environment.GetEnvironmentVariable("TRAMPO_SMOKE_PASSWORD") ?? throw new Exception("Senha de fixture ausente.");
    string tag = Environment.GetEnvironmentVariable("TRAMPO_SMOKE_TAG") ?? throw new Exception("Tag de fixture ausente.");
    int inactiveId = int.Parse(Environment.GetEnvironmentVariable("TRAMPO_SMOKE_INACTIVE_ID") ?? "0");
    using var api = new TrampoApiClient();
    var smokeSession = await api.SignInProfessionalAsync(email, password);
    try
    {
        Console.WriteLine("SMOKE: login profissional e /auth/me válidos.");
        var smokeCategories = await api.ListarCategoriasAsync();
        var selected = smokeCategories.SelectMany(c => c.Subcategorias.Select(s => (Category: c, Subcategory: s))).FirstOrDefault();
        if (selected.Subcategory is null) throw new Exception("Catálogo sem subcategoria para o smoke.");
        Console.WriteLine($"SMOKE: catálogo real carregado: {selected.Category.Nome} / {selected.Subcategory.Nome}.");
        await api.ListarLocaisAsync(smokeSession);
        var initial = await api.ListarMeusServicosAsync(smokeSession);
        if (!initial.Any(s => s.Id == inactiveId && !s.Ativo))
            throw new Exception("Serviço inativo da fixture não apareceu na lista própria.");
        Console.WriteLine("SMOKE: lista própria inclui serviço inativo.");
        var request = new ServicoRequest(selected.Subcategory.Id, tag + " criado", "Descrição inicial",
            "Online", null, "https://example.invalid/consulta", "Fixo", 83.45m,
            "1,3", TimeSpan.FromHours(8), TimeSpan.FromHours(18));
        int id = await api.CriarServicoAsync(smokeSession, request);
        var created = (await api.ListarMeusServicosAsync(smokeSession)).Single(s => s.Id == id);
        if (created.Nome != request.Nome || created.PrecoBase != 83.45m ||
            created.SubcategoriaId != selected.Subcategory.Id)
            throw new Exception("Cadastro não persistiu na lista própria.");
        Console.WriteLine($"SMOKE: POST 201 e serviço #{id} persistido.");
        var changed = request with { Nome = tag + " editado", Descricao = "Descrição atualizada", PrecoBase = 91.20m };
        await api.EditarServicoAsync(smokeSession, id, changed);
        var edited = (await api.ListarMeusServicosAsync(smokeSession)).Single(s => s.Id == id);
        if (edited.Nome != changed.Nome || edited.Descricao != changed.Descricao ||
            edited.PrecoBase != changed.PrecoBase || edited.SubcategoriaId != selected.Subcategory.Id)
            throw new Exception("Edição não persistiu ou alterou a subcategoria.");
        Console.WriteLine("SMOKE: PUT 204, nome, descrição e preço persistidos; subcategoria preservada.");
        var removal = await api.RemoverServicoAsync(smokeSession, id);
        if (removal.Resultado != "excluido" ||
            (await api.ListarMeusServicosAsync(smokeSession)).Any(s => s.Id == id))
            throw new Exception("Remoção sem histórico não foi confirmada.");
        Console.WriteLine("SMOKE: DELETE 200/excluido e lista atualizada.");
        Console.WriteLine("SMOKE REAL PASSOU.");
    }
    finally { smokeSession.Clear(); }
    return;
}
var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
int passed = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FALHOU: " + name);
    Console.WriteLine("PASSOU: " + name);
    passed++;
}

var own = JsonSerializer.Deserialize<ServicoProprioResponse>(
    """{"id":7,"subcategoriaId":3,"nome":"Aula","descricao":"Online","atendimento":"Online","localId":null,"linkOnline":"https://example.invalid/aula","tipoPreco":"Fixo","precoBase":125.50,"ativo":false,"diasSemana":"1,3","horaInicio":"08:00:00","horaFim":"18:00:00","categoria":"Educação","subcategoria":"Reforço"}""", json);
Check(own is { Id: 7, Ativo: false, LocalId: null, PrecoBase: 125.50m, HoraInicio: { Hours: 8 } } &&
    own.DiasSemana == "1,3" && own.Categoria == "Educação" && own.Subcategoria == "Reforço",
    "desserialização do serviço próprio, inclusive disponibilidade e inativo");
var categories = JsonSerializer.Deserialize<List<CategoriaResponse>>(
    """[{"id":2,"nome":"Educação","subcategorias":[{"id":3,"nome":"Reforço"}]}]""", json);
Check(categories is { Count: 1 } && categories[0].Subcategorias.Single().Id == 3,
    "desserialização de categorias e subcategorias");
foreach (var modality in new[] { "Local", "Domicilio", "Online" })
foreach (var priceType in new[] { "Fixo", "Combinar" })
{
    var body = JsonSerializer.Serialize(new ServicoRequest(3, "Aula", "Descrição", modality,
        modality == "Local" ? 9 : null, modality == "Online" ? "https://example.invalid/aula" : null,
        priceType, priceType == "Fixo" ? 125.50m : null, "1,3", TimeSpan.FromHours(8), TimeSpan.FromHours(18)), json);
    using var parsed = JsonDocument.Parse(body);
    Check(parsed.RootElement.GetProperty("atendimento").GetString() == modality &&
        parsed.RootElement.GetProperty("tipoPreco").GetString() == priceType &&
        parsed.RootElement.GetProperty("precoBase").ValueKind == (priceType == "Combinar" ? JsonValueKind.Null : JsonValueKind.Number) &&
        parsed.RootElement.GetProperty("diasSemana").GetString() == "1,3" &&
        parsed.RootElement.GetProperty("horaInicio").GetString() == "08:00:00" &&
        !parsed.RootElement.TryGetProperty("profissionalId", out _),
        $"serialização {modality}/{priceType} sem ID do proprietário");
}

UserSession Session()
{
    var session = new UserSession();
    session.Start("jwt-temporario", 5, "Profissional", "profissional");
    return session;
}
HttpResponseMessage Response(HttpStatusCode status, string? body = null) =>
    new(status) { Content = new StringContent(body ?? "", Encoding.UTF8, "application/json") };
TrampoApiClient Client(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) =>
    new(new HttpClient(new StubHandler(send)) { BaseAddress = new Uri("http://localhost:5165/") });

var session = Session();
using (var client = Client((request, _) =>
{
    Check(request.Method == HttpMethod.Get && request.RequestUri?.AbsolutePath == "/api/v1/servicos/me" &&
        request.Headers.Authorization?.Scheme == "Bearer" &&
        request.Headers.Authorization.Parameter == session.Jwt,
        "lista própria usa rota correta e JWT Bearer");
    return Task.FromResult(Response(HttpStatusCode.OK, "[]"));
}))
    Check((await client.ListarMeusServicosAsync(session)).Count == 0, "lista própria vazia");

using (var client = Client((request, _) =>
{
    Check(request.Method == HttpMethod.Get && request.RequestUri?.AbsolutePath == "/api/v1/categorias" &&
        request.Headers.Authorization is null, "catálogo público usa rota própria");
    return Task.FromResult(Response(HttpStatusCode.OK, "[]"));
}))
    Check((await client.ListarCategoriasAsync()).Count == 0, "catálogo vazio");

using (var client = Client((request, _) =>
{
    Check(request.Method == HttpMethod.Get && request.RequestUri?.AbsolutePath == "/api/v1/locais" &&
        request.Headers.Authorization?.Parameter == session.Jwt, "locais para seleção usam Bearer");
    return Task.FromResult(Response(HttpStatusCode.OK, "[{\"id\":9,\"nome\":\"Sala\",\"endereco\":\"Rua A\"}]"));
}))
    Check((await client.ListarLocaisAsync(session)).Single().Id == 9, "local desserializado para seleção");

var requestBody = new ServicoRequest(3, "Aula", "Descrição", "Online", null,
    "https://example.invalid/aula", "Fixo", 125.50m, "1,3", TimeSpan.FromHours(8), TimeSpan.FromHours(18));
using (var client = Client(async (request, _) =>
{
    string body = await request.Content!.ReadAsStringAsync();
    Check(request.Method == HttpMethod.Post && request.RequestUri?.AbsolutePath == "/api/v1/servicos" &&
        request.Headers.Authorization?.Parameter == session.Jwt && body.Contains("\"subcategoriaId\":3") &&
        !body.Contains("profissionalId"), "POST usa JSON e identidade do JWT");
    return Response(HttpStatusCode.Created, "{\"id\":12}");
}))
    Check(await client.CriarServicoAsync(session, requestBody) == 12, "POST exige 201 e lê ID criado");

using (var client = Client((request, _) =>
{
    Check(request.Method == HttpMethod.Put && request.RequestUri?.AbsolutePath == "/api/v1/servicos/12" &&
        request.Headers.Authorization?.Parameter == session.Jwt, "PUT usa rota e Bearer corretos");
    return Task.FromResult(Response(HttpStatusCode.NoContent));
}))
{
    await client.EditarServicoAsync(session, 12, requestBody);
    Check(true, "PUT exige 204");
}

using (var client = Client((request, _) =>
{
    Check(request.Method == HttpMethod.Delete && request.RequestUri?.AbsolutePath == "/api/v1/servicos/12" &&
        request.Headers.Authorization?.Parameter == session.Jwt, "DELETE usa rota e Bearer corretos");
    return Task.FromResult(Response(HttpStatusCode.OK, "{\"id\":12,\"resultado\":\"desativado\"}"));
}))
    Check((await client.RemoverServicoAsync(session, 12)).Resultado == "desativado", "DELETE distingue desativação");

foreach (var status in new[] { HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden,
    HttpStatusCode.NotFound, HttpStatusCode.Conflict, HttpStatusCode.TooManyRequests, HttpStatusCode.ServiceUnavailable })
{
    var testSession = Session();
    using var client = Client((_, _) => Task.FromResult(Response(status)));
    try { await client.ListarMeusServicosAsync(testSession); throw new Exception("Status aceito indevidamente: " + status); }
    catch (ApiException ex)
    {
        Check(ex.StatusCode == status && (status != HttpStatusCode.Unauthorized || testSession.Jwt == "") &&
            (status == HttpStatusCode.Unauthorized || testSession.Jwt != ""),
            $"HTTP {(int)status} tratado e sessão limpa apenas em 401");
    }
}
using (var client = Client((_, _) => throw new HttpRequestException()))
{
    try { await client.ListarMeusServicosAsync(Session()); throw new Exception("Conexão aceita indevidamente"); }
    catch (ApiException ex) { Check(ex.Message.Contains("conectar"), "falha de conexão tratada"); }
}
using (var client = Client((_, _) => throw new TaskCanceledException()))
{
    try { await client.ListarMeusServicosAsync(Session()); throw new Exception("Timeout aceito indevidamente"); }
    catch (ApiException ex) { Check(ex.Message.Contains("demorou"), "timeout tratado"); }
}
var localJson = """{"id":17,"nome":"Sala","endereco":"Rua A, 12 - Centro, São Paulo/SP - CEP 01001000","dadosEndereco":{"cep":"01001000","logradouro":"Rua A","numero":"12","complemento":null,"bairro":"Centro","cidade":"São Paulo","uf":"SP","latitude":-23.5,"longitude":-46.6,"enderecoFormatado":"Rua A, 12 - Centro, São Paulo/SP - CEP 01001000","estruturado":true}}""";
var ownLocal = JsonSerializer.Deserialize<LocalResponse>(localJson, json);
Check(ownLocal is { Id: 17, DadosEndereco: { Estruturado: true, Cep: "01001000", Latitude: -23.5m } } &&
    LocalEnderecoFormatter.Format(ownLocal).Contains("CEP 01001-000"),
    "local estruturado, coordenadas de leitura e endereço amigável");
var legacy = JsonSerializer.Deserialize<LocalResponse>(
    """{"id":18,"nome":"Legado","endereco":"Endereço livre","dadosEndereco":null}""", json);
Check(legacy is not null && LocalEnderecoFormatter.Format(legacy) == "Endereço livre",
    "endereço legado preservado");
var localRequest = new LocalRequest("Sala", new EnderecoRequest(
    "01001000", "Rua A", "12", null, "Centro", "São Paulo", "SP"));
using (var payload = JsonDocument.Parse(JsonSerializer.Serialize(localRequest, json)))
{
    Check(payload.RootElement.GetProperty("dadosEndereco").GetProperty("numero").GetString() == "12" &&
        payload.RootElement.GetProperty("nome").GetString() == "Sala" &&
        !payload.RootElement.TryGetProperty("profissionalId", out _) &&
        !payload.RootElement.GetProperty("dadosEndereco").TryGetProperty("latitude", out _),
        "criação e edição serializam somente o contrato, sem proprietário nem coordenadas");
}
var cepResult = JsonSerializer.Deserialize<CepResponse>(
    """{"cep":"01001000","logradouro":"Praça da Sé","bairro":"Sé","cidade":"São Paulo","uf":"SP"}""", json);
Check(cepResult is { Cep: "01001000", Cidade: "São Paulo", Uf: "SP" }, "resposta de CEP desserializada");

using (var client = Client((request, _) =>
{
    Check(request.Method == HttpMethod.Get && request.RequestUri?.AbsolutePath == "/api/v1/locais/17" &&
        request.Headers.Authorization?.Scheme == "Bearer" &&
        request.Headers.Authorization.Parameter == session.Jwt, "GET local usa ID e Bearer");
    return Task.FromResult(Response(HttpStatusCode.OK, localJson));
}))
    Check((await client.BuscarLocalAsync(session, 17)).DadosEndereco?.Numero == "12", "GET local desserializa endereço");
using (var client = Client(async (request, _) =>
{
    var body = await request.Content!.ReadAsStringAsync();
    Check(request.Method == HttpMethod.Post && request.RequestUri?.AbsolutePath == "/api/v1/locais" &&
        request.Headers.Authorization?.Parameter == session.Jwt &&
        body.Contains("\"dadosEndereco\"") && !body.Contains("profissionalId"),
        "POST local usa Bearer e corpo esperado");
    return Response(HttpStatusCode.Created, "{\"id\":21}");
}))
    Check(await client.CriarLocalAsync(session, localRequest) == 21, "POST local exige 201");
using (var client = Client(async (request, _) =>
{
    var body = await request.Content!.ReadAsStringAsync();
    Check(request.Method == HttpMethod.Put && request.RequestUri?.AbsolutePath == "/api/v1/locais/21" &&
        request.Headers.Authorization?.Parameter == session.Jwt && body.Contains("\"numero\":\"12\""),
        "PUT local usa Bearer e corpo esperado");
    return Response(HttpStatusCode.NoContent);
}))
{ await client.EditarLocalAsync(session, 21, localRequest); Check(true, "PUT local exige 204"); }
using (var client = Client((request, _) =>
{
    Check(request.Method == HttpMethod.Delete && request.RequestUri?.AbsolutePath == "/api/v1/locais/21" &&
        request.Headers.Authorization?.Parameter == session.Jwt, "DELETE local usa Bearer");
    return Task.FromResult(Response(HttpStatusCode.NoContent));
}))
{ await client.ExcluirLocalAsync(session, 21); Check(true, "DELETE local exige 204"); }
using (var client = Client((request, _) =>
{
    Check(request.Method == HttpMethod.Get && request.RequestUri?.AbsolutePath == "/api/v1/enderecos/cep/01001000" &&
        request.Headers.Authorization?.Parameter == session.Jwt, "CEP usa API e Bearer");
    return Task.FromResult(Response(HttpStatusCode.OK,
        "{\"cep\":\"01001000\",\"logradouro\":\"Praça da Sé\",\"bairro\":\"Sé\",\"cidade\":\"São Paulo\",\"uf\":\"SP\"}"));
}))
    Check((await client.BuscarCepAsync(session, "01001000")).Cidade == "São Paulo", "busca CEP desserializada");
foreach (var status in new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden,
    HttpStatusCode.NotFound, HttpStatusCode.Conflict })
{
    var testSession = Session();
    using var client = Client((_, _) => Task.FromResult(Response(status)));
    try { await client.ExcluirLocalAsync(testSession, 21); throw new Exception("Status aceito: " + status); }
    catch (ApiException ex)
    {
        Check(ex.StatusCode == status && (status != HttpStatusCode.Unauthorized || testSession.Jwt == "") &&
            (status == HttpStatusCode.Unauthorized || testSession.Jwt != ""),
            $"local HTTP {(int)status} tratado");
    }
}
using (var client = Client((_, _) => Task.FromResult(Response(HttpStatusCode.NotFound))))
{
    try { await client.BuscarCepAsync(Session(), "99999999"); throw new Exception("CEP inexistente aceito"); }
    catch (ApiException ex) { Check(ex.StatusCode == HttpStatusCode.NotFound, "CEP não encontrado mantém status 404"); }
}foreach (var status in new[] { HttpStatusCode.BadRequest, HttpStatusCode.TooManyRequests,
    HttpStatusCode.ServiceUnavailable, HttpStatusCode.GatewayTimeout })
{
    using var client = Client((_, _) => Task.FromResult(Response(status)));
    try { await client.BuscarCepAsync(Session(), "01001000"); throw new Exception("Falha CEP aceita: " + status); }
    catch (ApiException ex)
    {
        Check(ex.StatusCode == status, $"CEP HTTP {(int)status} preserva status para edição manual");
    }
}
using (var client = Client((_, _) => throw new HttpRequestException()))
{
    try { await client.BuscarCepAsync(Session(), "01001000"); throw new Exception("Falha conexão CEP aceita"); }
    catch (ApiException ex) { Check(ex.Message.Contains("conectar"), "CEP falha de conexão tratada"); }
}
using (var client = Client((_, _) => throw new TaskCanceledException()))
{
    try { await client.BuscarCepAsync(Session(), "01001000"); throw new Exception("Timeout CEP aceito"); }
    catch (ApiException ex) { Check(ex.Message.Contains("demorou"), "CEP timeout tratado"); }
}
IEnumerable<Control> Descendants(Control control) => control.Controls.Cast<Control>()
    .SelectMany(child => new[] { child }.Concat(Descendants(child)));
Exception? formError = null;
var formThread = new Thread(() =>
{
    try
    {
        var formSession = Session();
        using var formApi = Client((_, _) => Task.FromResult(Response(HttpStatusCode.OK, "[]")));
        using var main = new MainForm(formSession, formApi);
        Check(Descendants(main).OfType<Button>().Any(b => b.Text == "Serviços") &&
            Descendants(main).OfType<Button>().Any(b => b.Text == "Agendamentos") &&
            Descendants(main).OfType<Button>().Any(b => b.Text == "Locais") &&
            Descendants(main).OfType<Button>().Any(b => b.Text == "Sair"),
            "MainForm preserva Agendamentos e Sair e expõe Serviços");
        using var list = new ServicosForm(formSession, formApi);
        var grid = Descendants(list).OfType<DataGridView>().Single();
        Check(grid.ReadOnly && !grid.AutoGenerateColumns &&
            grid.SelectionMode == DataGridViewSelectionMode.FullRowSelect &&
            grid.Columns["Status"] is not null,
            "ServicosForm usa grade de leitura com linha inteira e status textual");
        using var localList = new LocaisForm(formSession, formApi);
        var localGrid = Descendants(localList).OfType<DataGridView>().Single();
        Check(localGrid.ReadOnly && !localGrid.AutoGenerateColumns &&
            localGrid.SelectionMode == DataGridViewSelectionMode.FullRowSelect &&
            localGrid.Columns["Cep"] is not null && localGrid.Columns["Uf"] is not null,
            "LocaisForm usa grade somente leitura e endereço estruturado");
        using var localEdit = new LocalForm(formSession, formApi, 17);
        Check(Descendants(localEdit).OfType<Button>().Any(b => b.Text == "Buscar CEP"),
            "LocalForm oferece consulta CEP pela API");
        using var edit = new ServicoForm(formSession, formApi, own);
        Check(Descendants(edit).OfType<Label>().Any(l => l.Text.Contains("não podem ser alteradas")),
            "ServicoForm informa classificação bloqueada na edição");
        using var loginForm = new LoginForm();
        Check(ReferenceEquals(loginForm.Icon, VisualIdentity.Icon) && Descendants(loginForm).OfType<PictureBox>().Any(),
            "Login usa ícone e logo incorporados");
        Check(ReferenceEquals(main.Icon, VisualIdentity.Icon) && Descendants(main).OfType<PictureBox>().Count() == 1,
            "MainForm usa ícone e uma logo incorporada");
        loginForm.Size = loginForm.MinimumSize;
        loginForm.PerformLayout();
        var loginCard = Descendants(loginForm).OfType<Panel>()
            .Single(panel => panel.Controls.OfType<TextBox>().Any());
        Check(loginCard.Right <= loginCard.Parent!.ClientSize.Width &&
            loginCard.Bottom <= loginCard.Parent.ClientSize.Height,
            "cartão de login cabe no tamanho mínimo");
        main.Size = main.MinimumSize;
        main.PerformLayout();
        var sidebar = Descendants(main).OfType<Panel>()
            .Single(panel => panel.Controls.OfType<Button>().Any(button => button.Text == "Agendamentos"));
        Check(sidebar.Controls.OfType<Button>().All(button =>
            button.Right <= sidebar.ClientSize.Width && button.Bottom <= sidebar.ClientSize.Height),
            "menu principal cabe no tamanho mínimo");        void CheckFooter(Form form, string name)
        {
            form.Size = form.MinimumSize;
            form.PerformLayout();
            var footers = Descendants(form).OfType<FlowLayoutPanel>()
                .Where(panel => panel.Controls.OfType<Button>().Any()).ToArray();
            Check(footers.Length > 0 && footers.All(panel =>
                panel.Controls.OfType<Button>().All(button =>
                    button.Right <= panel.ClientSize.Width + 1 &&
                    button.Bottom <= panel.ClientSize.Height + 1)),
                name + " mantém botões visíveis no tamanho mínimo");
        }
        CheckFooter(list, "ServicosForm");
        CheckFooter(edit, "ServicoForm");
        CheckFooter(localList, "LocaisForm");
        CheckFooter(localEdit, "LocalForm");
        using var appointments = new AgendamentosForm(formSession, formApi);
        using var detail = new DetalheAgendamentoForm(formSession, formApi, 42);
        CheckFooter(appointments, "AgendamentosForm");
        CheckFooter(detail, "DetalheAgendamentoForm");
        Check(Descendants(appointments).OfType<DataGridView>().Single().RowTemplate.Height == 42,
            "grades de Agendamentos, Serviços e Locais usam mesma altura de linha");
    }
    catch (Exception ex) { formError = ex; }
});
formThread.SetApartmentState(ApartmentState.STA);
formThread.Start();
formThread.Join();
if (formError is not null) throw formError;Console.WriteLine($"TOTAL: {passed} verificações passaram.");

internal sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        send(request, cancellationToken);
}