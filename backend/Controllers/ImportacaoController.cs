using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;
using SistemaVistorias.Data;
using SistemaVistorias.Models;

namespace SistemaVistorias.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ImportacaoController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ImportacaoController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost("excel")]
        public async Task<IActionResult> ImportarExcel(IFormFile arquivo)
        {
            if (arquivo == null || arquivo.Length == 0)
                return BadRequest("Nenhum arquivo enviado.");

            var ativosParaSalvar = new List<Ativo>();
            int linhasImportadas = 0;
            int linhasIgnoradas = 0;

            // Lista VIP de Contratos (Regra de Negócio AGEVAP)
            var contratosPermitidos = new List<string> { 
                "CG INEA GUANDU", 
                "CG INEA CBHs", 
                "CG INEA BG" 
            };

            using (var stream = new MemoryStream())
            {
                await arquivo.CopyToAsync(stream);
                using (var workbook = new XLWorkbook(stream))
                {
                    // CORREÇÃO 1: Busca a aba diretamente pelo nome real dela, ignorando a ordem ou abas ocultas
                    var worksheet = workbook.Worksheet("Patrimônio Adquirido"); 

                    // CORREÇÃO 2: Usa RangeUsed().Rows() para mapear toda a extensão de dados do início ao fim
                    var range = worksheet.RangeUsed();
                    if (range == null)
                        return BadRequest("Nenhum dado encontrado na planilha 'Patrimônio Adquirido'.");

                    var linhas = range.Rows();

                    foreach (var linha in linhas)
                    {
                        // 1. Ignora os logótipos e os cabeçalhos (pula tudo o que for antes da linha 15)
                        if (linha.RowNumber() < 15) continue; 

                        // 2. Mapeamento correto das colunas baseado na sua imagem
                        var contratoGestao = linha.Cell("A").GetString().Trim();
                        var patAgevap = linha.Cell("C").GetString().Trim();
                        var patOrgao = linha.Cell("D").GetString().Trim();
                        
                        // PRECISA PREENCHER AS LETRAS ABAIXO (estão ocultas na imagem):
                        var descricao = linha.Cell("L").GetString().Trim(); // Ex: "D"
                        var classQualidade = linha.Cell("R").GetString().Trim(); // Ex: "E"
                        
                        var condFuncional = linha.Cell("S").GetString().Trim();
                        var instalacao = linha.Cell("X").GetString().Trim();

                        // Filtro 1: Ignora se estiver a faltar a Chave Primária
                        if (string.IsNullOrEmpty(patAgevap) || string.IsNullOrEmpty(contratoGestao))
                        {
                            linhasIgnoradas++;
                            continue;
                        }

                        // Filtro 2: REGRA DE NEGÓCIO - Contrato e Condição
                        bool contratoValido = contratosPermitidos.Any(c => c.Equals(contratoGestao, StringComparison.OrdinalIgnoreCase));
                        
                        bool condicaoValida = condFuncional.Equals("inservível", StringComparison.OrdinalIgnoreCase) || 
                                              condFuncional.Equals("inservivel", StringComparison.OrdinalIgnoreCase);

                        if (!contratoValido || !condicaoValida)
                        {
                            linhasIgnoradas++;
                            continue;
                        }

                        // Filtro 3: Evita Duplicidade no banco de dados
                        var jaExiste = await _context.Ativos
                            .AnyAsync(a => a.PatrimonioAgevap == patAgevap && a.ContratoGestao == contratoGestao);

                        if (jaExiste)
                        {
                            linhasIgnoradas++;
                            continue;
                        }

                        // Passou em todos os filtros! Monta o objeto para guardar.
                        ativosParaSalvar.Add(new Ativo
                        {
                            ContratoGestao = contratoGestao,
                            PatrimonioAgevap = patAgevap,
                            PatrimonioOrgaoGestor = patOrgao,
                            Descricao = descricao,
                            ClassificacaoQualidade = classQualidade,
                            CondicaoFuncional = condFuncional,
                            InstalacaoEndereco = instalacao
                        });
                        
                        linhasImportadas++;
                    }
                }
            }

            if (ativosParaSalvar.Any())
            {
                _context.Ativos.AddRange(ativosParaSalvar);
                await _context.SaveChangesAsync();
            }

            return Ok(new { 
                mensagem = "Processamento concluído!", 
                sucesso = linhasImportadas, 
                ignorados = linhasIgnoradas 
            });
        }
    }
}