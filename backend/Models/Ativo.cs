using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore; // <-- Necessário para usar a Chave Composta

namespace SistemaVistorias.Models
{
    // Aqui está a mágica: definimos que a combinação dessas duas colunas é a chave única
    [PrimaryKey(nameof(PatrimonioAgevap), nameof(ContratoGestao))]
    public class Ativo
    {
        // Removemos o [Key] que ficava aqui
        public string PatrimonioAgevap { get; set; } = string.Empty;

        public string ContratoGestao { get; set; } = string.Empty;

        public string PatrimonioOrgaoGestor { get; set; } = string.Empty;

        public string Descricao { get; set; } = string.Empty;

        public string ClassificacaoQualidade { get; set; } = string.Empty;

        public string CondicaoFuncional { get; set; } = string.Empty;

        public string InstalacaoEndereco { get; set; } = string.Empty;

        // --- CAMPOS DE AUDITORIA ---
        public string? NovoEstadoConservacao { get; set; }
        
        public string? NumeroLaudo { get; set; }
        
        public string? CaminhoFotos { get; set; }
        
        public DateTime? DataVistoria { get; set; }
    }
}