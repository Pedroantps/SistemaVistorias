using System.Threading.Tasks;

namespace SistemaVistorias.Services
{
    public interface IRelatorioService
    {
        Task<(bool Sucesso, string Mensagem, byte[]? Arquivo, string NomeArquivo)> GerarRelatorioDesfazimentoAsync();
        Task<object> TestarDadosAsync();
    }
}
