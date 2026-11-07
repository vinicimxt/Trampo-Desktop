namespace Trampo.Desktop.Api;

internal sealed record ServicoProprioResponse(
    int Id, int SubcategoriaId, string Nome, string Descricao,
    string Atendimento, int? LocalId, string? LinkOnline,
    string TipoPreco, decimal? PrecoBase, bool Ativo,
    string DiasSemana, TimeSpan? HoraInicio, TimeSpan? HoraFim,
    string? Categoria, string? Subcategoria);

internal sealed record SubcategoriaResponse(int Id, string Nome);
internal sealed record CategoriaResponse(int Id, string Nome, IReadOnlyList<SubcategoriaResponse> Subcategorias);


internal sealed record ServicoRequest(
    int SubcategoriaId, string Nome, string? Descricao, string Atendimento,
    int? LocalId, string? LinkOnline, string TipoPreco, decimal? PrecoBase,
    string DiasSemana, TimeSpan HoraInicio, TimeSpan HoraFim);

internal sealed record ServicoCriadoResponse(int Id);
internal sealed record RemocaoServicoResponse(int Id, string Resultado);
internal sealed record CatalogoServicoResponse(int Id, int ProfissionalId, int SubcategoriaId,
    string Nome, string? Descricao, string Atendimento, string TipoPreco, decimal? PrecoBase,
    bool Ativo, string? NomeProfissional, string? Categoria, string? Subcategoria,
    double? MediaAvaliacoes = null, int TotalAvaliacoes = 0);
internal sealed record AgendaResponse(int ServicoId, DateOnly Data, IReadOnlyList<TimeSpan> Horarios);
