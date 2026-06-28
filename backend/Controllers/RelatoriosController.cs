using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaVistorias.Data;
using SistemaVistorias.Models;
using System.Globalization;
using Word = DocumentFormat.OpenXml.Wordprocessing;

namespace SistemaVistorias.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RelatoriosController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public RelatoriosController(AppDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        [HttpGet("desfazimento")]
        public async Task<IActionResult> GerarRelatorioDesfazimento()
        {
            try
            {
                var ativosVistoriados = await _context.Ativos
                    .AsNoTracking()
                    .Where(a => a.DataVistoria != null)
                    .OrderBy(a => a.ContratoGestao)
                    .ThenBy(a => a.PatrimonioAgevap)
                    .ToListAsync();

                if (ativosVistoriados.Count == 0)
                    return BadRequest(new { mensagem = "Nenhum bem com vistoria realizada foi encontrado. Realize vistorias primeiro." });

                var caminhoTemplate = Path.Combine(_environment.ContentRootPath, "Templates", "ModeloRelatorioDesfazimento.docx");
                if (!System.IO.File.Exists(caminhoTemplate))
                    return StatusCode(500, new { mensagem = "Modelo do relatório não encontrado no servidor." });

                var streamRelatorio = new MemoryStream();
                await using var arquivoTemplate = System.IO.File.OpenRead(caminhoTemplate);
                await arquivoTemplate.CopyToAsync(streamRelatorio);
                streamRelatorio.Position = 0;

                using (var documento = WordprocessingDocument.Open(streamRelatorio, true))
                {
                    var body = documento.MainDocumentPart?.Document.Body;
                    if (body == null)
                        return StatusCode(500, new { mensagem = "Modelo do relatório inválido." });

                    // Remove exemplo padrão do template e insere dados reais
                    PreencherBensNoTemplate(body, ativosVistoriados, documento);
                    documento.MainDocumentPart!.Document.Save();
                }

                streamRelatorio.Position = 0;
                var nomeArquivo = $"Relatorio_Desfazimento_{DateTime.Now:yyyyMMdd_HHmm}.docx";
                return File(
                    streamRelatorio,
                    "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                    nomeArquivo);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { mensagem = $"Erro ao gerar relatório: {ex.Message}" });
            }
        }

        [HttpGet("teste")]
        public async Task<IActionResult> TestarDados()
        {
            try
            {
                var totalAtivos = await _context.Ativos.AsNoTracking().CountAsync();
                var comVistoria = await _context.Ativos.AsNoTracking().Where(a => a.DataVistoria != null).CountAsync();
                var amostraComVistoria = await _context.Ativos
                    .AsNoTracking()
                    .Where(a => a.DataVistoria != null)
                    .Take(2)
                    .Select(a => new { a.PatrimonioAgevap, a.DataVistoria })
                    .ToListAsync();

                return Ok(new
                {
                    totalAtivos,
                    comVistoria,
                    semVistoria = totalAtivos - comVistoria,
                    amostra = amostraComVistoria,
                    temTemplateArquivo = System.IO.File.Exists(Path.Combine(_environment.ContentRootPath, "Templates", "ModeloRelatorioDesfazimento.docx"))
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { mensagem = $"Erro ao testar: {ex.Message}" });
            }
        }

        private static void PreencherBensNoTemplate(Body body, IReadOnlyList<Ativo> ativos, WordprocessingDocument documento)
        {
            // Encontra e remove os bens de exemplo (placeholders do template)
            RemoverBensExemplo(body);

            // Insere os bens reais a partir do primeiro bem vistoriado
            for (int i = 0; i < ativos.Count; i++)
            {
                var ativo = ativos[i];
                InserirelatorioAtivo(body, ativo, documento, i == 0);
            }
        }

        private static void RemoverBensExemplo(Body body)
        {
            // Remove os elementos do template de exemplo
            var toRemove = new List<OpenXmlElement>();

            // Remove todas as tabelas que contêm os dados de exemplo do template
            // (as tabelas na seção "Classificação e Registros Fotográficos")
            var tables = body.Elements<Word.Table>().ToList();
            
            foreach (var table in tables)
            {
                var tableText = table.InnerText;
                // Se a tabela contém informações de exemplo (000, 325, Vista Frontal, etc)
                if (tableText.Contains("000") && tableText.Contains("325") && tableText.Contains("Vista Frontal"))
                {
                    toRemove.Add(table);
                }
            }

            // Remove os parágrafos que são títulos de exemplos ou separadores
            var paragraphs = body.Elements<Word.Paragraph>().ToList();
            bool removendoExemplos = false;

            for (int i = 0; i < paragraphs.Count; i++)
            {
                var paraText = paragraphs[i].InnerText;
                
                // Identifica início da seção de exemplos
                if (paraText.Contains("Classificação") && paraText.Contains("Fotográficos"))
                {
                    removendoExemplos = true;
                    continue;
                }

                // Para de remover quando encontra "Anexo" ou "Conclusões" (outras seções do relatório)
                if (removendoExemplos && (paraText.Contains("Anexo") || paraText.Contains("Conclusões")))
                {
                    removendoExemplos = false;
                }

                // Remove parágrafos que contêm "CG INEA" enquanto estiver na seção de exemplos
                if (removendoExemplos && paraText.Contains("CG INEA") && paraText.Contains("2022"))
                {
                    toRemove.Add(paragraphs[i]);
                }

                // Remove parágrafos com números de exemplo isolados
                if (removendoExemplos && (paraText.Trim() == "000" || paraText.Trim() == "325"))
                {
                    toRemove.Add(paragraphs[i]);
                }
            }

            // Remove todos os elementos marcados
            foreach (var element in toRemove)
            {
                element.Remove();
            }
        }

        private static void InserirelatorioAtivo(Body body, Ativo ativo, WordprocessingDocument documento, bool ehPrimeiro)
        {
            // Encontra a posição de inserção (após a última tabela ou fim do documento)
            var posicaoInsercao = body.Elements().LastOrDefault();

            // Cria seção para o bem
            var paragrafoCabecalho = CriarParagrafo($"Bem Patrimônio AGEVAP nº {ativo.PatrimonioAgevap} - {ativo.ContratoGestao}", bold: true, fontSize: "16");
            
            // Tabela com informações do bem
            var tabelaBem = CriarTabelaBemDetalhado(ativo);

            // Parágrafos para as fotos
            var paragrafFotos = CriarParagrafo("Registros Fotográficos:", bold: true, fontSize: "14");

            // Insere os elementos
            if (posicaoInsercao != null)
            {
                posicaoInsercao.InsertAfterSelf(paragrafoCabecalho);
                paragrafoCabecalho.InsertAfterSelf(tabelaBem);
                tabelaBem.InsertAfterSelf(paragrafFotos);

                // Insere imagens se disponível
                if (!string.IsNullOrEmpty(ativo.CaminhoFotos))
                {
                    InsertarImagensDoAtivo(documento, paragrafFotos, ativo.CaminhoFotos);
                }
            }
        }

        private static Word.Table CriarTabelaBemDetalhado(Ativo ativo)
        {
            var table = new Word.Table();
            table.AppendChild(new TableProperties(
                new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
                new TableBorders(
                    new TopBorder { Val = BorderValues.Single, Size = 4 },
                    new BottomBorder { Val = BorderValues.Single, Size = 4 },
                    new LeftBorder { Val = BorderValues.Single, Size = 4 },
                    new RightBorder { Val = BorderValues.Single, Size = 4 },
                    new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4 },
                    new InsideVerticalBorder { Val = BorderValues.Single, Size = 4 })));

            table.AppendChild(new TableGrid(
                new GridColumn { Width = "1500" },
                new GridColumn { Width = "3500" }));

            // Adiciona linhas com informações do bem
            AdicionarLinhaTabela(table, "Contrato de Gestão:", ativo.ContratoGestao, bold: true);
            AdicionarLinhaTabela(table, "Número do Patrimônio AGEVAP:", ativo.PatrimonioAgevap, bold: false);
            AdicionarLinhaTabela(table, "Patrimônio Órgão Gestor:", ativo.PatrimonioOrgaoGestor, bold: false);
            AdicionarLinhaTabela(table, "Descrição:", ativo.Descricao, bold: false);
            AdicionarLinhaTabela(table, "Endereço:", ativo.InstalacaoEndereco, bold: false);
            AdicionarLinhaTabela(table, "Estado de Conservação:", ativo.NovoEstadoConservacao ?? ativo.CondicaoFuncional, bold: false);
            AdicionarLinhaTabela(table, "Número do Laudo:", ativo.NumeroLaudo ?? "N/A", bold: false);
            AdicionarLinhaTabela(table, "Data da Vistoria:", ativo.DataVistoria?.ToString("dd/MM/yyyy") ?? "N/A", bold: false);

            return table;
        }

        private static void AdicionarLinhaTabela(Word.Table table, string rotulo, string valor, bool bold = false)
        {
            var row = new Word.TableRow();

            // Célula do rótulo
            var cellRotulo = new Word.TableCell(
                new TableCellProperties(new Shading { Fill = "D9E2F3", Val = ShadingPatternValues.Clear }),
                new Paragraph(new ParagraphProperties(new Bold()), 
                    new Run(new RunProperties(new Bold()),
                        new Text(rotulo) { Space = SpaceProcessingModeValues.Preserve })));

            // Célula do valor
            var cellValor = new Word.TableCell(
                new Paragraph(
                    new Run(new RunProperties(
                        new RunFonts { Ascii = "Arial", HighAnsi = "Arial" },
                        new FontSize { Val = "22" }),
                        new Text(valor) { Space = SpaceProcessingModeValues.Preserve })));

            row.Append(cellRotulo, cellValor);
            table.Append(row);
        }

        private static void InsertarImagensDoAtivo(WordprocessingDocument documento, Word.Paragraph paragrafAnterior, string caminhoFotos)
        {
            // TODO: Implementar inserção de imagens
            // Esta função será implementada quando a estrutura do template estiver finalizada
            // Por enquanto, apenas adiciona um parágrafo indicando que há fotos
            try
            {
                if (!string.IsNullOrEmpty(caminhoFotos))
                {
                    var paragrafFoto = new Word.Paragraph(
                        new Run(new Text($"[Fotos disponíveis em: {caminhoFotos}]")));
                    paragrafAnterior.InsertAfterSelf(paragrafFoto);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Erro ao inserir referência de imagens: {ex.Message}");
            }
        }

        private static void AdicionarImagemAoParagrafo(WordprocessingDocument documento, Word.Paragraph paragrafAnterior, string caminhoImagem)
        {
            // Função mantida para referência futura
            // TODO: Implementar inserção de imagens JPEG/PNG em documentos Word
        }

        private static void AdicionarSecaoBensVistoriados(Body body, IReadOnlyList<Ativo> ativos)
        {
            var sectionProperties = body.Elements<SectionProperties>().LastOrDefault();

            void Append(OpenXmlElement element)
            {
                if (sectionProperties != null)
                    body.InsertBefore(element, sectionProperties);
                else
                    body.Append(element);
            }

            Append(new Paragraph(new Run(new Break { Type = BreakValues.Page })));
            Append(CriarParagrafo("Anexo II - Bens com vistoria realizada", bold: true, center: true, fontSize: "24"));
            Append(CriarParagrafo(
                $"Relatório gerado automaticamente em {DateTime.Now:dd/MM/yyyy HH:mm}. Total de bens vistoriados: {ativos.Count}.",
                fontSize: "18"));
            Append(CriarTabelaBens(ativos));
        }

        private static Paragraph CriarParagrafo(string texto, bool bold = false, bool center = false, string fontSize = "18")
        {
            var paragraph = new Paragraph();
            paragraph.ParagraphProperties = new ParagraphProperties(
                new Justification { Val = center ? JustificationValues.Center : JustificationValues.Both },
                new SpacingBetweenLines { After = "160" });

            var runProperties = new RunProperties(
                new RunFonts { Ascii = "Arial", HighAnsi = "Arial", ComplexScript = "Arial" },
                new FontSize { Val = fontSize });

            if (bold)
                runProperties.Append(new Bold(), new BoldComplexScript());

            paragraph.Append(new Run(runProperties, new Text(texto) { Space = SpaceProcessingModeValues.Preserve }));
            return paragraph;
        }

        private static Word.Table CriarTabelaBens(IReadOnlyList<Ativo> ativos)
        {
            var table = new Word.Table();
            table.AppendChild(new TableProperties(
                new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
                new TableBorders(
                    new TopBorder { Val = BorderValues.Single, Size = 4 },
                    new BottomBorder { Val = BorderValues.Single, Size = 4 },
                    new LeftBorder { Val = BorderValues.Single, Size = 4 },
                    new RightBorder { Val = BorderValues.Single, Size = 4 },
                    new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4 },
                    new InsideVerticalBorder { Val = BorderValues.Single, Size = 4 })));

            table.AppendChild(new TableGrid(
                new GridColumn { Width = "450" },
                new GridColumn { Width = "950" },
                new GridColumn { Width = "1000" },
                new GridColumn { Width = "1100" },
                new GridColumn { Width = "2100" },
                new GridColumn { Width = "1100" },
                new GridColumn { Width = "950" },
                new GridColumn { Width = "900" },
                new GridColumn { Width = "950" }));

            table.Append(CriarLinha(true,
                "#",
                "Patrimônio AGEVAP",
                "Patrimônio Órgão",
                "Contrato",
                "Descrição",
                "Localização",
                "Novo estado",
                "Nº laudo",
                "Data"));

            for (var i = 0; i < ativos.Count; i++)
            {
                var ativo = ativos[i];
                table.Append(CriarLinha(false,
                    (i + 1).ToString(CultureInfo.InvariantCulture),
                    ativo.PatrimonioAgevap,
                    ativo.PatrimonioOrgaoGestor,
                    ativo.ContratoGestao,
                    ativo.Descricao,
                    ativo.InstalacaoEndereco,
                    ativo.NovoEstadoConservacao ?? "",
                    ativo.NumeroLaudo ?? "",
                    ativo.DataVistoria?.ToString("dd/MM/yyyy") ?? ""));
            }

            return table;
        }

        private static Word.TableRow CriarLinha(bool cabecalho, params string[] valores)
        {
            var row = new Word.TableRow();
            foreach (var valor in valores)
                row.Append(CriarCelula(valor, cabecalho));

            return row;
        }

        private static Word.TableCell CriarCelula(string texto, bool cabecalho)
        {
            var cellProperties = new TableCellProperties(
                new TableCellMargin(
                    new TopMargin { Width = "60", Type = TableWidthUnitValues.Dxa },
                    new BottomMargin { Width = "60", Type = TableWidthUnitValues.Dxa },
                    new LeftMargin { Width = "70", Type = TableWidthUnitValues.Dxa },
                    new RightMargin { Width = "70", Type = TableWidthUnitValues.Dxa }));

            if (cabecalho)
                cellProperties.Append(new Shading { Fill = "D9E2F3", Val = ShadingPatternValues.Clear });

            var paragraph = new Paragraph(new ParagraphProperties(
                new Justification { Val = cabecalho ? JustificationValues.Center : JustificationValues.Left }));

            var runProperties = new RunProperties(
                new RunFonts { Ascii = "Arial", HighAnsi = "Arial", ComplexScript = "Arial" },
                new FontSize { Val = "14" });

            if (cabecalho)
                runProperties.Append(new Bold(), new BoldComplexScript());

            paragraph.Append(new Run(runProperties, new Text(texto ?? "") { Space = SpaceProcessingModeValues.Preserve }));

            return new Word.TableCell(cellProperties, paragraph);
        }
    }
}
