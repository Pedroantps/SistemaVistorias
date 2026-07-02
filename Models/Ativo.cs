using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace SistemaVistorias.Models
{
    /// <summary>
    /// Representa um bem patrimonial (ativo) registrado no sistema.
    /// Possui chave primária composta pelo Número do Patrimônio e o Contrato de Gestão.
    /// </summary>
    [PrimaryKey(nameof(PatrimonioAgevap), nameof(ContratoGestao))]
    public class Ativo
    {
        public string PatrimonioAgevap { get; set; } = string.Empty;

        public string ContratoGestao { get; set; } = string.Empty;

        public string PatrimonioOrgaoGestor { get; set; } = string.Empty;

        public string Descricao { get; set; } = string.Empty;

        public string ClassificacaoQualidade { get; set; } = string.Empty;

        public string CondicaoFuncional { get; set; } = string.Empty;

        public string? CondicaoOriginal { get; set; }

        public string InstalacaoEndereco { get; set; } = string.Empty;

        public string? InstalacaoOriginal { get; set; }
        
        public string? PatrimonioOrgaoOriginal { get; set; }

        public bool IsAvulso { get; set; } = false;

        public string? NovoEstadoConservacao { get; set; }

        public string? NumeroLaudo { get; set; }

        public string? CaminhoFotos { get; set; }

        public DateTime? DataVistoria { get; set; }

        public string? UsuarioVistoriador { get; set; }
    }
}