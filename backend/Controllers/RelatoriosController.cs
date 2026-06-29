using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaVistorias.Data;
using SistemaVistorias.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Word = DocumentFormat.OpenXml.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

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
            var paragrafoAncora = body.Elements<Word.Paragraph>()
                .FirstOrDefault(p => p.InnerText.Contains("Os seguintes bens foram classificados como inservíveis"));

            if (paragrafoAncora == null) return;

            var moldeDetalhes = body.Elements<Word.Table>()
                .FirstOrDefault(t => t.InnerText.Contains("Contrato de Gestão") && t.InnerText.Contains("Patrimônio AGEVAP"));
            
            var moldeFotos = body.Elements<Word.Table>()
                .FirstOrDefault(t => t.InnerText.Contains("Vista Frontal") && t.InnerText.Contains("Vista Etiqueta"));

            RemoverBensExemplo(body, paragrafoAncora);

            OpenXmlElement posicaoAtual = paragrafoAncora;

            foreach (var ativo in ativos)
            {
                var tituloBem = CriarParagrafo(ativo.ContratoGestao, bold: true, fontSize: "24");
                posicaoAtual = posicaoAtual.InsertAfterSelf(tituloBem);

                if (moldeDetalhes != null)
                {
                    var novaTabelaDetalhes = (Word.Table)moldeDetalhes.CloneNode(true);
                    PreencherDadosTabelaClonada(novaTabelaDetalhes, ativo);
                    posicaoAtual = posicaoAtual.InsertAfterSelf(novaTabelaDetalhes);
                }

                if (moldeFotos != null)
                {
                    posicaoAtual = posicaoAtual.InsertAfterSelf(new Word.Paragraph(new Word.Run(new Word.Text("")))); 
                    
                    var novaTabelaFotos = (Word.Table)moldeFotos.CloneNode(true);
                    PreencherImagensNaTabela(documento.MainDocumentPart!, novaTabelaFotos, ativo);
                    
                    posicaoAtual = posicaoAtual.InsertAfterSelf(novaTabelaFotos);
                }
                
                posicaoAtual = posicaoAtual.InsertAfterSelf(new Word.Paragraph(new Word.Run(new Word.Text(""))));
            }
        }

        private static void RemoverBensExemplo(Body body, Word.Paragraph paragrafoAncora)
        {
            var elementosParaRemover = new List<OpenXmlElement>();
            var elementoAtual = paragrafoAncora.NextSibling();

            while (elementoAtual != null)
            {
                if (elementoAtual is Word.Paragraph p && 
                   (p.InnerText.Contains("Recomendações") || p.InnerText.Contains("Conclusões")))
                {
                    break;
                }

                elementosParaRemover.Add(elementoAtual);
                elementoAtual = elementoAtual.NextSibling();
            }

            foreach (var el in elementosParaRemover)
            {
                el.Remove();
            }
        }

        private static void PreencherDadosTabelaClonada(Word.Table tabela, Ativo ativo)
        {
            var linhas = tabela.Elements<Word.TableRow>().ToList();

            SubstituirTextoCelula(linhas.ElementAtOrDefault(0), ativo.ContratoGestao);
            SubstituirTextoCelula(linhas.ElementAtOrDefault(1), ativo.PatrimonioAgevap);
            SubstituirTextoCelula(linhas.ElementAtOrDefault(2), ativo.PatrimonioOrgaoGestor);
            SubstituirTextoCelula(linhas.ElementAtOrDefault(3), ativo.Descricao);
            SubstituirTextoCelula(linhas.ElementAtOrDefault(4), ativo.InstalacaoEndereco);
            SubstituirTextoCelula(linhas.ElementAtOrDefault(5), ativo.NovoEstadoConservacao ?? ativo.CondicaoFuncional);
            SubstituirTextoCelula(linhas.ElementAtOrDefault(6), ativo.NumeroLaudo ?? "N/A");
        }

        private static void SubstituirTextoCelula(Word.TableRow linha, string novoTexto)
        {
            if (linha == null) return;
            
            var celulaValor = linha.Elements<Word.TableCell>().LastOrDefault();
            var paragrafo = celulaValor?.Elements<Word.Paragraph>().FirstOrDefault();
            
            if (paragrafo != null)
            {
                var runReferencia = paragrafo.Elements<Word.Run>().FirstOrDefault();
                var propriedades = runReferencia?.RunProperties != null ? 
                    (Word.RunProperties)runReferencia.RunProperties.CloneNode(true) : null;

                paragrafo.RemoveAllChildren<Word.Run>();

                var novoRun = new Word.Run();
                if (propriedades != null) novoRun.Append(propriedades);
                novoRun.Append(new Word.Text(novoTexto ?? string.Empty));
                
                paragrafo.Append(novoRun);
            }
        }

        private static void PreencherImagensNaTabela(MainDocumentPart mainPart, Word.Table tabelaFotos, Ativo ativo)
        {
            // CORREÇÃO 1: Remove IMEDIATAMENTE todos os desenhos/placeholders antigos do molde clonado
            // Isso garante que se uma foto não for encontrada, o espaço ficará vazio em vez de repetir a foto do template
            foreach (var desenho in tabelaFotos.Descendants<Word.Drawing>().ToList())
            {
                desenho.Remove();
            }

            if (string.IsNullOrEmpty(ativo.CaminhoFotos)) return;

            var listaFotos = ativo.CaminhoFotos.Split(';');
            var basePasta = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "fotos_vistorias");

            foreach (var celula in tabelaFotos.Descendants<Word.TableCell>())
            {
                var textoCelula = celula.InnerText;
                string fotoRelativa = null;

                // CORREÇÃO 2: Busca inteligente. Tenta achar pelo nome da posição ("Esquerda"), 
                // se não achar, faz o fallback pela ordem exata em que as fotos foram gravadas no banco.
                if (textoCelula.Contains("Esquerda")) 
                {
                    fotoRelativa = listaFotos.FirstOrDefault(f => f.Contains("Esquerda")) 
                                ?? (listaFotos.Length > 0 ? listaFotos[0] : null);
                }
                else if (textoCelula.Contains("Direita")) 
                {
                    fotoRelativa = listaFotos.FirstOrDefault(f => f.Contains("Direita")) 
                                ?? (listaFotos.Length > 1 ? listaFotos[1] : null);
                }
                else if (textoCelula.Contains("Frontal")) 
                {
                    fotoRelativa = listaFotos.FirstOrDefault(f => f.Contains("Frontal")) 
                                ?? (listaFotos.Length > 2 ? listaFotos[2] : null);
                }
                else if (textoCelula.Contains("Etiqueta")) 
                {
                    fotoRelativa = listaFotos.FirstOrDefault(f => f.Contains("Etiqueta")) 
                                ?? (listaFotos.Length > 3 ? listaFotos[3] : null);
                }

                if (!string.IsNullOrEmpty(fotoRelativa))
                {
                    var caminhoCompleto = Path.Combine(basePasta, fotoRelativa.Replace('/', Path.DirectorySeparatorChar));
                    
                    if (System.IO.File.Exists(caminhoCompleto))
                    {
                        SubstituirTextoPorImagemXml(mainPart, celula, caminhoCompleto);
                    }
                }
            }
        }

        private static void SubstituirTextoPorImagemXml(MainDocumentPart mainPart, Word.TableCell celula, string caminhoArquivo)
        {
            var extensao = Path.GetExtension(caminhoArquivo).ToLower();
            var tipoImagem = extensao == ".png" ? ImagePartType.Png : ImagePartType.Jpeg;

            var imagePart = mainPart.AddImagePart(tipoImagem);
            using (var stream = new FileStream(caminhoArquivo, FileMode.Open, FileAccess.Read))
            {
                imagePart.FeedData(stream);
            }

            string relId = mainPart.GetIdOfPart(imagePart);

            long larguraEmu = 2160000;
            long alturaEmu = 1620000;

            var elementoDrawing = new Word.Drawing(
                new DW.Inline(
                    new DW.Extent() { Cx = larguraEmu, Cy = alturaEmu },
                    new DW.EffectExtent() { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
                    new DW.DocProperties() { Id = (UInt32Value)1U, Name = Path.GetFileName(caminhoArquivo) },
                    new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks() { NoChangeAspect = true }),
                    new A.Graphic(
                        new A.GraphicData(
                            new PIC.Picture(
                                new PIC.NonVisualPictureProperties(
                                    new PIC.NonVisualDrawingProperties() { Id = (UInt32Value)2U, Name = "FotoVistoria" },
                                    new PIC.NonVisualPictureDrawingProperties()),
                                new PIC.BlipFill(
                                    new A.Blip() { Embed = relId, CompressionState = A.BlipCompressionValues.Print },
                                    new A.Stretch(new A.FillRectangle())),
                                new PIC.ShapeProperties(
                                    new A.Transform2D(
                                        new A.Offset() { X = 0L, Y = 0L },
                                        new A.Extents() { Cx = larguraEmu, Cy = alturaEmu }),
                                    new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }))
                    ) { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }
                )
            ) { DistanceFromTop = 0U, DistanceFromBottom = 0U, DistanceFromLeft = 0U, DistanceFromRight = 0U }
            );

            // SOLUÇÃO: Em vez de apagar os Runs do parágrafo existente (o que deletava a legenda),
            // criamos um parágrafo novo, centralizado, e anexamos ele ao final da célula.
            // Assim o relatório fica com o formato: [Legenda Original] na linha 1 e [Foto] na linha 2.
            var paragrafoImagem = new Word.Paragraph(
                new Word.ParagraphProperties(new Word.Justification { Val = Word.JustificationValues.Center }),
                new Word.Run(elementoDrawing)
            );

            celula.Append(paragrafoImagem);
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
    }
}