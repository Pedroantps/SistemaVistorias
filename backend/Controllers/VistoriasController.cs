using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaVistorias.Data;
using SistemaVistorias.Models;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

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

        /// <summary>
        /// GET: api/vistorias/buscar?patrimonio=150&contrato=CG INEA GUANDU
        /// Endpoint que valida a existência do ativo antes de abrir o formulário de vistoria
        /// </summary>
        [HttpGet("buscar")]
        public async Task<IActionResult> BuscarAtivo([FromQuery] string patrimonio, [FromQuery] string contrato)
        {
            // Busca o ativo específico utilizando a chave primária composta
            var ativo = await _context.Ativos
                .FirstOrDefaultAsync(a => a.PatrimonioAgevap == patrimonio && a.ContratoGestao == contrato);

            if (ativo == null)
                return NotFound(new { mensagem = "Ativo não encontrado." });

            return Ok(ativo);
        }

        /// <summary>
        /// POST: api/vistorias/registrar
        /// Endpoint que recebe os dados da vistoria e organiza as fotos em pastas exclusivas
        /// </summary>
        [HttpPost("registrar")]
        public async Task<IActionResult> RegistrarVistoria(
            [FromForm] string patrimonioAgevap, 
            [FromForm] string contratoGestao, 
            [FromForm] string novoEstado, 
            [FromForm] string numeroLaudo, 
            [FromForm] List<IFormFile> fotos)
        {
            // 1. Validação de segurança utilizando a Chave Composta
            var ativo = await _context.Ativos
                .FirstOrDefaultAsync(a => a.PatrimonioAgevap == patrimonioAgevap && a.ContratoGestao == contratoGestao);

            if (ativo == null)
                return NotFound(new { mensagem = "Ativo não encontrado no sistema." });

            // 2. Organização dos Diretórios Físicos
            // Cria o caminho lógico apontando para wwwroot/fotos_vistorias/NUMERO_DO_PATRIMONIO/
            var pastaDestino = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "fotos_vistorias", patrimonioAgevap);
            
            // Cria a subpasta automaticamente caso seja a primeira vistoria deste item
            if (!Directory.Exists(pastaDestino))
                Directory.CreateDirectory(pastaDestino);

            var referenciasBanco = new List<string>();

            // 3. Processamento de Upload das Imagens
            if (fotos != null && fotos.Count > 0)
            {
                int contador = 1;
                foreach (var foto in fotos)
                {
                    if (foto.Length > 0)
                    {
                        var extensao = Path.GetExtension(foto.FileName);
                        
                        // Nome limpo e sequencial. Exemplo: foto_1_a2b3.jpg
                        var nomeFicheiro = $"foto_{contador}_{Guid.NewGuid().ToString()[..4]}{extensao}";
                        var caminhoCompleto = Path.Combine(pastaDestino, nomeFicheiro);

                        using (var stream = new FileStream(caminhoCompleto, FileMode.Create))
                        {
                            await foto.CopyToAsync(stream);
                        }
                        
                        // Guarda o caminho relativo estruturado para o banco de dados
                        referenciasBanco.Add($"{patrimonioAgevap}/{nomeFicheiro}");
                        contador++;
                    }
                }
            }

            // 4. Atualização e Auditoria no MariaDB
            ativo.NovoEstadoConservacao = novoEstado;
            ativo.NumeroLaudo = numeroLaudo;
            
            // Junta as referências das fotos separando-as por ponto e vírgula (;)
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