using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaVistorias.Data;

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

        [HttpGet]
        public async Task<IActionResult> ObterDashboard()
        {
            var ativos = await _context.Ativos
                .AsNoTracking()
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
                    Vistoriado = a.DataVistoria != null
                })
                .ToListAsync();

            var total = ativos.Count;
            var vistoriados = ativos.Count(a => a.Vistoriado);
            var pendentes = total - vistoriados;

            var resposta = new
            {
                Resumo = new
                {
                    Total = total,
                    Vistoriados = vistoriados,
                    Pendentes = pendentes,
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
                Ativos = ativos
            };

            return Ok(resposta);
        }
    }
}
