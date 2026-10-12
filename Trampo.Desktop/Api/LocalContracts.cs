namespace Trampo.Desktop.Api;

internal sealed record EnderecoDadosResponse(
    string? Cep, string? Logradouro, string? Numero, string? Complemento,
    string? Bairro, string? Cidade, string? Uf, decimal? Latitude,
    decimal? Longitude, string EnderecoFormatado, bool Estruturado);

internal sealed record LocalResponse(int Id, string Nome, string Endereco, EnderecoDadosResponse? DadosEndereco);

internal sealed record EnderecoRequest(
    string Cep, string Logradouro, string Numero, string? Complemento,
    string Bairro, string Cidade, string Uf);

internal sealed record LocalRequest(string Nome, EnderecoRequest DadosEndereco);
internal sealed record LocalCriadoResponse(int Id);
internal sealed record CepResponse(string Cep, string Logradouro, string Bairro, string Cidade, string Uf);

internal static class LocalEnderecoFormatter
{
    public static string Format(LocalResponse local)
    {
        var address = local.DadosEndereco;
        if (address is not { Estruturado: true }) return local.Endereco;
        var street = string.Join(", ", new[] { address.Logradouro, address.Numero }
            .Where(x => !string.IsNullOrWhiteSpace(x)));
        if (!string.IsNullOrWhiteSpace(address.Complemento)) street += " - " + address.Complemento;
        var city = string.IsNullOrWhiteSpace(address.Uf) ? address.Cidade : address.Cidade + "/" + address.Uf;
        var region = string.Join(" — ", new[] { address.Bairro, city }
            .Where(x => !string.IsNullOrWhiteSpace(x)));
        var cep = string.IsNullOrWhiteSpace(address.Cep) ? "" : "CEP " +
            (address.Cep.Length == 8 ? address.Cep.Insert(5, "-") : address.Cep);
        return string.Join(Environment.NewLine, new[] { street, region, cep }
            .Where(x => !string.IsNullOrWhiteSpace(x)));
    }
}
