using System.Threading;
using System.Threading.Tasks;

namespace OpenAC.Net.NFSe.Nacional.Tomador;

/// <summary>
/// Abstração de fonte de dados para consulta de pessoas físicas e jurídicas a partir do documento.
/// </summary>
/// <remarks>
/// Recurso opcional e desacoplado do fluxo de emissão. Permite preencher o tomador da nota
/// consultando uma base externa de CPF/CNPJ sem acoplar o restante da biblioteca a nenhum provedor.
/// </remarks>
public interface IPessoaLookup
{
    /// <summary>
    /// Consulta os dados de uma pessoa física a partir do CPF.
    /// </summary>
    /// <param name="cpf">CPF a consultar, com ou sem formatação.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Dados normalizados da pessoa física.</returns>
    Task<DadosPessoaConsultada> ConsultarCpfAsync(string cpf, CancellationToken cancellationToken = default);

    /// <summary>
    /// Consulta os dados de uma pessoa jurídica a partir do CNPJ.
    /// </summary>
    /// <param name="cnpj">CNPJ a consultar, com ou sem formatação.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Dados normalizados da pessoa jurídica.</returns>
    Task<DadosPessoaConsultada> ConsultarCnpjAsync(string cnpj, CancellationToken cancellationToken = default);
}
