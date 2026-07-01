using SistemaVistorias.Models;
using SistemaVistorias.Models.DTOs;

namespace SistemaVistorias.Services
{
    public interface IAuthService
    {
        Task<(bool Sucesso, string Mensagem)> RegistrarAsync(RegistroRequest dados);
        Task<(bool Sucesso, string Mensagem, string Token, Usuario? Usuario)> LoginAsync(LoginRequest dados);
        Task<(bool Sucesso, string Mensagem)> LogoutAsync(string authorizationHeader);
        Task<(bool Valido, string Mensagem, Usuario? Usuario)> ValidarAsync(string authorizationHeader);
        Task<Usuario?> ObterUsuarioAutenticadoAsync(string authorizationHeader);
    }
}
