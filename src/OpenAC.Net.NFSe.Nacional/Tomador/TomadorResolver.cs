using System;
using System.Threading;
using System.Threading.Tasks;
using OpenAC.Net.Core;
using OpenAC.Net.NFSe.Nacional.Common;
using OpenAC.Net.NFSe.Nacional.Common.Model;

namespace OpenAC.Net.NFSe.Nacional.Tomador;

/// <summary>
/// Resolve o tomador de uma DPS a partir do documento, consultando uma <see cref="IPessoaLookup"/>
/// e devolvendo um <see cref="InfoPessoaNFSe"/> já preenchido com nome, endereço, código IBGE e CEP.
/// </summary>
/// <remarks>
/// Recurso opcional. Quem não usar a resolução automática continua montando o tomador manualmente,
/// sem qualquer dependência adicional.
/// </remarks>
public sealed class TomadorResolver
{
    #region Fields

    private readonly IPessoaLookup lookup;

    #endregion Fields

    #region Constructors

    /// <summary>
    /// Inicializa uma nova instância de <see cref="TomadorResolver"/>.
    /// </summary>
    /// <param name="lookup">Fonte de dados usada para consultar CPF e CNPJ.</param>
    /// <exception cref="ArgumentNullException">Quando <paramref name="lookup"/> é nulo.</exception>
    public TomadorResolver(IPessoaLookup lookup)
    {
        this.lookup = lookup ?? throw new ArgumentNullException(nameof(lookup));
    }

    #endregion Constructors

    #region Methods

    /// <summary>
    /// Monta o tomador a partir de um CPF.
    /// </summary>
    /// <param name="cpf">CPF com ou sem formatação.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Tomador preenchido.</returns>
    public async Task<InfoPessoaNFSe> PorCpfAsync(string cpf, CancellationToken cancellationToken = default)
    {
        var documento = cpf.SomenteNumeros();
        if (string.IsNullOrEmpty(documento))
            throw new ArgumentException("CPF é obrigatório.", nameof(cpf));

        var dados = await lookup.ConsultarCpfAsync(documento!, cancellationToken).ConfigureAwait(false);
        var tomador = MontarTomador(dados);
        tomador.CPF = documento;
        return tomador;
    }

    /// <summary>
    /// Monta o tomador a partir de um CNPJ.
    /// </summary>
    /// <param name="cnpj">CNPJ com ou sem formatação.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Tomador preenchido.</returns>
    public async Task<InfoPessoaNFSe> PorCnpjAsync(string cnpj, CancellationToken cancellationToken = default)
    {
        var documento = cnpj.SomenteAlfanumerico();
        if (string.IsNullOrEmpty(documento))
            throw new ArgumentException("CNPJ é obrigatório.", nameof(cnpj));

        var dados = await lookup.ConsultarCnpjAsync(documento!, cancellationToken).ConfigureAwait(false);
        var tomador = MontarTomador(dados);
        tomador.CNPJ = documento;
        return tomador;
    }

    /// <summary>
    /// Monta o tomador detectando automaticamente se o documento é CPF (11 dígitos) ou CNPJ (14 posições).
    /// </summary>
    /// <param name="documento">CPF ou CNPJ com ou sem formatação.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Tomador preenchido.</returns>
    /// <exception cref="OpenException">Quando o documento não tem 11 nem 14 posições.</exception>
    public async Task<InfoPessoaNFSe> PorDocumentoAsync(string documento, CancellationToken cancellationToken = default)
    {
        var normalizado = documento.SomenteAlfanumerico() ?? string.Empty;

        return normalizado.Length switch
        {
            11 => await PorCpfAsync(normalizado, cancellationToken).ConfigureAwait(false),
            14 => await PorCnpjAsync(normalizado, cancellationToken).ConfigureAwait(false),
            _ => throw new OpenException("Documento inválido: informe um CPF com 11 dígitos ou um CNPJ com 14 posições.")
        };
    }

    private static InfoPessoaNFSe MontarTomador(DadosPessoaConsultada dados)
    {
        var tomador = new InfoPessoaNFSe
        {
            Nome = dados.Nome
        };

        if (!TemEndereco(dados)) return tomador;

        tomador.Endereco = new EnderecoNFSe
        {
            Logradouro = dados.Logradouro ?? string.Empty,
            Numero = string.IsNullOrWhiteSpace(dados.Numero) ? "S/N" : dados.Numero!,
            Complemento = dados.Complemento,
            Bairro = dados.Bairro ?? string.Empty,
            Municipio = new MunicipioNacional
            {
                CodMunicipio = dados.CodigoMunicipioIbge ?? string.Empty,
                CEP = dados.Cep.SomenteNumeros() ?? string.Empty
            }
        };

        return tomador;
    }

    private static bool TemEndereco(DadosPessoaConsultada dados) =>
        !string.IsNullOrWhiteSpace(dados.Logradouro) ||
        !string.IsNullOrWhiteSpace(dados.Bairro) ||
        !string.IsNullOrWhiteSpace(dados.Cep) ||
        !string.IsNullOrWhiteSpace(dados.CodigoMunicipioIbge);

    #endregion Methods
}
