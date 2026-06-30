using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SistemaVistorias.Services;
using System.Threading.Tasks;

namespace SistemaVistorias.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class VistoriasController : ControllerBase
    {
        private readonly IVistoriaService _vistoriaService;
        private readonly IAuthService _authService;

        public VistoriasController(IVistoriaService vistoriaService, IAuthService authService)
        {
            _vistoriaService = vistoriaService;
            _authService = authService;
        }

        [HttpGet("buscar")]
        public async Task<IActionResult> BuscarAtivo([FromQuery] string patrimonio, [FromQuery] string contrato)
        {
            var authHeader = Request.Headers["Authorization"].ToString();
            var usuario = await _authService.ObterUsuarioAutenticadoAsync(authHeader);
            if (usuario == null)
                return Unauthorized(new { mensagem = "Sessao invalida ou expirada." });

            var ativo = await _vistoriaService.BuscarAtivoAsync(patrimonio, contrato);
            if (ativo == null)
                return NotFound(new { mensagem = "Ativo nao encontrado." });
                
            return Ok(ativo);
        }

        [HttpPost("registrar")]
        public async Task<IActionResult> RegistrarVistoria(
            [FromForm] string patrimonioAgevap,
            [FromForm] string contratoGestao,
            [FromForm] string novoEstado,
            [FromForm] string numeroLaudo,
            [FromForm] IFormFile? fotoEsquerda,
            [FromForm] IFormFile? fotoDireita,
            [FromForm] IFormFile? fotoFrontal,
            [FromForm] IFormFile? fotoEtiqueta,
            [FromForm] string? descricao = null,
            [FromForm] string? condicaoFuncional = null,
            [FromForm] string? instalacaoEndereco = null,
            [FromForm] string? patrimonioInea = null)
        {
            var authHeader = Request.Headers["Authorization"].ToString();
            var usuario = await _authService.ObterUsuarioAutenticadoAsync(authHeader);
            if (usuario == null)
                return Unauthorized(new { mensagem = "Sessao invalida ou expirada." });

            var resultado = await _vistoriaService.RegistrarVistoriaAsync(
                usuario,
                patrimonioAgevap,
                contratoGestao,
                novoEstado,
                numeroLaudo,
                fotoEsquerda,
                fotoDireita,
                fotoFrontal,
                fotoEtiqueta,
                descricao,
                condicaoFuncional,
                instalacaoEndereco,
                patrimonioInea
            );

            if (!resultado.Sucesso)
            {
                if (resultado.Mensagem.Contains("nao encontrado"))
                    return NotFound(new { mensagem = resultado.Mensagem });
                return BadRequest(new { mensagem = resultado.Mensagem });
            }

            return Ok(new { mensagem = resultado.Mensagem });
        }
    }
}
