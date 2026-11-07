# TRAMPO Desktop — Cliente e Profissional

Aplicativo Windows Forms em C# e .NET 10 para clientes e profissionais da plataforma TRAMPO. A Sprint 12 criou a área Profissional; a Sprint 13 acrescentou a área Cliente, preservando os recursos anteriores.

Este repositório é uma extensão do [projeto principal TRAMPO](https://github.com/vinicimxt/Trampo), que contém a aplicação web e a API. O Desktop usa essa API para autenticação e operações; não possui banco de dados próprio nem se conecta diretamente ao SQL Server. O [aplicativo Android](https://github.com/vinicimxt/TRAMPO-APP) é outra interface do ecossistema, voltada principalmente ao cliente.

## Perfis suportados

- **Cliente:** busca serviços, consulta disponibilidade, cria e acompanha agendamentos, cancela ou confirma conclusão conforme o estado, avalia atendimentos elegíveis, edita perfil, lê notificações e envia/consulta chamados de suporte.
- **Profissional:** mantém o painel original de agendamentos recebidos, serviços e locais.
- **Admin:** utiliza somente a aplicação Web. O Desktop informa isso após a identificação do perfil.

## O que é possível fazer

| Área | Funcionalidades |
| --- | --- |
| Acesso | Entrar como **Cliente** ou **Profissional** e abrir o painel correspondente; sair da sessão. Admin utiliza a Web. |
| Cliente | Buscar por texto, categoria, subcategoria, modalidade e profissional; ver serviço e horários; solicitar e acompanhar agendamentos; cancelar, confirmar conclusão e avaliar quando permitido; editar perfil e senha; ver notificações e chamados de suporte. |
| Agendamentos | Consultar pedidos recebidos e seus detalhes; confirmar ou recusar pedidos pendentes; finalizar atendimentos confirmados informando o valor final. |
| Serviços | Listar serviços próprios ativos e inativos; cadastrar, editar e remover da oferta. A remoção pode desativar um serviço que já possui histórico, conforme a resposta da API. |
| Locais | Listar, cadastrar, editar e excluir locais próprios quando não houver vínculos que impeçam a exclusão. Consultar CEP pela API, com preenchimento manual se a consulta falhar. |

Categorias e subcategorias vêm da API. Na edição de um serviço existente, esses campos ficam bloqueados de acordo com a regra atual do backend.

A área Cliente depende dos endpoints da Sprint 13 da API do projeto principal. Antes de testar esse perfil, atualize e execute também o backend correspondente.

## Como funciona

```text
Cliente Desktop ──────┐
                      ├──> API REST v1 / JWT Bearer
Profissional Desktop ─┘             │
                                    ▼
                          Application Services
                                    │
                                    ▼
                              DAO / SQL Server
```

O cliente HTTP está em `Trampo.Desktop/Api/TrampoApiClient.cs`. Após o login, o aplicativo valida o perfil em `/api/v1/auth/me`. O token JWT fica apenas em memória e é limpo no logout ou quando a API retorna `401` em uma operação autenticada. Não há connection string ou armazenamento local da sessão no Desktop.

## Pré-requisitos

- Windows 10/11;
- [.NET SDK 10](https://dotnet.microsoft.com/download/dotnet/10.0);
- aplicação web/API do [repositório principal](https://github.com/vinicimxt/Trampo) em execução, com o banco SQL Server configurado conforme as instruções daquele repositório;
- uma conta cadastrada com perfil **Cliente** ou **Profissional** na plataforma.

## Executar localmente

1. Prepare e inicie o backend seguindo o README do [TRAMPO Web/API](https://github.com/vinicimxt/Trampo). O Desktop espera a API em `http://localhost:5165/` por padrão.
2. Em um terminal PowerShell, na raiz deste repositório, restaure, compile e execute:

   ```powershell
   dotnet restore Trampo.Desktop.slnx
   dotnet build Trampo.Desktop.slnx --configuration Release --no-restore
   dotnet run --project Trampo.Desktop/Trampo.Desktop.csproj --configuration Release --no-build
   ```

3. Entre com o e-mail e a senha da conta **Cliente** ou **Profissional**. O Desktop abre o painel correspondente.

Se a API estiver em outro endereço, defina `TRAMPO_API_BASE_URL` **no mesmo terminal, antes de executar** o aplicativo. A URL deve começar com `http://` ou `https://`:

```powershell
$env:TRAMPO_API_BASE_URL = "https://localhost:7000/"
dotnet run --project Trampo.Desktop/Trampo.Desktop.csproj --configuration Release --no-build
```

Substitua o endereço do exemplo pela URL real da sua API. A variável altera apenas a conexão do Desktop; a configuração do SQL Server permanece no backend.

## Testes e integração contínua

O projeto `Tests/Trampo.Desktop.Tests` contém verificações não visuais de contratos JSON, chamadas HTTP, papéis, tratamento de 401/403 e componentes dos Forms Cliente e Profissional. Após a compilação acima, execute:

```powershell
dotnet run --project Tests/Trampo.Desktop.Tests/Trampo.Desktop.Tests.csproj --configuration Release --no-build
```

Esse comando usa respostas simuladas e não exige a API em execução. O runner também possui modos de smoke test contra uma API real, que dependem de credenciais e dados de teste próprios. O workflow em `.github/workflows/desktop.yml` executa restore, build e os testes não visuais em `windows-latest` a cada push e pull request.

## Estrutura do repositório

```text
Trampo.Desktop/
  Api/                    Cliente HTTP e contratos da API
  Session/                Sessão autenticada em memória
  Assets/                 Logo e ícone
  *Form.cs                Telas de acesso, Cliente e Profissional
Tests/Trampo.Desktop.Tests/  Runner de testes não visuais
.github/workflows/desktop.yml  Pipeline de build e testes
Trampo.Desktop.slnx        Solução .NET
```

## Escopo

O Desktop não oferece área Admin, pagamentos, chat, mapas ou funcionamento offline. A média e a quantidade de avaliações vêm da API; comentários individuais, lista de avaliações já enviadas, perfil público completo e texto de resposta de suporte ainda não têm contrato REST suficiente. O Desktop não inventa esses dados. O preço atual de um serviço não é apresentado como preço histórico contratado. Para arquitetura completa, configuração do banco, API e demais interfaces, consulte o [repositório principal TRAMPO](https://github.com/vinicimxt/Trampo).
