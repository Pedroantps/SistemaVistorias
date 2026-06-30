using Microsoft.EntityFrameworkCore;
using SistemaVistorias.Data;
using SistemaVistorias.Services;

var builder = WebApplication.CreateBuilder(args);

// Configuração para suportar Controllers com Views
builder.Services.AddControllersWithViews();

// 1. CONFIGURAÇÃO DE CORS (Libera o acesso para o navegador)
builder.Services.AddCors(options => {
    options.AddPolicy("PermitirTudo", policy => {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

// Registro dos Serviços da camada de Negócios
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IVistoriaService, VistoriaService>();
builder.Services.AddScoped<IRelatorioService, RelatorioService>();

var app = builder.Build();

// 2. ATIVAR O CORS (Tem que ficar antes do MapControllers)
app.UseCors("PermitirTudo");

app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

// Mapear rotas de API
app.MapControllers();

// Mapear rota MVC padrão
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Login}/{id?}");

app.Run();