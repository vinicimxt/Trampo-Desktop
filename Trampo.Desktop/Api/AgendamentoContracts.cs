namespace Trampo.Desktop.Api;

internal sealed record AgendamentoResponse(
    int Id,
    int ServicoId,
    int ProfissionalId,
    DateOnly Data,
    TimeSpan Hora,
    string Status,
    string Descricao,
    string? EnderecoCliente,
    decimal? ValorFinal,
    string OrigemValorFinal,
    EnderecoAtendimentoResponse? EnderecoAtendimento,
    string? ServicoNome = null, string? ProfissionalNome = null, string? ClienteNome = null,
    string? Atendimento = null);

internal sealed record EnderecoAtendimentoResponse(
    string? CEP,
    string? Logradouro,
    string? Numero,
    string? Complemento,
    string? Bairro,
    string? Cidade,
    string? UF,
    string EnderecoFormatado,
    bool Estruturado);

internal sealed record ServicoResumoResponse(
    int Id,
    string Nome,
    string Atendimento,
    string TipoPreco,
    decimal? PrecoBase);

internal sealed record FinalizarRequest(decimal ValorFinal);
internal sealed record CriarAgendamentoRequest(int ServicoId, DateOnly Data, TimeSpan Hora,
    string? Descricao, EnderecoRequest? Endereco);
internal sealed record RecursoCriadoResponse(int Id);
internal sealed record EstadoAvaliacaoResponse(bool PodeAvaliar, bool JaAvaliado);
internal sealed record AvaliacaoRequest(int Nota, string? Comentario);
