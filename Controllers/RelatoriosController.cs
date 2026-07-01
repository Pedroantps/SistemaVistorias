using Microsoft.AspNetCore.Mvc;
using SistemaVistorias.Services;

namespace SistemaVistorias.Controllers
{
    /// <summary>
    /// Controlador responsável pela geração e download de relatórios.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class RelatoriosController : ControllerBase
    {
        private readonly IRelatorioService _relatorioService;

        public RelatoriosController(IRelatorioService relatorioService)
        {
            _relatorioService = relatorioService;
        }

        /// <summary>
        /// Gera e retorna um relatório (em formato Word) dos bens que foram
        /// alterados para a condição de "Inservível".
        /// </summary>
        /// <returns>O arquivo .docx gerado ou uma mensagem de erro.</returns>
        [HttpGet("desfazimento")]
        public async Task<IActionResult> GerarRelatorioDesfazimento()
        {
            var resultado = await _relatorioService.GerarRelatorioDesfazimentoAsync();
            
            if (!resultado.Sucesso)
            {
                if (resultado.Mensagem.Contains("Nenhum bem"))
                    return BadRequest(new { mensagem = resultado.Mensagem });
                return StatusCode(500, new { mensagem = resultado.Mensagem });
            }

            return File(
                resultado.Arquivo!,
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                resultado.NomeArquivo);
        }
    }
}