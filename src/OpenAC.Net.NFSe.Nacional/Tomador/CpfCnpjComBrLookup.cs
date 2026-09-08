using System;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using OpenAC.Net.Core;
using OpenAC.Net.NFSe.Nacional.Common;

namespace OpenAC.Net.NFSe.Nacional.Tomador;

/// <summary>
/// Implementação de referência de <see cref="IPessoaLookup"/> que consulta a API pública da
/// CPF.CNPJ (<c>https://api.cpfcnpj.com.br</c>).
/// </summary>
/// <remarks>
/// O token é atrelado ao IP de origem e obtido no painel da CPF.CNPJ em API &gt; Tokens.
/// O token público de testes <c>5ae973d7a997af13f0aaf2bf60e65803</c> devolve dados fictícios.
/// </remarks>
public sealed class CpfCnpjComBrLookup : IPessoaLookup, IDisposable
{
    #region Fields

    /// <summary>Pacote padrão para consulta de CPF que retorna endereço completo.</summary>
    public const int PacoteCpfPadrao = 3;

    /// <summary>Pacote padrão para consulta de CNPJ que retorna endereço da matriz.</summary>
    public const int PacoteCnpjPadrao = 5;

    /// <summary>Endereço base padrão da API.</summary>
    public const string UrlBasePadrao = "https://api.cpfcnpj.com.br";

    private static readonly JsonSerializerOptions OpcoesJson = CriarOpcoes();

