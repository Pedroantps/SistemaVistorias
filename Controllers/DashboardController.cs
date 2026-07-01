using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaVistorias.Data;
using SistemaVistorias.Services;

namespace SistemaVistorias.Controllers
{
    /// <summary>
    /// Controlador responsável por prover os dados consolidados para a visualização do Dashboard.
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    public class DashboardController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IAuthService _authService;

        public DashboardController(AppDbContext context, IAuthService authService)
        {
            _context = context;
            _authService = authService;
        }

        /// <summary>
        /// Obtém o resumo dos ativos cadastrados e as estatísticas de vistorias realizadas.
        /// </summary>
        /// <remarks>
        /// Esta rota é o coração do dashboard. Ela lê de forma não-rastreada (AsNoTracking) os ativos inservíveis
        /// para maximizar a performance. O retorno é um objeto anônimo formatado para facilitar a renderização
        /// dos gráficos de rosca e barras diretamente no front-end, sem precisar de múltiplas requisições adicionais.
        /// </remarks>
        /// <returns>Retorna um JSON contendo o usuário logado, o resumo total e os agrupamentos por contrato, estado e vistoriador.</returns>
        [HttpGet]
        public async Task<IActionResult> ObterDashboard()
        {
            var authHeader = Request.Headers["Authorization"].ToString();
            var usuario = await _authService.ObterUsuarioAutenticadoAsync(authHeader);
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
