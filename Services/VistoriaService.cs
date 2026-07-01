using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SistemaVistorias.Data;
using SistemaVistorias.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace SistemaVistorias.Services
{
    public class VistoriaService : IVistoriaService
    {
        private readonly AppDbContext _context;

        public VistoriaService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Ativo?> BuscarAtivoAsync(string patrimonio, string contrato)
        {
            return await _context.Ativos
                .FirstOrDefaultAsync(a => a.PatrimonioAgevap == patrimonio && a.ContratoGestao == contrato);
        }

        public async Task<(bool Sucesso, string Mensagem)> RegistrarVistoriaAsync(
            Usuario usuario,
            string patrimonioAgevap,
            string contratoGestao,
            string novoEstado,
            string numeroLaudo,
            IFormFile? fotoEsquerda,
            IFormFile? fotoDireita,
            IFormFile? fotoFrontal,
            IFormFile? fotoEtiqueta,
            string? descricao = null,
            string? condicaoFuncional = null,
            string? instalacaoEndereco = null,
            string? patrimonioInea = null)
        {
            var ativo = await BuscarAtivoAsync(patrimonioAgevap, contratoGestao);
            
            if (ativo == null)
            {
                if (string.IsNullOrEmpty(descricao) || string.IsNullOrEmpty(instalacaoEndereco))
                {
                    return (false, "Ativo nao encontrado no sistema. Para registrar uma vistoria avulsa, a descrição e localização do novo ativo devem ser preenchidas.");
                }

                ativo = new Ativo
                {
                    PatrimonioAgevap = patrimonioAgevap,
                    ContratoGestao = contratoGestao,
                    PatrimonioOrgaoGestor = string.IsNullOrWhiteSpace(patrimonioInea) ? patrimonioAgevap : patrimonioInea,
                    Descricao = descricao,
                    ClassificacaoQualidade = "Inservível",
                    CondicaoFuncional = condicaoFuncional ?? "Não informada",
                    InstalacaoEndereco = instalacaoEndereco,
                    IsAvulso = true
                };
                
                _context.Ativos.Add(ativo);
            }

            var contratoSeguro = string.Join("-", contratoGestao.Split(Path.GetInvalidFileNameChars()));
            var pastaDestino = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "fotos_vistorias", contratoSeguro, patrimonioAgevap, "fotos");
            if (!Directory.Exists(pastaDestino))
                Directory.CreateDirectory(pastaDestino);

            var caminhosFotos = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(ativo.CaminhoFotos))
            {
                foreach (var path in ativo.CaminhoFotos.Split(';', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (path.Contains("_Esquerda_")) caminhosFotos["Esquerda"] = path;
                    else if (path.Contains("_Direita_")) caminhosFotos["Direita"] = path;
                    else if (path.Contains("_Frontal_")) caminhosFotos["Frontal"] = path;
                    else if (path.Contains("_Etiqueta_")) caminhosFotos["Etiqueta"] = path;
                }
            }

            async Task ProcessarFoto(IFormFile? novaFoto, string posicao)
            {
                if (novaFoto != null && novaFoto.Length > 0)
                {
                    if (caminhosFotos.ContainsKey(posicao))
                    {
                        var caminhoAntigoAbsoluto = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "fotos_vistorias", caminhosFotos[posicao]);
                        if (System.IO.File.Exists(caminhoAntigoAbsoluto))
                        {
                            System.IO.File.Delete(caminhoAntigoAbsoluto);
                        }
                    }

                    var extensao = Path.GetExtension(novaFoto.FileName);
                    var nomeFicheiro = $"foto_{posicao}_{Guid.NewGuid().ToString()[..4]}{extensao}";
                    var caminhoCompleto = Path.Combine(pastaDestino, nomeFicheiro);
                    using (var stream = new FileStream(caminhoCompleto, FileMode.Create))
                    {
                        await novaFoto.CopyToAsync(stream);
                    }
                    caminhosFotos[posicao] = $"{contratoSeguro}/{patrimonioAgevap}/fotos/{nomeFicheiro}";
                }
            }

            await ProcessarFoto(fotoEsquerda, "Esquerda");
            await ProcessarFoto(fotoDireita, "Direita");
            await ProcessarFoto(fotoFrontal, "Frontal");
            await ProcessarFoto(fotoEtiqueta, "Etiqueta");

            ativo.NovoEstadoConservacao = novoEstado;
            ativo.NumeroLaudo = numeroLaudo;
            if (!string.IsNullOrEmpty(condicaoFuncional) && ativo.CondicaoFuncional != condicaoFuncional)
            {
                if (string.IsNullOrEmpty(ativo.CondicaoOriginal))
                {
                    ativo.CondicaoOriginal = ativo.CondicaoFuncional;
                }
                ativo.CondicaoFuncional = condicaoFuncional;
            }
            if (caminhosFotos.Count > 0)
            {
                ativo.CaminhoFotos = string.Join(";", caminhosFotos.Values);
            }
            ativo.DataVistoria = DateTime.Now;
            ativo.UsuarioVistoriador = usuario.NomeUsuario;

            await _context.SaveChangesAsync();

            return (true, $"Vistoria concluida com sucesso! Registrada por {usuario.NomeCompleto} ({usuario.NomeUsuario}).");
        }
    }
}
