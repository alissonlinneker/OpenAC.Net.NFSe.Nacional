using System.Net;
using System.Text;
using OpenAC.Net.Core;
using OpenAC.Net.NFSe.Nacional.Common.Model;
using OpenAC.Net.NFSe.Nacional.Tomador;

namespace OpenAC.Net.NFSe.Nacional.Test;

/// <summary>
/// Testes offline da resolução automática de tomador a partir do documento.
/// Não exigem <c>.env</c>, certificado nem acesso à rede: a fonte de dados é simulada.
/// </summary>
public class TestTomadorResolver
{
    #region TomadorResolver

    [Test]
    public async Task PorCpf_PreencheTomadorComEndereco()
    {
        var lookup = new LookupFalso
        {
            RespostaCpf = new DadosPessoaConsultada
            {
                Documento = "11111111111",
                Nome = "Fulano de Tal",
                Logradouro = "Rua das Flores",
                Numero = "100",
                Complemento = "Apto 12",
                Bairro = "Centro",
                Cep = "01001000",
                CodigoMunicipioIbge = "3550308"
            }
        };
        var resolver = new TomadorResolver(lookup);

        var tomador = await resolver.PorCpfAsync("111.111.111-11");

        await Assert.That(lookup.UltimoCpf).IsEqualTo("11111111111");
        await Assert.That(tomador.CPF).IsEqualTo("11111111111");
        await Assert.That(tomador.CNPJ).IsNull();
        await Assert.That(tomador.Nome).IsEqualTo("Fulano de Tal");
        await Assert.That(tomador.Endereco).IsNotNull();
        await Assert.That(tomador.Endereco!.Logradouro).IsEqualTo("Rua das Flores");
        await Assert.That(tomador.Endereco.Numero).IsEqualTo("100");
        await Assert.That(tomador.Endereco.Bairro).IsEqualTo("Centro");
        await Assert.That(((MunicipioNacional)tomador.Endereco.Municipio!).CodMunicipio).IsEqualTo("3550308");
        await Assert.That(((MunicipioNacional)tomador.Endereco.Municipio!).CEP).IsEqualTo("01001000");
    }

    [Test]
    public async Task PorCnpj_PreencheTomadorComRazaoEEndereco()
    {
        var lookup = new LookupFalso
        {
            RespostaCnpj = new DadosPessoaConsultada
            {
                Documento = "12345678000195",
                Nome = "Empresa Exemplo Ltda",
                Fantasia = "Exemplo",
                Logradouro = "Avenida Brasil",
                Numero = "1500",
                Bairro = "Jardim",
                Cep = "20040002",
                CodigoMunicipioIbge = "3304557",
                SimplesNacional = true
            }
        };
        var resolver = new TomadorResolver(lookup);

        var tomador = await resolver.PorCnpjAsync("12.345.678/0001-95");

        await Assert.That(tomador.CNPJ).IsEqualTo("12345678000195");
        await Assert.That(tomador.CPF).IsNull();
        await Assert.That(tomador.Nome).IsEqualTo("Empresa Exemplo Ltda");
        await Assert.That(tomador.Endereco!.Logradouro).IsEqualTo("Avenida Brasil");
        await Assert.That(((MunicipioNacional)tomador.Endereco.Municipio!).CodMunicipio).IsEqualTo("3304557");
    }

    [Test]
    public async Task PorCnpj_NormalizaCepParaSomenteDigitos()
    {
        var lookup = new LookupFalso
        {
            RespostaCnpj = new DadosPessoaConsultada
            {
                Nome = "Empresa Exemplo Ltda",
                Logradouro = "Avenida Brasil",
                Numero = "1500",
                Bairro = "Centro",
                Cep = "20040-002",
                CodigoMunicipioIbge = "3304557"
            }
        };
        var resolver = new TomadorResolver(lookup);

        var tomador = await resolver.PorCnpjAsync("12345678000195");

        await Assert.That(((MunicipioNacional)tomador.Endereco!.Municipio!).CEP).IsEqualTo("20040002");
    }

    [Test]
    public async Task PorCpf_SemNumero_UsaSemNumero()
    {
        var lookup = new LookupFalso
        {
            RespostaCpf = new DadosPessoaConsultada
            {
                Nome = "Fulano de Tal",
                Logradouro = "Rua das Flores",
                Numero = null,
                Bairro = "Centro",
                Cep = "01001000",
                CodigoMunicipioIbge = "3550308"
            }
        };
        var resolver = new TomadorResolver(lookup);

        var tomador = await resolver.PorCpfAsync("11111111111");

        await Assert.That(tomador.Endereco!.Numero).IsEqualTo("S/N");
    }

