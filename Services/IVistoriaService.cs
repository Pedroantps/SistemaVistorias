using SistemaVistorias.Models;

namespace SistemaVistorias.Services
{
    public interface IVistoriaService
    {
        Task<Ativo?> BuscarAtivoAsync(string patrimonio, string contrato);
        Task<(bool Sucesso, string Mensagem)> RegistrarVistoriaAsync(
            Usuario usuario,
            string patrimonioAgevap,
            string contratoGestao,
            string novoEstado,
            string numeroLaudo,
            IFormFile? fotoEsquerda,
            IFormFile? fotoDireita,
            IFormFile? fotoFrontal,
            IFormFile? fotoEtiqueta,
            string? descricao = null,
            string? condicaoFuncional = null,
            string? instalacaoEndereco = null,
            string? patrimonioInea = null);
    }
}
