using Microsoft.AspNetCore.Mvc;
using SistemaVistorias.Models.DTOs;
using SistemaVistorias.Services;

namespace SistemaVistorias.Controllers
{
    /// <summary>
    /// Controlador responsável por gerenciar o ciclo de vida da sessão do usuário (Registro, Login, Logout e Validação).
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        /// <summary>
        /// Registra um novo usuário no sistema.
        /// </summary>
        /// <remarks>
        /// A senha será convertida em hash utilizando SHA256 na camada de serviço, para que
        /// a credencial original nunca seja salva em texto plano no banco de dados.
        /// </remarks>
        /// <param name="dados">DTO contendo as informações de registro.</param>
        /// <returns>Resultado da operação.</returns>
        [HttpPost("registro")]
        public async Task<IActionResult> Registrar([FromBody] RegistroRequest dados)
        {
            var resultado = await _authService.RegistrarAsync(dados);
            
            if (!resultado.Sucesso)
            {
                if (resultado.Mensagem.Contains("Ja existe"))
                    return Conflict(new { mensagem = resultado.Mensagem });
                return BadRequest(new { mensagem = resultado.Mensagem });
            }

            return Ok(new { mensagem = resultado.Mensagem });
        }

        /// <summary>
        /// Autentica um usuário e inicia sua sessão, invalidando sessões anteriores.
        /// </summary>
        /// <remarks>
        /// Ao logar, o sistema varre e remove tokens expirados ou sessões antigas deste mesmo usuário,
        /// garantindo que não tenhamos "lixo" acumulado na tabela de sessões.
        /// </remarks>
        /// <param name="dados">DTO com credenciais (usuário e senha).</param>
        /// <returns>Token JWT customizado e dados básicos do usuário logado.</returns>
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest dados)
        {
            var resultado = await _authService.LoginAsync(dados);
            
            if (!resultado.Sucesso)
                return Unauthorized(new { mensagem = resultado.Mensagem });

            return Ok(new
            {
                mensagem = resultado.Mensagem,
                token = resultado.Token,
                usuario = new
                {
                    id = resultado.Usuario!.Id,
                    nomeUsuario = resultado.Usuario.NomeUsuario,
                    nomeCompleto = resultado.Usuario.NomeCompleto
                }
            });
        }

        /// <summary>
        /// Realiza o logout do usuário, encerrando sua sessão ativa.
        /// </summary>
        /// <param name="Authorization">O token de autorização JWT recebido via Header.</param>
        /// <returns>Resultado da operação.</returns>
        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromHeader] string Authorization)
        {
            var resultado = await _authService.LogoutAsync(Authorization);
            
            if (!resultado.Sucesso)
                return BadRequest(new { mensagem = resultado.Mensagem });

            return Ok(new { mensagem = resultado.Mensagem });
        }

        /// <summary>
        /// Valida o token recebido no header para verificar se a sessão ainda está ativa.
        /// </summary>
        /// <param name="Authorization">O token JWT recebido no formato "Bearer {token}".</param>
        /// <returns>Dados do usuário associado à sessão se válido, senão Unauthorized (401).</returns>
        [HttpGet("validar")]
        public async Task<IActionResult> Validar([FromHeader] string Authorization)
        {
            var resultado = await _authService.ValidarAsync(Authorization);
            
            if (!resultado.Valido)
                return Unauthorized(new { mensagem = resultado.Mensagem });

            return Ok(new
            {
                valido = true,
                usuario = new
                {
                    id = resultado.Usuario!.Id,
                    nomeUsuario = resultado.Usuario.NomeUsuario,
                    nomeCompleto = resultado.Usuario.NomeCompleto
                }
            });
        }
    }
}