    [Test]
    public async Task PorDocumento_ComOnzeDigitos_ConsultaCpf()
    {
        var lookup = new LookupFalso
        {
            RespostaCpf = new DadosPessoaConsultada { Nome = "Fulano de Tal" }
        };
        var resolver = new TomadorResolver(lookup);

        var tomador = await resolver.PorDocumentoAsync("111.111.111-11");

        await Assert.That(lookup.UltimoCpf).IsEqualTo("11111111111");
        await Assert.That(lookup.UltimoCnpj).IsNull();
        await Assert.That(tomador.Nome).IsEqualTo("Fulano de Tal");
    }

    [Test]
    public async Task PorDocumento_ComQuatorzePosicoes_ConsultaCnpj()
    {
        var lookup = new LookupFalso
        {
            RespostaCnpj = new DadosPessoaConsultada { Nome = "Empresa Exemplo Ltda" }
        };
        var resolver = new TomadorResolver(lookup);

        var tomador = await resolver.PorDocumentoAsync("12.345.678/0001-95");

        await Assert.That(lookup.UltimoCnpj).IsEqualTo("12345678000195");
        await Assert.That(lookup.UltimoCpf).IsNull();
        await Assert.That(tomador.Nome).IsEqualTo("Empresa Exemplo Ltda");
    }

    [Test]
    public async Task PorDocumento_TamanhoInvalido_LancaExcecao()
    {
        var resolver = new TomadorResolver(new LookupFalso());

        var capturada = await CapturarAsync(() => resolver.PorDocumentoAsync("123456"));

        await Assert.That(capturada is OpenException).IsTrue();
    }

    [Test]
    public async Task PorCpf_ErroDaFonte_Propaga()
    {
        var lookup = new LookupFalso { Erro = new OpenException("indisponível") };
        var resolver = new TomadorResolver(lookup);

        var capturada = await CapturarAsync(() => resolver.PorCpfAsync("11111111111"));

        await Assert.That(capturada is OpenException).IsTrue();
    }

    #endregion TomadorResolver

    #region CpfCnpjComBrLookup

    [Test]
    public async Task Lookup_ConsultarCpf_MapeiaCampos()
    {
        const string json =
            "{\"status\":1,\"cpf\":\"11111111111\",\"nome\":\"Fulano de Tal\"," +
            "\"endereco\":\"Rua das Flores\",\"numero\":\"100\",\"complemento\":\"Apto 12\"," +
            "\"bairro\":\"Centro\",\"cep\":\"01001-000\",\"cidade\":\"Sao Paulo\",\"uf\":\"SP\",\"ibge\":\"3550308\"}";
        var handler = new RespostaHttpFalsa(json);
        using var http = new HttpClient(handler);
        using var lookup = new CpfCnpjComBrLookup("token-teste", http);

        var dados = await lookup.ConsultarCpfAsync("111.111.111-11");

        await Assert.That(handler.UltimaUrl).IsEqualTo("https://api.cpfcnpj.com.br/token-teste/3/11111111111");
        await Assert.That(dados.Nome).IsEqualTo("Fulano de Tal");
        await Assert.That(dados.Logradouro).IsEqualTo("Rua das Flores");
        await Assert.That(dados.Cep).IsEqualTo("01001000");
        await Assert.That(dados.CodigoMunicipioIbge).IsEqualTo("3550308");
    }

    [Test]
    public async Task Lookup_ConsultarCnpj_MapeiaEnderecoIbgeESimples()
    {
        const string json =
            "{\"status\":1,\"cnpj\":\"12345678000195\",\"tipo\":\"MATRIZ\",\"razao\":\"Empresa Exemplo Ltda\",\"fantasia\":\"Exemplo\"," +
            "\"matrizEndereco\":{\"cep\":\"20040-002\",\"logradouro\":\"Avenida Brasil\",\"numero\":\"1500\",\"complemento\":\"Sala 5\",\"bairro\":\"Centro\",\"cidade\":\"Rio de Janeiro\",\"uf\":\"RJ\"}," +
            "\"ibge\":{\"cidade\":{\"ibge_id\":\"3304557\"}}," +
            "\"simplesNacional\":{\"optante\":\"Sim\"},\"situacao\":{\"nome\":\"ATIVA\"},\"porte\":{\"descricao\":\"ME\"}}";
        var handler = new RespostaHttpFalsa(json);
        using var http = new HttpClient(handler);
        using var lookup = new CpfCnpjComBrLookup("token-teste", http, pacoteCnpj: 6);

        var dados = await lookup.ConsultarCnpjAsync("12.345.678/0001-95");

        await Assert.That(handler.UltimaUrl).IsEqualTo("https://api.cpfcnpj.com.br/token-teste/6/12345678000195");
        await Assert.That(dados.Nome).IsEqualTo("Empresa Exemplo Ltda");
        await Assert.That(dados.Fantasia).IsEqualTo("Exemplo");
        await Assert.That(dados.Logradouro).IsEqualTo("Avenida Brasil");
        await Assert.That(dados.Cep).IsEqualTo("20040002");
        await Assert.That(dados.CodigoMunicipioIbge).IsEqualTo("3304557");
        await Assert.That(dados.SimplesNacional!.Value).IsTrue();
        await Assert.That(dados.Situacao).IsEqualTo("ATIVA");
        await Assert.That(dados.Porte).IsEqualTo("ME");
    }

