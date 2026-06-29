using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaVistorias.Data;
using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SistemaVistorias.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class VistoriasController : ControllerBase
    {
        private readonly AppDbContext _context;

        public VistoriasController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("buscar")]
        public async Task<IActionResult> BuscarAtivo([FromQuery] string patrimonio, [FromQuery] string contrato)
        {
            var ativo = await _context.Ativos
                .FirstOrDefaultAsync(a => a.PatrimonioAgevap == patrimonio && a.ContratoGestao == contrato);

            if (ativo == null)
                return NotFound(new { mensagem = "Ativo não encontrado." });

            return Ok(ativo);
        }

        [HttpPost("registrar")]
        public async Task<IActionResult> RegistrarVistoria(
            [FromForm] string patrimonioAgevap, 
            [FromForm] string contratoGestao, 
            [FromForm] string novoEstado, 
            [FromForm] string numeroLaudo, 
            [FromForm] List<IFormFile> fotos)
        {
            var ativo = await _context.Ativos
                .FirstOrDefaultAsync(a => a.PatrimonioAgevap == patrimonioAgevap && a.ContratoGestao == contratoGestao);

            if (ativo == null)
                return NotFound(new { mensagem = "Ativo não encontrado no sistema." });

            var pastaDestino = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "fotos_vistorias", patrimonioAgevap);
            
            if (!Directory.Exists(pastaDestino))
                Directory.CreateDirectory(pastaDestino);

            var referenciasBanco = new List<string>();

            if (fotos != null && fotos.Count > 0)
            {
                // Mapeamento das posições baseado na ordem enviada pelo frontend
                string[] posicoes = { "Esquerda", "Direita", "Frontal", "Etiqueta" };
                
                for (int i = 0; i < fotos.Count; i++)
                {
                    var foto = fotos[i];
                    if (foto.Length > 0)
                    {
                        var extensao = Path.GetExtension(foto.FileName);
                        var posicaoStr = i < posicoes.Length ? posicoes[i] : $"Extra_{i}";
                        
                        // Exemplo de saída: foto_Esquerda_a2b3.jpg
                        var nomeFicheiro = $"foto_{posicaoStr}_{Guid.NewGuid().ToString()[..4]}{extensao}";
                        var caminhoCompleto = Path.Combine(pastaDestino, nomeFicheiro);

                        using (var stream = new FileStream(caminhoCompleto, FileMode.Create))
                        {
                            await foto.CopyToAsync(stream);
                        }
                        
                        referenciasBanco.Add($"{patrimonioAgevap}/{nomeFicheiro}");
                    }
                }
            }

            ativo.NovoEstadoConservacao = novoEstado;
            ativo.NumeroLaudo = numeroLaudo;
            ativo.CaminhoFotos = string.Join(";", referenciasBanco); 
            ativo.DataVistoria = DateTime.Now;

            _context.Ativos.Update(ativo);
            await _context.SaveChangesAsync();

            return Ok(new { 
                mensagem = $"Vistoria concluída com sucesso! Arquivos armazenados na pasta do patrimônio {patrimonioAgevap}." 
            });
        }
    }
}