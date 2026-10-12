namespace Trampo.Desktop.Session;

internal sealed class UserSession
{
    public string Jwt { get; private set; } = string.Empty;
    public int Id { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public string Perfil { get; private set; } = string.Empty;

    public void Start(string jwt, int id, string nome, string perfil)
    {
        Jwt = jwt;
        Id = id;
        Nome = nome;
        Perfil = perfil;
    }

    public void Clear()
    {
        Jwt = string.Empty;
        Id = 0;
        Nome = string.Empty;
        Perfil = string.Empty;
    }
}
