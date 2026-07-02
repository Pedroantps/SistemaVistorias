using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaVistorias.Data;
using SistemaVistorias.Services;
using ClosedXML.Excel;

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

            var ativosBd = await _context.Ativos
                .AsNoTracking()
                .Where(a => 
                    (!string.IsNullOrEmpty(a.CondicaoFuncional) && a.CondicaoFuncional.ToLower().Contains("inserv")) || 
                    (!string.IsNullOrEmpty(a.NovoEstadoConservacao) && a.NovoEstadoConservacao.ToLower().Contains("inserv")))
                .ToListAsync();

            var ativos = ativosBd
                .Select(a => {
                    var patrimonioPad = a.PatrimonioAgevap;
                    if (int.TryParse(a.PatrimonioAgevap, out _))
                        patrimonioPad = a.PatrimonioAgevap.PadLeft(3, '0');
                        
                    return new
                    {
                        PatrimonioAgevap = patrimonioPad,
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
                        a.CondicaoOriginal,
                        a.InstalacaoOriginal,
                        a.PatrimonioOrgaoOriginal
                    };
                })
                .OrderBy(a => a.ContratoGestao)
                .ThenBy(a => a.PatrimonioAgevap)
                .ToList();

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
                    Alterados = ativos.Count(a => !string.IsNullOrEmpty(a.CondicaoOriginal) || !string.IsNullOrEmpty(a.InstalacaoOriginal) || !string.IsNullOrEmpty(a.PatrimonioOrgaoOriginal) || (!string.IsNullOrEmpty(a.NovoEstadoConservacao) && a.NovoEstadoConservacao != a.CondicaoFuncional)),
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
        [HttpGet("ExportarAlterados")]
        public async Task<IActionResult> ExportarAlteradosExcel()
        {
            try
            {
                var authHeader = Request.Headers["Authorization"].ToString();
                var usuario = await _authService.ObterUsuarioAutenticadoAsync(authHeader);
                if (usuario == null)
                    return Unauthorized(new { mensagem = "Sessão inválida ou expirada." });

                var alteradosBd = await _context.Ativos
                    .AsNoTracking()
                    .Where(a => !string.IsNullOrEmpty(a.CondicaoOriginal) || 
                                !string.IsNullOrEmpty(a.InstalacaoOriginal) || 
                                !string.IsNullOrEmpty(a.PatrimonioOrgaoOriginal) ||
                                (!string.IsNullOrEmpty(a.NovoEstadoConservacao) && a.NovoEstadoConservacao != a.CondicaoFuncional))
                    .ToListAsync();

                var alterados = alteradosBd
                    .OrderBy(a => {
                        if (int.TryParse(a.PatrimonioAgevap, out _))
                            return a.PatrimonioAgevap.PadLeft(3, '0');
                        return a.PatrimonioAgevap;
                    })
                    .ToList();

                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("Bens Alterados");

                // Cabecalho
                var cabecalhos = new string[] 
                { 
                    "Patrimônio AGEVAP", "Contrato", "Descrição", 
                    "Patrimônio Órgão (Anterior)", "Patrimônio Órgão (Novo)", 
                    "Instalação (Anterior)", "Instalação (Novo)", 
                    "Condição (Anterior)", "Novo Estado", 
                    "Vistoriador" 
                };

                for (int i = 0; i < cabecalhos.Length; i++)
                {
                    worksheet.Cell(1, i + 1).Value = cabecalhos[i];
                }

                // Dados
                for (int i = 0; i < alterados.Count; i++)
                {
                    var ativo = alterados[i];
                    int row = i + 2;

                    worksheet.Cell(row, 1).Value = ativo.PatrimonioAgevap != null && int.TryParse(ativo.PatrimonioAgevap, out _) 
                        ? ativo.PatrimonioAgevap.PadLeft(3, '0') 
                        : (ativo.PatrimonioAgevap ?? "");
                    worksheet.Cell(row, 2).Value = ativo.ContratoGestao ?? "";
                    worksheet.Cell(row, 3).Value = ativo.Descricao ?? "";
                    
                    // Patrimônio Órgão Gestor
                    bool mudouPatrimonio = !string.IsNullOrWhiteSpace(ativo.PatrimonioOrgaoOriginal);
                    worksheet.Cell(row, 4).Value = mudouPatrimonio ? ativo.PatrimonioOrgaoOriginal : "-";
                    worksheet.Cell(row, 5).Value = ativo.PatrimonioOrgaoGestor ?? "-";

                    // Instalação
                    bool mudouInstalacao = !string.IsNullOrWhiteSpace(ativo.InstalacaoOriginal);
                    worksheet.Cell(row, 6).Value = mudouInstalacao ? ativo.InstalacaoOriginal : "-";
                    worksheet.Cell(row, 7).Value = ativo.InstalacaoEndereco ?? "-";

                    // Condição e Novo Estado
                    string condicaoRealOriginal = !string.IsNullOrWhiteSpace(ativo.CondicaoOriginal) ? ativo.CondicaoOriginal : (ativo.CondicaoFuncional ?? "-");
                    
                    string condicaoAnterior = "-";
                    string novoEstado = ativo.CondicaoFuncional ?? "-";
                    
                    if (!string.IsNullOrWhiteSpace(ativo.NovoEstadoConservacao) && ativo.NovoEstadoConservacao != condicaoRealOriginal)
                    {
                        condicaoAnterior = condicaoRealOriginal;
                        novoEstado = ativo.NovoEstadoConservacao;
                    }
                    else if (condicaoRealOriginal != ativo.CondicaoFuncional)
                    {
                        condicaoAnterior = condicaoRealOriginal;
                        novoEstado = ativo.CondicaoFuncional ?? "-";
                    }

                    worksheet.Cell(row, 8).Value = condicaoAnterior;
                    worksheet.Cell(row, 9).Value = novoEstado;
                    worksheet.Cell(row, 10).Value = ativo.UsuarioVistoriador ?? "-";
                }

                var range = worksheet.Range(1, 1, alterados.Count + 1, cabecalhos.Length);
                range.CreateTable();
                worksheet.Columns().AdjustToContents();

                using var stream = new MemoryStream();
                workbook.SaveAs(stream);
                var content = stream.ToArray();

                return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Bens_Alterados.xlsx");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Erro Exportar Excel: " + ex.ToString());
                return BadRequest(new { mensagem = ex.Message, stack = ex.StackTrace });
            }
        }
    }
}
