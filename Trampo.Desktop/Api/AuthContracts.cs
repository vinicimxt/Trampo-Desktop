namespace Trampo.Desktop.Api;

internal sealed record LoginRequest(string Email, string Senha);
internal sealed record LoginResponse(string AccessToken, string TokenType, DateTime ExpiresAt);
internal sealed record UsuarioResponse(int Id, string Nome, string Email, string Tipo);
