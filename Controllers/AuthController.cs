using Microsoft.AspNetCore.Mvc;
using SistemaVistorias.Models.DTOs;
using SistemaVistorias.Services;
using System.Threading.Tasks;

namespace SistemaVistorias.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

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

        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromHeader] string Authorization)
        {
            var resultado = await _authService.LogoutAsync(Authorization);
            
            if (!resultado.Sucesso)
                return BadRequest(new { mensagem = resultado.Mensagem });

            return Ok(new { mensagem = resultado.Mensagem });
        }

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
