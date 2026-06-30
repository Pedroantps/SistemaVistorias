using Microsoft.EntityFrameworkCore;
using SistemaVistorias.Data;
using SistemaVistorias.Models;
using SistemaVistorias.Models.DTOs;
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace SistemaVistorias.Services
{
    public class AuthService : IAuthService
    {
        private readonly AppDbContext _context;

        public AuthService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<(bool Sucesso, string Mensagem)> RegistrarAsync(RegistroRequest dados)
        {
            if (string.IsNullOrWhiteSpace(dados.NomeUsuario) || string.IsNullOrWhiteSpace(dados.Senha))
                return (false, "Informe nome de usuario e senha.");

            var usuarioExistente = await _context.Usuarios
                .FirstOrDefaultAsync(u => u.NomeUsuario == dados.NomeUsuario);

            if (usuarioExistente != null)
                return (false, "Ja existe um usuario com este nome de usuario.");

            var novoUsuario = new Usuario
            {
                NomeUsuario = dados.NomeUsuario.Trim(),
                SenhaHash = GerarHash(dados.Senha),
                NomeCompleto = string.IsNullOrWhiteSpace(dados.NomeCompleto) ? dados.NomeUsuario : dados.NomeCompleto.Trim(),
                DataCriacao = DateTime.Now,
                Ativo = true
            };

            _context.Usuarios.Add(novoUsuario);
            await _context.SaveChangesAsync();

            return (true, "Usuario cadastrado com sucesso.");
        }

        public async Task<(bool Sucesso, string Mensagem, string Token, Usuario? Usuario)> LoginAsync(LoginRequest dados)
        {
            if (string.IsNullOrWhiteSpace(dados.NomeUsuario) || string.IsNullOrWhiteSpace(dados.Senha))
                return (false, "Informe nome de usuario e senha.", string.Empty, null);

            var usuario = await _context.Usuarios
                .FirstOrDefaultAsync(u => u.NomeUsuario == dados.NomeUsuario && u.Ativo);

            if (usuario == null || usuario.SenhaHash != GerarHash(dados.Senha))
                return (false, "Usuario ou senha invalidos.", string.Empty, null);

            var sessoesAntigas = _context.Sessoes
                .Where(s => s.UsuarioId == usuario.Id || s.DataExpiracao < DateTime.Now);
            _context.Sessoes.RemoveRange(sessoesAntigas);

            var token = GerarToken();
            var sessao = new Sessao
            {
                Token = token,
                UsuarioId = usuario.Id,
                DataCriacao = DateTime.Now,
                DataExpiracao = DateTime.Now.AddHours(8)
            };
            _context.Sessoes.Add(sessao);
            await _context.SaveChangesAsync();

            return (true, "Login realizado com sucesso.", token, usuario);
        }

        public async Task<(bool Sucesso, string Mensagem)> LogoutAsync(string authorizationHeader)
        {
            var token = ExtrairToken(authorizationHeader);
            if (string.IsNullOrEmpty(token))
                return (false, "Token nao informado.");

            var sessao = await _context.Sessoes.FirstOrDefaultAsync(s => s.Token == token);
            if (sessao == null)
                return (true, "Sessao ja encerrada.");

            _context.Sessoes.Remove(sessao);
            await _context.SaveChangesAsync();

            return (true, "Logout realizado com sucesso.");
        }

        public async Task<(bool Valido, string Mensagem, Usuario? Usuario)> ValidarAsync(string authorizationHeader)
        {
            var token = ExtrairToken(authorizationHeader);
            if (string.IsNullOrEmpty(token))
                return (false, "Token nao informado.", null);

            var sessao = await _context.Sessoes
                .FirstOrDefaultAsync(s => s.Token == token && s.DataExpiracao > DateTime.Now);

            if (sessao == null)
                return (false, "Sessao invalida ou expirada.", null);

            var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Id == sessao.UsuarioId);
            if (usuario == null || !usuario.Ativo)
                return (false, "Usuario invalido.", null);

            return (true, "Sessao valida.", usuario);
        }

        public async Task<Usuario?> ObterUsuarioAutenticadoAsync(string authorizationHeader)
        {
            var token = ExtrairToken(authorizationHeader);
            if (string.IsNullOrEmpty(token)) return null;

            var sessao = await _context.Sessoes.FirstOrDefaultAsync(s => s.Token == token && s.DataExpiracao > DateTime.Now);
            if (sessao == null) return null;
            return await _context.Usuarios.FirstOrDefaultAsync(u => u.Id == sessao.UsuarioId && u.Ativo);
        }

        private static string ExtrairToken(string? authorization)
        {
            if (string.IsNullOrWhiteSpace(authorization)) return string.Empty;
            const string prefixo = "Bearer ";
            return authorization.StartsWith(prefixo, StringComparison.OrdinalIgnoreCase)
                ? authorization.Substring(prefixo.Length).Trim()
                : authorization.Trim();
        }

        private static string GerarHash(string senha)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(senha));
            var sb = new StringBuilder();
            foreach (var b in bytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        private static string GerarToken()
        {
            return Convert.ToBase64String(RandomNumberGenerator.GetBytes(48))
                .Replace("+", "-").Replace("/", "_").Replace("=", "");
        }
    }
}
