using Microsoft.AspNetCore.Mvc;
using SistemaVistorias.Services;
using System.Threading.Tasks;

namespace SistemaVistorias.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RelatoriosController : ControllerBase
    {
        private readonly IRelatorioService _relatorioService;

        public RelatoriosController(IRelatorioService relatorioService)
        {
            _relatorioService = relatorioService;
        }

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

        [HttpGet("teste")]
        public async Task<IActionResult> TestarDados()
        {
            try
            {
                var resultado = await _relatorioService.TestarDadosAsync();
                return Ok(resultado);
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, new { mensagem = $"Erro ao testar: {ex.Message}" });
            }
        }
    }
}