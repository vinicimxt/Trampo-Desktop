using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Trampo.Desktop.Session;

namespace Trampo.Desktop.Api;

internal sealed class TrampoApiClient : IDisposable
{
    private const string DefaultBaseUrl = "http://localhost:5165/";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;

    public TrampoApiClient()
    {
        var configuredUrl = Environment.GetEnvironmentVariable("TRAMPO_API_BASE_URL");
        var baseUrl = string.IsNullOrWhiteSpace(configuredUrl) ? DefaultBaseUrl : configuredUrl.Trim();
        if (!baseUrl.EndsWith('/')) baseUrl += "/";
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new InvalidOperationException("TRAMPO_API_BASE_URL deve ser uma URL HTTP ou HTTPS válida.");

        _http = new HttpClient { BaseAddress = uri, Timeout = TimeSpan.FromSeconds(15) };
    }

    internal TrampoApiClient(HttpClient http)
    {
        _http = http;
    }
    public Task<UserSession> SignInProfessionalAsync(string email, string senha) => ExecuteAsync(async () =>
    {
        using var loginResponse = await _http.PostAsJsonAsync(
            "api/v1/auth/login", new LoginRequest(email, senha), JsonOptions);
        EnsureSuccess(loginResponse, isLogin: true);
        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions);
        if (string.IsNullOrWhiteSpace(login?.AccessToken) ||
            !string.Equals(login.TokenType, "Bearer", StringComparison.OrdinalIgnoreCase))
            throw new ApiException("A API retornou uma resposta de login inválida.");

        using var meRequest = new HttpRequestMessage(HttpMethod.Get, "api/v1/auth/me");
        meRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
        using var meResponse = await _http.SendAsync(meRequest);
        EnsureSuccess(meResponse);
        var usuario = await meResponse.Content.ReadFromJsonAsync<UsuarioResponse>(JsonOptions);
        if (usuario is null || usuario.Id <= 0 || string.IsNullOrWhiteSpace(usuario.Nome) ||
            string.IsNullOrWhiteSpace(usuario.Tipo))
            throw new ApiException("A API retornou um perfil inválido.");

        if (!string.Equals(usuario.Tipo, "profissional", StringComparison.OrdinalIgnoreCase))
            throw new ApiException(string.Equals(usuario.Tipo, "admin", StringComparison.OrdinalIgnoreCase)
                ? "Administradores devem acessar o TRAMPO pela Web."
                : "O aplicativo Desktop é destinado a profissionais.");

        var session = new UserSession();
        session.Start(login.AccessToken, usuario.Id, usuario.Nome, usuario.Tipo);
        return session;
    });

    public Task<IReadOnlyList<AgendamentoResponse>> GetAgendamentosRecebidosAsync(UserSession session) =>
        ExecuteAsync<IReadOnlyList<AgendamentoResponse>>(async () =>
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/v1/agendamentos?visao=recebidos");
            using var response = await SendAuthorizedAsync(request, session);
            var items = await response.Content.ReadFromJsonAsync<List<AgendamentoResponse>>(JsonOptions);
            return items ?? throw new ApiException("A API retornou uma lista de agendamentos inválida.");
        });

    public Task<AgendamentoResponse> GetAgendamentoAsync(UserSession session, int id) =>
        ExecuteAsync(async () =>
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"api/v1/agendamentos/{id}");
            using var response = await SendAuthorizedAsync(request, session);
            return await response.Content.ReadFromJsonAsync<AgendamentoResponse>(JsonOptions)
                ?? throw new ApiException("A API retornou um agendamento inválido.");
        });

    // O catálogo público fornece apenas dados atuais do serviço e pode não conter serviços inativos.
    public Task<ServicoResumoResponse?> GetServicoAtualAsync(int id) =>
        ExecuteAsync(async () =>
        {
            using var response = await _http.GetAsync($"api/v1/servicos/{id}");
            if (response.StatusCode == HttpStatusCode.NotFound) return null;
            EnsureSuccess(response);
            return await response.Content.ReadFromJsonAsync<ServicoResumoResponse>(JsonOptions);
        });

    public Task ConfirmarAgendamentoAsync(UserSession session, int id) =>
        PostAgendamentoAsync(session, id, "confirmar");

    public Task RecusarAgendamentoAsync(UserSession session, int id) =>
        PostAgendamentoAsync(session, id, "recusar");

    public Task FinalizarAgendamentoAsync(UserSession session, int id, decimal valorFinal) =>
        ExecuteAsync(async () =>
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"api/v1/agendamentos/{id}/finalizar")
            {
                Content = JsonContent.Create(new FinalizarRequest(valorFinal), options: JsonOptions)
            };
            using var response = await SendAuthorizedAsync(request, session);
            return true;
        });

    public Task<IReadOnlyList<ServicoProprioResponse>> ListarMeusServicosAsync(UserSession session) =>
        ExecuteAsync<IReadOnlyList<ServicoProprioResponse>>(async () =>
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/v1/servicos/me");
            using var response = await SendAuthorizedAsync(request, session);
            return await response.Content.ReadFromJsonAsync<List<ServicoProprioResponse>>(JsonOptions)
                ?? throw new ApiException("A API retornou uma lista de serviços inválida.");
        });

    public Task<IReadOnlyList<CategoriaResponse>> ListarCategoriasAsync() =>
        ExecuteAsync<IReadOnlyList<CategoriaResponse>>(async () =>
        {
            using var response = await _http.GetAsync("api/v1/categorias");
            EnsureSuccess(response);
            return await response.Content.ReadFromJsonAsync<List<CategoriaResponse>>(JsonOptions)
                ?? throw new ApiException("A API retornou categorias inválidas.");
        });

    public Task<IReadOnlyList<LocalResponse>> ListarLocaisAsync(UserSession session) =>
        ExecuteAsync<IReadOnlyList<LocalResponse>>(async () =>
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/v1/locais");
            using var response = await SendAuthorizedAsync(request, session);
            return await response.Content.ReadFromJsonAsync<List<LocalResponse>>(JsonOptions)
                ?? throw new ApiException("A API retornou locais inválidos.");
        });

    public Task<LocalResponse> BuscarLocalAsync(UserSession session, int id) =>
        ExecuteAsync(async () =>
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"api/v1/locais/{id}");
            using var response = await SendAuthorizedAsync(request, session);
            return await response.Content.ReadFromJsonAsync<LocalResponse>(JsonOptions)
                ?? throw new ApiException("A API retornou um local inválido.");
        });

    public Task<int> CriarLocalAsync(UserSession session, LocalRequest dados) => ExecuteAsync(async () =>
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/locais")
        {
            Content = JsonContent.Create(dados, options: JsonOptions)
        };
        using var response = await SendAuthorizedAsync(request, session);
        EnsureExpectedStatus(response, HttpStatusCode.Created);
        var criado = await response.Content.ReadFromJsonAsync<LocalCriadoResponse>(JsonOptions);
        return criado is { Id: > 0 } ? criado.Id :
            throw new ApiException("A API não confirmou o ID do local criado.");
    });

    public Task EditarLocalAsync(UserSession session, int id, LocalRequest dados) => ExecuteAsync(async () =>
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"api/v1/locais/{id}")
        {
            Content = JsonContent.Create(dados, options: JsonOptions)
        };
        using var response = await SendAuthorizedAsync(request, session);
        EnsureExpectedStatus(response, HttpStatusCode.NoContent);
        return true;
    });

    public Task ExcluirLocalAsync(UserSession session, int id) => ExecuteAsync(async () =>
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"api/v1/locais/{id}");
        using var response = await SendAuthorizedAsync(request, session);
        EnsureExpectedStatus(response, HttpStatusCode.NoContent);
        return true;
    });

    public Task<CepResponse> BuscarCepAsync(UserSession session, string cep) => ExecuteAsync(async () =>
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"api/v1/enderecos/cep/{Uri.EscapeDataString(cep)}");
        using var response = await SendAuthorizedAsync(request, session);
        return await response.Content.ReadFromJsonAsync<CepResponse>(JsonOptions)
            ?? throw new ApiException("A API retornou um CEP inválido.");
    });
    public Task<int> CriarServicoAsync(UserSession session, ServicoRequest dados) => ExecuteAsync(async () =>
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/servicos")
        {
            Content = JsonContent.Create(dados, options: JsonOptions)
        };
        using var response = await SendAuthorizedAsync(request, session);
        EnsureExpectedStatus(response, HttpStatusCode.Created);
        var criado = await response.Content.ReadFromJsonAsync<ServicoCriadoResponse>(JsonOptions);
        return criado is { Id: > 0 } ? criado.Id :
            throw new ApiException("A API não confirmou o ID do serviço criado.");
    });

    public Task EditarServicoAsync(UserSession session, int id, ServicoRequest dados) => ExecuteAsync(async () =>
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"api/v1/servicos/{id}")
        {
            Content = JsonContent.Create(dados, options: JsonOptions)
        };
        using var response = await SendAuthorizedAsync(request, session);
        EnsureExpectedStatus(response, HttpStatusCode.NoContent);
        return true;
    });

    public Task<RemocaoServicoResponse> RemoverServicoAsync(UserSession session, int id) => ExecuteAsync(async () =>
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"api/v1/servicos/{id}");
        using var response = await SendAuthorizedAsync(request, session);
        EnsureExpectedStatus(response, HttpStatusCode.OK);
        var removido = await response.Content.ReadFromJsonAsync<RemocaoServicoResponse>(JsonOptions);
        if (removido is null || removido.Id != id ||
            (removido.Resultado != "desativado" && removido.Resultado != "excluido"))
            throw new ApiException("A API retornou um resultado de remoção inválido.");
        return removido;
    });

    private static void EnsureExpectedStatus(HttpResponseMessage response, HttpStatusCode expected)
    {
        if (response.StatusCode != expected)
            throw new ApiException("A API retornou um status inesperado para a operação.", response.StatusCode);
    }
    private Task PostAgendamentoAsync(UserSession session, int id, string action) =>
        ExecuteAsync(async () =>
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"api/v1/agendamentos/{id}/{action}");
            using var response = await SendAuthorizedAsync(request, session);
            return true;
        });

    private async Task<HttpResponseMessage> SendAuthorizedAsync(HttpRequestMessage request, UserSession session)
    {
        if (string.IsNullOrWhiteSpace(session.Jwt))
            throw new ApiException("Sua sessão terminou. Entre novamente.", HttpStatusCode.Unauthorized);

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.Jwt);
        var response = await _http.SendAsync(request);
        try
        {
            EnsureSuccess(response, session: session);
            return response;
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }

    private static async Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
    {
        try { return await operation(); }
        catch (HttpRequestException)
        {
            throw new ApiException("Não foi possível conectar à API TRAMPO. Verifique se o servidor está ativo.");
        }
        catch (TaskCanceledException)
        {
            throw new ApiException("A API demorou para responder. Tente novamente.");
        }
        catch (JsonException)
        {
            throw new ApiException("A API retornou dados inválidos. Tente novamente mais tarde.");
        }
    }

    private static void EnsureSuccess(HttpResponseMessage response, bool isLogin = false, UserSession? session = null)
    {
        if (response.IsSuccessStatusCode) return;
        if (response.StatusCode == HttpStatusCode.Unauthorized) session?.Clear();

        var message = response.StatusCode switch
        {
            HttpStatusCode.BadRequest => "Confira os dados informados e tente novamente.",
            HttpStatusCode.Unauthorized when isLogin => "E-mail ou senha inválidos.",
            HttpStatusCode.Unauthorized => "Sua autenticação expirou. Entre novamente.",
            HttpStatusCode.Forbidden => "Você não tem permissão para acessar este recurso.",
            HttpStatusCode.NotFound => "O recurso solicitado não foi encontrado.",
            HttpStatusCode.Conflict => "Os dados foram alterados ou a operação conflita com uma regra atual. Atualize e tente novamente.",
            HttpStatusCode.TooManyRequests => "Muitas tentativas. Aguarde um minuto e tente novamente.",
            HttpStatusCode.ServiceUnavailable => "A API está indisponível no momento.",
            _ when (int)response.StatusCode >= 500 => "O servidor está indisponível. Tente novamente mais tarde.",
            _ => "Não foi possível concluir a operação."
        };
        throw new ApiException(message, response.StatusCode);
    }

    public void Dispose() => _http.Dispose();
}

