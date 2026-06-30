using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.AspNetCore.Hosting;
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

namespace SistemaVistorias.Services
{
    public class RelatorioService : IRelatorioService
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public RelatorioService(AppDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        public async Task<(bool Sucesso, string Mensagem, byte[]? Arquivo, string NomeArquivo)> GerarRelatorioDesfazimentoAsync()
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
                    return (false, "Nenhum bem com vistoria realizada foi encontrado. Realize vistorias primeiro.", null, string.Empty);

                var caminhoTemplate = Path.Combine(_environment.ContentRootPath, "Templates", "ModeloRelatorioDesfazimento.docx");
                if (!System.IO.File.Exists(caminhoTemplate))
                    return (false, "Modelo do relatório não encontrado no servidor.", null, string.Empty);

                var streamRelatorio = new MemoryStream();
                await using var arquivoTemplate = System.IO.File.OpenRead(caminhoTemplate);
                await arquivoTemplate.CopyToAsync(streamRelatorio);
                streamRelatorio.Position = 0;

                using (var documento = WordprocessingDocument.Open(streamRelatorio, true))
                {
                    var body = documento.MainDocumentPart?.Document.Body;
                    if (body == null)
                        return (false, "Modelo do relatório inválido.", null, string.Empty);

                    PreencherBensNoTemplate(body, ativosVistoriados, documento);
                    documento.MainDocumentPart!.Document.Save();
                }

                var nomeArquivo = $"Relatorio_Desfazimento_{DateTime.Now:yyyyMMdd_HHmm}.docx";
                return (true, "Relatório gerado com sucesso.", streamRelatorio.ToArray(), nomeArquivo);
            }
            catch (Exception ex)
            {
                return (false, $"Erro ao gerar relatório: {ex.Message}", null, string.Empty);
            }
        }

        public async Task<object> TestarDadosAsync()
        {
            var totalAtivos = await _context.Ativos.AsNoTracking().CountAsync();
            var comVistoria = await _context.Ativos.AsNoTracking().Where(a => a.DataVistoria != null).CountAsync();
            var amostraComVistoria = await _context.Ativos
                .AsNoTracking()
                .Where(a => a.DataVistoria != null)
                .Take(2)
                .Select(a => new { a.PatrimonioAgevap, a.DataVistoria })
                .ToListAsync();

            return new
            {
                totalAtivos,
                comVistoria,
                semVistoria = totalAtivos - comVistoria,
                amostra = amostraComVistoria,
                temTemplateArquivo = System.IO.File.Exists(Path.Combine(_environment.ContentRootPath, "Templates", "ModeloRelatorioDesfazimento.docx"))
            };
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

            if (moldeDetalhes == null && moldeFotos == null) return;

            RemoverBensExemplo(body, paragrafoAncora);

            OpenXmlElement posicaoAtual = paragrafoAncora;

            var ativosAgrupados = ativos
                .GroupBy(a => string.IsNullOrWhiteSpace(a.ContratoGestao) ? "Sem Contrato" : a.ContratoGestao)
                .OrderBy(g => g.Key);

            foreach (var grupo in ativosAgrupados)
            {
                var tituloContrato = CriarParagrafo(grupo.Key, bold: true, fontSize: "24");
                posicaoAtual = posicaoAtual.InsertAfterSelf(tituloContrato);

                foreach (var ativo in grupo)
                {
                    if (moldeDetalhes != null || moldeFotos != null)
                    {
                        Word.Table novaTabela = null;
                        
                        if (moldeDetalhes != null)
                        {
                            novaTabela = (Word.Table)moldeDetalhes.CloneNode(true);
                            PreencherDadosTabelaClonada(novaTabela, ativo);
                        }

                        if (moldeFotos != null)
                        {
                            var linhasFotos = GerarLinhasDeFotos(documento.MainDocumentPart!, moldeFotos, ativo);
                            
                            if (novaTabela != null)
                            {
                                // Mescla APENAS as linhas de fotos (sem espaços extras)
                                foreach (var linha in linhasFotos)
                                {
                                    novaTabela.Append(linha);
                                }
                            }
                            else
                            {
                                novaTabela = new Word.Table();
                                foreach (var linha in linhasFotos) novaTabela.Append(linha);
                            }
                        }
                        
                        posicaoAtual = posicaoAtual.InsertAfterSelf(novaTabela!);
                    }
                    
                    posicaoAtual = posicaoAtual.InsertAfterSelf(new Word.Paragraph(new Word.Run(new Word.Text(""))));
                }
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

            foreach (var linha in linhas)
            {
                var celulas = linha.Elements<Word.TableCell>().ToList();
                if (celulas.Count < 2) continue;

                var rotulo = celulas[0].InnerText.ToLower();

                if (rotulo.Contains("contrato")) SubstituirTextoCelula(linha, ativo.ContratoGestao);
                else if (rotulo.Contains("agevap")) SubstituirTextoCelula(linha, ativo.PatrimonioAgevap);
                else if (rotulo.Contains("inea")) SubstituirTextoCelula(linha, ativo.PatrimonioOrgaoGestor);
                else if (rotulo.Contains("descri")) SubstituirTextoCelula(linha, ativo.Descricao);
                else if (rotulo.Contains("endere") || rotulo.Contains("localiza")) SubstituirTextoCelula(linha, ativo.InstalacaoEndereco);
                else if (rotulo.Contains("estado") || rotulo.Contains("conserva")) SubstituirTextoCelula(linha, ativo.NovoEstadoConservacao ?? ativo.CondicaoFuncional);
                else if (rotulo.Contains("laudo")) SubstituirTextoCelula(linha, ativo.NumeroLaudo ?? "N/A");
            }
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

        private static List<Word.TableRow> GerarLinhasDeFotos(MainDocumentPart mainPart, Word.Table moldeFotos, Ativo ativo)
        {
            var linhasGeradas = new List<Word.TableRow>();
            if (string.IsNullOrEmpty(ativo.CaminhoFotos)) return linhasGeradas;

            var listaFotos = ativo.CaminhoFotos.Split(';');
            var basePasta = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "fotos_vistorias");

            var linhasOriginais = moldeFotos.Elements<Word.TableRow>().ToList();

            foreach (var linhaLegenda in linhasOriginais)
            {
                var textoLinha = linhaLegenda.InnerText;
                if (!textoLinha.Contains("Frontal") && !textoLinha.Contains("Esquerda") && !textoLinha.Contains("Direita") && !textoLinha.Contains("Etiqueta"))
                    continue;

                // Clona a linha de legenda para criar a linha de fotos
                var linhaFoto = (Word.TableRow)linhaLegenda.CloneNode(true);
                
                // Limpa o texto da nova linha
                foreach (var run in linhaFoto.Descendants<Word.Run>().ToList())
                    run.RemoveAllChildren<Word.Text>();
                
                foreach (var p in linhaFoto.Descendants<Word.Paragraph>())
                {
                    if (p.ParagraphProperties == null) p.PrependChild(new Word.ParagraphProperties());
                    p.ParagraphProperties.Justification = new Word.Justification { Val = Word.JustificationValues.Center };
                }

                // Legenda final que sera usada
                var legendaFinal = (Word.TableRow)linhaLegenda.CloneNode(true);

                var celulasFoto = linhaFoto.Elements<Word.TableCell>().ToList();
                var celulasLegenda = legendaFinal.Elements<Word.TableCell>().ToList();

                for (int c = 0; c < celulasLegenda.Count; c++)
                {
                    var textoCelula = celulasLegenda[c].InnerText;
                    string fotoRelativa = null;

                    if (textoCelula.Contains("Esquerda")) fotoRelativa = listaFotos.FirstOrDefault(f => f.Contains("Esquerda")) ?? (listaFotos.Length > 0 ? listaFotos[0] : null);
                    else if (textoCelula.Contains("Direita")) fotoRelativa = listaFotos.FirstOrDefault(f => f.Contains("Direita")) ?? (listaFotos.Length > 1 ? listaFotos[1] : null);
                    else if (textoCelula.Contains("Frontal")) fotoRelativa = listaFotos.FirstOrDefault(f => f.Contains("Frontal")) ?? (listaFotos.Length > 2 ? listaFotos[2] : null);
                    else if (textoCelula.Contains("Etiqueta")) fotoRelativa = listaFotos.FirstOrDefault(f => f.Contains("Etiqueta")) ?? (listaFotos.Length > 3 ? listaFotos[3] : null);

                    if (c < celulasFoto.Count) AdicionarBordasNaCelula(celulasFoto[c]);
                    AdicionarBordasNaCelula(celulasLegenda[c]);

                    if (!string.IsNullOrEmpty(fotoRelativa))
                    {
                        var caminhoCompleto = Path.Combine(basePasta, fotoRelativa.Replace('/', Path.DirectorySeparatorChar));
                        if (System.IO.File.Exists(caminhoCompleto))
                        {
                            if (c < celulasFoto.Count)
                            {
                                SubstituirTextoPorImagemXml(mainPart, celulasFoto[c], caminhoCompleto);
                            }
                        }
                    }
                }

                linhasGeradas.Add(linhaFoto);
                linhasGeradas.Add(legendaFinal);
            }

            return linhasGeradas;
        }

        private static void AdicionarBordasNaCelula(Word.TableCell celula)
        {
            var tcPr = celula.GetFirstChild<Word.TableCellProperties>();
            if (tcPr == null) 
            {
                tcPr = new Word.TableCellProperties();
                celula.InsertAt(tcPr, 0);
            }
            
            var borders = tcPr.GetFirstChild<Word.TableCellBorders>();
            if (borders == null)
            {
                borders = new Word.TableCellBorders();
                tcPr.Append(borders);
            }

            if (borders.TopBorder == null) borders.TopBorder = new Word.TopBorder { Val = Word.BorderValues.Single, Size = 4 };
            if (borders.BottomBorder == null) borders.BottomBorder = new Word.BottomBorder { Val = Word.BorderValues.Single, Size = 4 };
            if (borders.LeftBorder == null) borders.LeftBorder = new Word.LeftBorder { Val = Word.BorderValues.Single, Size = 4 };
            if (borders.RightBorder == null) borders.RightBorder = new Word.RightBorder { Val = Word.BorderValues.Single, Size = 4 };
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

            var paragrafoImagem = new Word.Paragraph(
                new Word.ParagraphProperties(new Word.Justification { Val = Word.JustificationValues.Center }),
                new Word.Run(elementoDrawing)
            );

            celula.RemoveAllChildren<Word.Paragraph>();
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