    [Test]
    public async Task Lookup_IbgeComoNumero_InterpretaComoTexto()
    {
        const string json =
            "{\"status\":1,\"cpf\":\"11111111111\",\"nome\":\"Fulano de Tal\"," +
            "\"endereco\":\"Rua das Flores\",\"numero\":\"100\",\"bairro\":\"Centro\",\"cep\":1001000,\"ibge\":3550308}";
        var handler = new RespostaHttpFalsa(json);
        using var http = new HttpClient(handler);
        using var lookup = new CpfCnpjComBrLookup("token-teste", http);

        var dados = await lookup.ConsultarCpfAsync("11111111111");

        await Assert.That(dados.CodigoMunicipioIbge).IsEqualTo("3550308");
        await Assert.That(dados.Cep).IsEqualTo("1001000");
    }

    [Test]
    public async Task Lookup_StatusZero_LancaOpenException()
    {
        const string json = "{\"status\":0,\"erro\":\"CPF nao encontrado\",\"erroCodigo\":\"12\"}";
        var handler = new RespostaHttpFalsa(json);
        using var http = new HttpClient(handler);
        using var lookup = new CpfCnpjComBrLookup("token-teste", http);

        var capturada = await CapturarAsync(() => lookup.ConsultarCpfAsync("11111111111"));

        await Assert.That(capturada is OpenException).IsTrue();
    }

    [Test]
    public async Task Lookup_HttpNaoSucesso_LancaOpenException()
    {
        var handler = new RespostaHttpFalsa("{}", HttpStatusCode.Unauthorized);
        using var http = new HttpClient(handler);
        using var lookup = new CpfCnpjComBrLookup("token-teste", http);

        var capturada = await CapturarAsync(() => lookup.ConsultarCnpjAsync("12345678000195"));

        await Assert.That(capturada is OpenException).IsTrue();
    }

    #endregion CpfCnpjComBrLookup

    #region Helpers

    private static async Task<Exception?> CapturarAsync(Func<Task> acao)
    {
        try
        {
            await acao();
            return null;
        }
        catch (Exception ex)
        {
            return ex;
        }
    }

    private sealed class LookupFalso : IPessoaLookup
    {
        public DadosPessoaConsultada? RespostaCpf { get; set; }
        public DadosPessoaConsultada? RespostaCnpj { get; set; }
        public string? UltimoCpf { get; private set; }
        public string? UltimoCnpj { get; private set; }
        public Exception? Erro { get; set; }

        public Task<DadosPessoaConsultada> ConsultarCpfAsync(string cpf, CancellationToken cancellationToken = default)
        {
            UltimoCpf = cpf;
            if (Erro != null) throw Erro;
            return Task.FromResult(RespostaCpf ?? new DadosPessoaConsultada());
        }

        public Task<DadosPessoaConsultada> ConsultarCnpjAsync(string cnpj, CancellationToken cancellationToken = default)
        {
            UltimoCnpj = cnpj;
            if (Erro != null) throw Erro;
            return Task.FromResult(RespostaCnpj ?? new DadosPessoaConsultada());
        }
    }

    private sealed class RespostaHttpFalsa : HttpMessageHandler
    {
        private readonly string json;
        private readonly HttpStatusCode status;

        public RespostaHttpFalsa(string json, HttpStatusCode status = HttpStatusCode.OK)
        {
            this.json = json;
            this.status = status;
        }

        public string? UltimaUrl { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            UltimaUrl = request.RequestUri?.ToString();
            var resposta = new HttpResponseMessage(status)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(resposta);
        }
    }

    #endregion Helpers
}
