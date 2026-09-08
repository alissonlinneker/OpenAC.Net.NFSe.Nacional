namespace OpenAC.Net.NFSe.Nacional.Tomador;

/// <summary>
/// Dados normalizados de uma pessoa retornados por uma <see cref="IPessoaLookup"/>.
/// </summary>
/// <remarks>
/// Os campos de endereço valem tanto para pessoa física quanto jurídica. Os campos
/// <see cref="Fantasia"/>, <see cref="SimplesNacional"/>, <see cref="Situacao"/> e
/// <see cref="Porte"/> são preenchidos apenas para pessoa jurídica, quando a fonte de dados os fornecer.
/// </remarks>
public sealed class DadosPessoaConsultada
{
    #region Properties

    /// <summary>Documento retornado pela consulta (CPF ou CNPJ), somente dígitos ou alfanumérico.</summary>
    public string? Documento { get; set; }

    /// <summary>Nome da pessoa física ou razão social da pessoa jurídica.</summary>
    public string? Nome { get; set; }

    /// <summary>Nome fantasia da pessoa jurídica.</summary>
    public string? Fantasia { get; set; }

    /// <summary>Logradouro do endereço.</summary>
    public string? Logradouro { get; set; }

    /// <summary>Número do endereço.</summary>
    public string? Numero { get; set; }

    /// <summary>Complemento do endereço.</summary>
    public string? Complemento { get; set; }

    /// <summary>Bairro do endereço.</summary>
    public string? Bairro { get; set; }

    /// <summary>CEP do endereço, somente dígitos.</summary>
    public string? Cep { get; set; }

    /// <summary>Nome do município.</summary>
    public string? Cidade { get; set; }

    /// <summary>Unidade federativa.</summary>
    public string? Uf { get; set; }

    /// <summary>Código IBGE do município, com sete dígitos.</summary>
    public string? CodigoMunicipioIbge { get; set; }

    /// <summary>Indica se a pessoa jurídica é optante pelo Simples Nacional.</summary>
    public bool? SimplesNacional { get; set; }

    /// <summary>Situação cadastral da pessoa jurídica.</summary>
    public string? Situacao { get; set; }

    /// <summary>Porte da pessoa jurídica.</summary>
    public string? Porte { get; set; }

    #endregion Properties
}
