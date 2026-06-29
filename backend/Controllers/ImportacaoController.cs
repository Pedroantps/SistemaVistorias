using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaVistorias.Data;
using SistemaVistorias.Models;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using ClosedXML.Excel;
using System.Text;
using System.Globalization;

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

        [HttpPost("upload")]
        public async Task<IActionResult> ImportarPlanilha(IFormFile arquivo)
        {
            if (arquivo == null || arquivo.Length == 0)
                return BadRequest(new { mensagem = "Nenhum arquivo enviado." });

            using var stream = new MemoryStream();
            await arquivo.CopyToAsync(stream);
            
            using var workbook = new XLWorkbook(stream);
            
            var abasParaLer = new[] { "Patrimônio Adquirido", "Patrimônio Cedido INEA GUANDU", "Patrimônio Cedido INEA BG" }; 
            
            var contratosPermitidos = new[] { 
                "CG INEA GUANDU", 
                "CG INEA CBHs", 
                "CG INEA BG", 
                "CG INEA Nº 003/2010 - CEDIDO", 
                "CG INEA Nº 002/2017 - CEDIDO",
                "Patrimônio Adquirido"
            };

            int cadastrados = 0;
            int atualizados = 0;
            int ignorados = 0;
            var logsDiagnostico = new List<string>();

            foreach (var nomeAba in abasParaLer)
            {
                if (!workbook.TryGetWorksheet(nomeAba, out var worksheet)) 
                {
                    logsDiagnostico.Add($"Aba '{nomeAba}' não encontrada no arquivo.");
                    continue;
                }

                int linhaCabecalhoIndex = EncontrarLinhaCabecalho(worksheet, nomeAba == "Patrimônio Adquirido" ? 14 : 10);

                // ATUALIZAÇÃO AQUI: Passamos arrays e uma lista de palavras proibidas (como "anterior")
                int colAgevap = ObterIndiceColuna(worksheet, linhaCabecalhoIndex, new[] { "agevap", "adquirido" });
                int colOrgao = ObterIndiceColuna(worksheet, linhaCabecalhoIndex, new[] { "orgao", "órgão", "inea", "cedido", "tombamento" });
                int colDescricao = ObterIndiceColuna(worksheet, linhaCabecalhoIndex, new[] { "descricao", "descrição", "especificacao", "especificação", "bem" });
                int colContrato = ObterIndiceColuna(worksheet, linhaCabecalhoIndex, new[] { "contrato", "cg", "origem" });
                
                // O sistema vai ignorar qualquer coluna que tenha "anterior" ou "antigo" no nome
                int colInstalacao = ObterIndiceColuna(worksheet, linhaCabecalhoIndex, 
                    new[] { "instalacao", "instalação", "local", "setor", "unidade" }, 
                    new[] { "anterior", "antigo" });
                
                int colCondicao = ObterIndiceColuna(worksheet, linhaCabecalhoIndex, 
                    new[] { "condicao", "condição", "estado", "situacao", "situação", "conservacao", "conservação" }, 
                    new[] { "anterior", "antigo" });

                logsDiagnostico.Add($"<strong>{nomeAba}</strong> (Lida na linha {linhaCabecalhoIndex}): Colunas -> AGEVAP: {colAgevap}, Órgão: {colOrgao}, Desc: {colDescricao}, Local: {colInstalacao}, Cond: {colCondicao}, Contrato: {colContrato}");

                if (colInstalacao == -1 || colCondicao == -1)
                {
                    logsDiagnostico.Add($"<span class='text-danger'>Aviso: Coluna de Instalação ou Condição não encontrada na aba {nomeAba}. Os itens desta aba podem ser ignorados.</span>");
                }

                var linhas = worksheet.RowsUsed().Where(r => r.RowNumber() > linhaCabecalhoIndex);

                foreach (var linha in linhas)
                {
                    if (linha.CellsUsed().Count() < 2) continue; 

                    string instalacao = colInstalacao != -1 ? linha.Cell(colInstalacao).GetString().Trim() : "";
                    string condicaoFuncional = colCondicao != -1 ? linha.Cell(colCondicao).GetString().Trim() : "";
                    string contratoLido = colContrato != -1 ? linha.Cell(colContrato).GetString().Trim() : "";
                    string patrimonioAgevap = colAgevap != -1 ? linha.Cell(colAgevap).GetString().Trim() : "";
                    string patrimonioOrgao = colOrgao != -1 ? linha.Cell(colOrgao).GetString().Trim() : "";
                    string descricao = colDescricao != -1 ? linha.Cell(colDescricao).GetString().Trim() : "";

                    if (string.IsNullOrEmpty(patrimonioAgevap) && string.IsNullOrEmpty(patrimonioOrgao) && string.IsNullOrEmpty(descricao))
                    {
                        continue;
                    }

                    string instNorm = RemoverAcentos(instalacao.ToLower());
                    if (!instNorm.Contains("nova sede") && instNorm != "sede")
                    {
                        ignorados++;
                        continue;
                    }

                    string condNorm = RemoverAcentos(condicaoFuncional.ToLower());
                    if (string.IsNullOrEmpty(condNorm) || !condNorm.Contains("inserv"))
                    {
                        ignorados++;
                        continue;
                    }
                    
                    if (string.IsNullOrEmpty(contratoLido))
                    {
                        if (nomeAba.Contains("GUANDU", StringComparison.OrdinalIgnoreCase)) contratoLido = "CG INEA GUANDU";
                        else if (nomeAba.Contains("BG", StringComparison.OrdinalIgnoreCase)) contratoLido = "CG INEA BG";
                        else if (nomeAba.Contains("Adquirido", StringComparison.OrdinalIgnoreCase)) contratoLido = "Patrimônio Adquirido";
                    }

                    string contratoPadronizado = contratosPermitidos.FirstOrDefault(c => 
                        contratoLido.Contains(c, StringComparison.OrdinalIgnoreCase) ||
                        (c == "CG INEA GUANDU" && contratoLido.Contains("DUANDU", StringComparison.OrdinalIgnoreCase)) 
                    );

                    if (contratoPadronizado == null)
                    {
                        ignorados++;
                        continue;
                    }

                    if (string.IsNullOrEmpty(patrimonioAgevap))
                    {
                        if (!string.IsNullOrEmpty(patrimonioOrgao))
                        {
                            patrimonioAgevap = $"OG-{patrimonioOrgao}";
                        }
                        else
                        {
                            ignorados++;
                            continue;
                        }
                    }

                    if (descricao.Length > 500) descricao = descricao.Substring(0, 497) + "...";

                    var ativoExistente = await _context.Ativos
                        .FirstOrDefaultAsync(a => a.PatrimonioAgevap == patrimonioAgevap && a.ContratoGestao == contratoPadronizado);

                    if (ativoExistente != null)
                    {
                        ativoExistente.PatrimonioOrgaoGestor = patrimonioOrgao;
                        ativoExistente.Descricao = descricao;
                        ativoExistente.CondicaoFuncional = condicaoFuncional;
                        ativoExistente.InstalacaoEndereco = instalacao;
                        _context.Ativos.Update(ativoExistente);
                        atualizados++;
                    }
                    else
                    {
                        var novoAtivo = new Ativo
                        {
                            PatrimonioAgevap = patrimonioAgevap,
                            PatrimonioOrgaoGestor = patrimonioOrgao,
                            ContratoGestao = contratoPadronizado, 
                            Descricao = descricao,
                            CondicaoFuncional = condicaoFuncional,
                            InstalacaoEndereco = instalacao
                        };
                        _context.Ativos.Add(novoAtivo);
                        cadastrados++;
                    }
                }
            }

            await _context.SaveChangesAsync();

            string mensagemHtml = $"Importação concluída! <strong>{cadastrados}</strong> itens novos cadastrados e <strong>{atualizados}</strong> atualizados.<br><small class='text-muted'>{ignorados} itens ignorados legitimamente pelos filtros.</small><hr><p class='mb-1 small fw-bold'>Diagnóstico de Colunas Lidas:</p><ul class='small mb-0 text-start'>";
            foreach(var log in logsDiagnostico) {
                mensagemHtml += $"<li class='mb-1'>{log}</li>";
            }
            mensagemHtml += "</ul>";

            return Ok(new { mensagem = mensagemHtml });
        }

        [HttpDelete("limpar-banco")]
        public async Task<IActionResult> LimparBancoDeDados()
        {
            _context.Ativos.RemoveRange(_context.Ativos);
            await _context.SaveChangesAsync();
            return Ok(new { mensagem = "Banco de dados limpo com sucesso!" });
        }

        private int EncontrarLinhaCabecalho(IXLWorksheet ws, int linhaSugerida)
        {
            if (LinhaPareceCabecalho(ws.Row(linhaSugerida))) return linhaSugerida;

            for (int i = 1; i <= 20; i++)
            {
                if (LinhaPareceCabecalho(ws.Row(i))) return i;
            }
            return linhaSugerida; 
        }

        private bool LinhaPareceCabecalho(IXLRow row)
        {
            var rowText = RemoverAcentos(string.Join(" ", row.CellsUsed().Select(c => c.GetString())).ToLower());
            return (rowText.Contains("descricao") || rowText.Contains("especificacao") || rowText.Contains("bem")) && 
                   (rowText.Contains("estado") || rowText.Contains("condicao") || rowText.Contains("situacao") || rowText.Contains("local") || rowText.Contains("instalacao"));
        }

        // ATUALIZAÇÃO AQUI: O método agora aceita a lista de palavras proibidas
        private int ObterIndiceColuna(IXLWorksheet ws, int linhaCabecalhoIndex, string[] palavrasChave, string[] palavrasProibidas = null)
        {
            var linhaCabecalho = ws.Row(linhaCabecalhoIndex);
            foreach (var celula in linhaCabecalho.CellsUsed())
            {
                var valorCabecalho = RemoverAcentos(celula.GetString().Trim().ToLower());

                // Trava: se a coluna tiver uma palavra proibida (ex: "anterior"), ele ignora e passa pra próxima
                if (palavrasProibidas != null && palavrasProibidas.Any(p => valorCabecalho.Contains(RemoverAcentos(p.ToLower()))))
                {
                    continue;
                }

                foreach (var palavra in palavrasChave)
                {
                    if (valorCabecalho.Contains(RemoverAcentos(palavra.ToLower())))
                        return celula.Address.ColumnNumber; 
                }
            }
            return -1; 
        }

        private string RemoverAcentos(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto)) return texto;
            var normalizedString = texto.Normalize(NormalizationForm.FormD);
            var stringBuilder = new StringBuilder();
            foreach (var c in normalizedString)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }
            return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}