    private static JsonSerializerOptions CriarOpcoes()
    {
        var opcoes = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };
        opcoes.Converters.Add(new TextoFlexivelConverter());
        return opcoes;
    }

    private readonly string token;
    private readonly int pacoteCpf;
    private readonly int pacoteCnpj;
    private readonly string urlBase;
    private readonly HttpClient http;
    private readonly bool clienteProprio;

    #endregion Fields

    #region Constructors

    /// <summary>
    /// Inicializa uma nova instância de <see cref="CpfCnpjComBrLookup"/>.
    /// </summary>
    /// <param name="token">Token de acesso da API, obtido no painel em API &gt; Tokens.</param>
    /// <param name="httpClient">Cliente HTTP a reutilizar. Quando nulo, um cliente próprio é criado e liberado no <see cref="Dispose"/>.</param>
    /// <param name="pacoteCpf">Pacote usado nas consultas de CPF. O padrão retorna o endereço completo.</param>
    /// <param name="pacoteCnpj">Pacote usado nas consultas de CNPJ. Use o pacote com Simples Nacional para preencher <see cref="DadosPessoaConsultada.SimplesNacional"/>, <see cref="DadosPessoaConsultada.Situacao"/> e <see cref="DadosPessoaConsultada.Porte"/>.</param>
    /// <param name="urlBase">Endereço base da API. Quando nulo, usa <see cref="UrlBasePadrao"/>.</param>
    /// <exception cref="ArgumentException">Quando o token não é informado.</exception>
    public CpfCnpjComBrLookup(string token, HttpClient? httpClient = null, int pacoteCpf = PacoteCpfPadrao,
        int pacoteCnpj = PacoteCnpjPadrao, string? urlBase = null)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("Token da API CPF.CNPJ é obrigatório.", nameof(token));

        this.token = token.Trim();
        this.pacoteCpf = pacoteCpf;
        this.pacoteCnpj = pacoteCnpj;
        this.urlBase = (urlBase ?? UrlBasePadrao).TrimEnd('/');
        clienteProprio = httpClient is null;
        http = httpClient ?? new HttpClient();
    }

    #endregion Constructors

    #region Methods

    /// <inheritdoc />
    public async Task<DadosPessoaConsultada> ConsultarCpfAsync(string cpf, CancellationToken cancellationToken = default)
    {
        var documento = cpf.SomenteAlfanumerico();
        if (string.IsNullOrEmpty(documento))
            throw new ArgumentException("CPF é obrigatório.", nameof(cpf));

        var resposta = await ConsultarAsync<RespostaCpf>(pacoteCpf, documento!, cancellationToken).ConfigureAwait(false);

        return new DadosPessoaConsultada
        {
            Documento = resposta.Cpf ?? documento,
            Nome = resposta.Nome,
            Logradouro = resposta.Endereco,
            Numero = resposta.Numero,
            Complemento = resposta.Complemento,
            Bairro = resposta.Bairro,
            Cep = resposta.Cep.SomenteNumeros(),
            Cidade = resposta.Cidade,
            Uf = resposta.Uf,
            CodigoMunicipioIbge = resposta.Ibge
        };
    }

    /// <inheritdoc />
    public async Task<DadosPessoaConsultada> ConsultarCnpjAsync(string cnpj, CancellationToken cancellationToken = default)
    {
        var documento = cnpj.SomenteAlfanumerico();
        if (string.IsNullOrEmpty(documento))
            throw new ArgumentException("CNPJ é obrigatório.", nameof(cnpj));

        var resposta = await ConsultarAsync<RespostaCnpj>(pacoteCnpj, documento!, cancellationToken).ConfigureAwait(false);
        var endereco = resposta.MatrizEndereco;

        return new DadosPessoaConsultada
        {
            Documento = resposta.Cnpj ?? documento,
            Nome = resposta.Razao,
            Fantasia = resposta.Fantasia,
            Logradouro = endereco?.Logradouro,
            Numero = endereco?.Numero,
            Complemento = endereco?.Complemento,
            Bairro = endereco?.Bairro,
            Cep = endereco?.Cep.SomenteNumeros(),
            Cidade = endereco?.Cidade,
            Uf = endereco?.Uf,
            CodigoMunicipioIbge = resposta.Ibge?.Cidade?.IbgeId,
            SimplesNacional = InterpretarSimNao(resposta.SimplesNacional?.Optante),
            Situacao = resposta.Situacao?.Nome,
            Porte = resposta.Porte?.Descricao
        };
    }

    private async Task<T> ConsultarAsync<T>(int pacote, string documento, CancellationToken cancellationToken)
        where T : RespostaBase
    {
        var url = $"{urlBase}/{token}/{pacote}/{documento}";

        HttpResponseMessage resposta;
        try
        {
            resposta = await http.GetAsync(url, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new OpenException("Falha na comunicação com a API CPF.CNPJ.", ex);
        }

        using (resposta)
        {
            if (!resposta.IsSuccessStatusCode)
                throw new OpenException($"Consulta à API CPF.CNPJ retornou status HTTP {(int)resposta.StatusCode}.");

            T? conteudo;
            try
            {
                conteudo = await resposta.Content.ReadFromJsonAsync<T>(OpcoesJson, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new OpenException("Não foi possível interpretar a resposta da API CPF.CNPJ.", ex);
            }

            if (conteudo is null)
                throw new OpenException("A API CPF.CNPJ devolveu uma resposta vazia.");

            if (conteudo.Status != 1)
            {
                var detalhe = string.IsNullOrWhiteSpace(conteudo.Erro) ? "erro não informado" : conteudo.Erro!;
                var codigo = string.IsNullOrWhiteSpace(conteudo.ErroCodigo) ? string.Empty : $" (código {conteudo.ErroCodigo})";
                throw new OpenException($"Consulta à API CPF.CNPJ falhou: {detalhe}{codigo}.");
            }

            return conteudo;
        }
    }

    private static bool? InterpretarSimNao(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        return valor!.Trim().ToUpperInvariant() switch
        {
            "SIM" => true,
            "NAO" => false,
            "NÃO" => false,
            _ => null
        };
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (clienteProprio) http.Dispose();
    }

    #endregion Methods

    #region Internal DTOs

    private abstract class RespostaBase
    {
        [JsonPropertyName("status")]
        public int Status { get; set; }

        [JsonPropertyName("erro")]
        public string? Erro { get; set; }

        [JsonPropertyName("erroCodigo")]
        public string? ErroCodigo { get; set; }
    }

    private sealed class RespostaCpf : RespostaBase
    {
        [JsonPropertyName("cpf")]
        public string? Cpf { get; set; }

        [JsonPropertyName("nome")]
        public string? Nome { get; set; }

        [JsonPropertyName("endereco")]
        public string? Endereco { get; set; }

        [JsonPropertyName("numero")]
        public string? Numero { get; set; }

        [JsonPropertyName("complemento")]
        public string? Complemento { get; set; }

        [JsonPropertyName("bairro")]
        public string? Bairro { get; set; }

        [JsonPropertyName("cep")]
        public string? Cep { get; set; }

        [JsonPropertyName("cidade")]
        public string? Cidade { get; set; }

        [JsonPropertyName("uf")]
        public string? Uf { get; set; }

        [JsonPropertyName("ibge")]
        public string? Ibge { get; set; }
    }

    private sealed class RespostaCnpj : RespostaBase
    {
        [JsonPropertyName("cnpj")]
        public string? Cnpj { get; set; }

        [JsonPropertyName("razao")]
        public string? Razao { get; set; }

        [JsonPropertyName("fantasia")]
        public string? Fantasia { get; set; }

        [JsonPropertyName("matrizEndereco")]
        public MatrizEnderecoCnpj? MatrizEndereco { get; set; }

        [JsonPropertyName("ibge")]
        public IbgeCnpj? Ibge { get; set; }

        [JsonPropertyName("simplesNacional")]
        public SimplesNacionalCnpj? SimplesNacional { get; set; }

        [JsonPropertyName("situacao")]
        public SituacaoCnpj? Situacao { get; set; }

        [JsonPropertyName("porte")]
        public PorteCnpj? Porte { get; set; }
    }

    private sealed class MatrizEnderecoCnpj
    {
        [JsonPropertyName("cep")]
        public string? Cep { get; set; }

        [JsonPropertyName("logradouro")]
        public string? Logradouro { get; set; }

        [JsonPropertyName("numero")]
        public string? Numero { get; set; }

        [JsonPropertyName("complemento")]
        public string? Complemento { get; set; }

        [JsonPropertyName("bairro")]
        public string? Bairro { get; set; }

        [JsonPropertyName("cidade")]
        public string? Cidade { get; set; }

        [JsonPropertyName("uf")]
        public string? Uf { get; set; }
    }

    private sealed class IbgeCnpj
    {
        [JsonPropertyName("cidade")]
        public IbgeCidadeCnpj? Cidade { get; set; }
    }

    private sealed class IbgeCidadeCnpj
    {
        [JsonPropertyName("ibge_id")]
        public string? IbgeId { get; set; }
    }

    private sealed class SimplesNacionalCnpj
    {
        [JsonPropertyName("optante")]
        public string? Optante { get; set; }
    }

    private sealed class SituacaoCnpj
    {
        [JsonPropertyName("nome")]
        public string? Nome { get; set; }
    }

    private sealed class PorteCnpj
    {
        [JsonPropertyName("descricao")]
        public string? Descricao { get; set; }
    }

    #endregion Internal DTOs

    #region Converters

    /// <summary>
    /// Lê valores de texto que a API pode devolver como número, texto ou booleano,
    /// evitando falha de desserialização quando campos como IBGE ou CEP vêm sem aspas.
    /// </summary>
    private sealed class TextoFlexivelConverter : JsonConverter<string?>
    {
        public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return reader.TokenType switch
            {
                JsonTokenType.String => reader.GetString(),
                JsonTokenType.Number => reader.TryGetInt64(out var inteiro)
                    ? inteiro.ToString(CultureInfo.InvariantCulture)
                    : reader.GetDouble().ToString(CultureInfo.InvariantCulture),
                JsonTokenType.True => "true",
                JsonTokenType.False => "false",
                JsonTokenType.Null => null,
                _ => null
            };
        }

        public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
        {
            if (value is null) writer.WriteNullValue();
            else writer.WriteStringValue(value);
        }
    }

    #endregion Converters
}
