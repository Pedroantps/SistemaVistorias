using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaVistorias.Data;
using SistemaVistorias.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SistemaVistorias.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DashboardController : ControllerBase
    {
        private readonly AppDbContext _context;

        public DashboardController(AppDbContext context)
        {
            _context = context;
        }

        private async Task<Usuario?> ObterUsuarioAutenticado()
        {
            var token = Request.Headers["Authorization"].ToString();
            if (string.IsNullOrWhiteSpace(token)) return null;
            const string prefixo = "Bearer ";
            if (token.StartsWith(prefixo, StringComparison.OrdinalIgnoreCase))
                token = token.Substring(prefixo.Length).Trim();

            var sessao = await _context.Sessoes.FirstOrDefaultAsync(s => s.Token == token && s.DataExpiracao > DateTime.Now);
            if (sessao == null) return null;
            return await _context.Usuarios.FirstOrDefaultAsync(u => u.Id == sessao.UsuarioId && u.Ativo);
        }

        [HttpGet]
        public async Task<IActionResult> ObterDashboard()
        {
            var usuario = await ObterUsuarioAutenticado();
            if (usuario == null)
                return Unauthorized(new { mensagem = "Sessao invalida ou expirada." });

            var ativos = await _context.Ativos
                .AsNoTracking()
                .Where(a => a.CondicaoFuncional.ToLower().Contains("inserv"))
                .OrderBy(a => a.ContratoGestao)
                .ThenBy(a => a.PatrimonioAgevap)
                .Select(a => new
                {
                    a.PatrimonioAgevap,
                    a.PatrimonioOrgaoGestor,
                    a.ContratoGestao,
                    a.Descricao,
                    a.ClassificacaoQualidade,
                    a.CondicaoFuncional,
                    a.InstalacaoEndereco,
                    a.NovoEstadoConservacao,
                    a.NumeroLaudo,
                    a.DataVistoria,
                    a.UsuarioVistoriador,
                    Vistoriado = a.DataVistoria != null,
                    IsAvulso = a.IsAvulso,
                    a.CondicaoOriginal
                })
                .ToListAsync();

            var total = ativos.Count;
            var vistoriados = ativos.Count(a => a.Vistoriado);
            var pendentes = total - vistoriados;

            var resposta = new
            {
                Usuario = new
                {
                    id = usuario.Id,
                    nomeUsuario = usuario.NomeUsuario,
                    nomeCompleto = usuario.NomeCompleto
                },
                Resumo = new
                {
                    Total = total,
                    Vistoriados = vistoriados,
                    Pendentes = pendentes,
                    AlteradosInservivel = ativos.Count(a => !string.IsNullOrEmpty(a.CondicaoOriginal)),
                    PercentualVistoriado = total == 0 ? 0 : Math.Round(vistoriados * 100m / total, 1)
                },
                PorContrato = ativos
                    .GroupBy(a => string.IsNullOrWhiteSpace(a.ContratoGestao) ? "Sem contrato" : a.ContratoGestao)
                    .Select(g => new
                    {
                        Contrato = g.Key,
                        Total = g.Count(),
                        Vistoriados = g.Count(a => a.Vistoriado),
                        Pendentes = g.Count(a => !a.Vistoriado)
                    })
                    .OrderBy(g => g.Contrato),
                PorEstado = ativos
                    .GroupBy(a => string.IsNullOrWhiteSpace(a.NovoEstadoConservacao) ? "Sem vistoria" : a.NovoEstadoConservacao)
                    .Select(g => new
                    {
                        Estado = g.Key,
                        Total = g.Count()
                    })
                    .OrderByDescending(g => g.Total),
                PorVistoriador = ativos
                    .Where(a => !string.IsNullOrWhiteSpace(a.UsuarioVistoriador))
                    .GroupBy(a => a.UsuarioVistoriador!)
                    .Select(g => new
                    {
                        Vistoriador = g.Key,
                        Total = g.Count()
                    })
                    .OrderByDescending(g => g.Total),
                Ativos = ativos
            };
            return Ok(resposta);
        }
    }
}
