namespace Trampo.Desktop.Api;

internal sealed record PerfilResponse(string Nome, string Email, string? Telefone);
internal sealed record PerfilRequest(string Nome, string? Telefone);
internal sealed record SenhaRequest(string SenhaAtual, string NovaSenha, string ConfirmarSenha);
internal sealed record NotificacaoResponse(int Id, string Titulo, string Mensagem, DateTime DataCriacao, bool Lida);
internal sealed record SuporteRequest(string Tipo, string Assunto, string Mensagem);
internal sealed record ChamadoSuporteResponse(int Id, string Tipo, string Assunto, string Mensagem,
    string Status, DateTime DataCriacao);
