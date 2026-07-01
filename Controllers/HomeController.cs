using Microsoft.AspNetCore.Mvc;

namespace SistemaVistorias.Controllers
{
    /// <summary>
    /// Controller responsável por servir as Views (telas HTML) do sistema.
    /// As rotas de navegação principal do usuário passam por aqui.
    /// </summary>
    public class HomeController : Controller
    {
        /// <summary>
        /// Retorna a view da página de login.
        /// </summary>
        public IActionResult Login()
        {
            return View();
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Dashboard()
        {
            return View();
        }

        public IActionResult Importacao()
        {
            return View();
        }

        public IActionResult Vistoria()
        {
            return View();
        }
    }
}
