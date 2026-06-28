using Microsoft.EntityFrameworkCore;
using SistemaVistorias.Models;

namespace SistemaVistorias.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        // Mapeia o modelo Ativo para a tabela "Ativos" no MariaDB
        public DbSet<Ativo> Ativos { get; set; }
    }
}