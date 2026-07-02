using Microsoft.AspNetCore.Mvc;
using SistemaVistorias.Services;

namespace SistemaVistorias.Controllers
{
    /// <summary>
    /// Controlador responsável pelo fluxo de preenchimento e registro das vistorias (formulário e fotos).
    /// </summary>
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

        /// <summary>
        /// Busca um ativo específico antes de iniciar o preenchimento da vistoria.
        /// </summary>
        /// <remarks>
        /// A busca permite localizar um ativo cadastrado por meio de seu Patrimônio e Contrato de Gestão.
        /// O retorno também sinaliza previamente ao front-end se a condição do bem foi cadastrada como "inservível",
        /// preparando a interface para exibir alertas ao vistoriador.
        /// </remarks>
        /// <param name="patrimonio">Patrimônio (AGEVAP ou Órgão).</param>
        /// <param name="contrato">Contrato de Gestão.</param>
        /// <returns>Detalhes do ativo e flag indicando condição inservível.</returns>
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

            bool isInservivel = !string.IsNullOrEmpty(ativo.CondicaoFuncional)
                && ativo.CondicaoFuncional.ToLower().Contains("inserv");

            return Ok(new { ativo, isInservivel });
        }

        /// <summary>
        /// Registra os dados finais da vistoria e faz o upload das fotos anexadas.
        /// </summary>
        /// <remarks>
        /// A rota é configurada para [FromForm] (multipart/form-data) pois mistura dados textuais 
        /// com até quatro arquivos binários pesados (fotos). Os arquivos são persistidos no disco (wwwroot/fotos_vistorias)
        /// para poupar o banco de dados.
        /// </remarks>
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

        [HttpGet("instalacoes")]
        public async Task<IActionResult> ObterInstalacoes()
        {
            var instalacoes = await _vistoriaService.ObterInstalacoesAsync();
            return Ok(instalacoes);
        }
    }
}
