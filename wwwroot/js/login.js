// ---- Estado ----
let modoRegistro = false;

// ---- Se já autenticado, redireciona ----
(function() {
    const token = obterToken();
    if (token) {
        // Verifica rapidamente e redireciona
        fetch(`${AUTH_API_URL}/validar`, {
            method: 'GET',
            headers: { 'Authorization': `Bearer ${token}` }
        }).then(r => {
            if (r.ok) window.location.href = '/Home/Index';
        }).catch(() => {});
    }
})();

// ---- Formulário ----
document.getElementById('formLogin').addEventListener('submit', async function(e) {
    e.preventDefault();
    ocultarAlerta();

    const usuario = document.getElementById('inputUsuario').value.trim();
    const senha = document.getElementById('inputSenha').value;
    const btn = document.getElementById('btnSubmit');
    const textoOriginal = btn.innerHTML;

    if (!usuario || !senha) {
        mostrarAlerta('Preencha todos os campos.', 'erro');
        return;
    }

    btn.disabled = true;
    btn.innerHTML = '<span class="spinner-border spinner-border-sm" role="status"></span>Aguarde...';

    try {
        if (modoRegistro) {
            // Registro
            const nomeCompleto = document.getElementById('inputNomeCompleto').value.trim();
            const confirmarSenha = document.getElementById('inputConfirmarSenha').value;

            if (senha !== confirmarSenha) {
                mostrarAlerta('As senhas não coincidem.', 'erro');
                return;
            }

            if (senha.length < 4) {
                mostrarAlerta('A senha deve ter no mínimo 4 caracteres.', 'erro');
                return;
            }

            await realizarRegistro(usuario, senha, nomeCompleto);
            mostrarAlerta('Conta criada com sucesso! Faça login para continuar.', 'sucesso');

            // Volta para modo login
            setTimeout(() => alternarModo(null), 1500);

        } else {
            // Login
            await realizarLogin(usuario, senha);
            window.location.href = '/Home/Index';
        }

    } catch (error) {
        mostrarAlerta(error.message || 'Erro ao conectar com o servidor.', 'erro');
    } finally {
        btn.disabled = false;
        btn.innerHTML = textoOriginal;
    }
});

// ---- Alternar Login / Registro ----
function alternarModo(e) {
    if (e) e.preventDefault();
    modoRegistro = !modoRegistro;
    ocultarAlerta();

    const titulo = document.getElementById('tituloCard');
    const subtitulo = document.getElementById('subtituloCard');
    const btn = document.getElementById('btnSubmit');
    const footerTexto = document.getElementById('footerTexto');
    const link = document.getElementById('linkAlternar');
    const camposExtras = document.querySelectorAll('.registro-extra');

    if (modoRegistro) {
        titulo.textContent = 'Criar nova conta';
        subtitulo.textContent = 'Preencha os dados para registrar seu acesso';
        btn.innerHTML = '<i class="bi bi-person-plus me-2"></i>Registrar';
        footerTexto.textContent = 'Já tem conta? ';
        link.textContent = 'Fazer login';
        camposExtras.forEach(el => el.classList.add('visivel'));
    } else {
        titulo.textContent = 'Bem-vindo de volta';
        subtitulo.textContent = 'Entre com suas credenciais para acessar o sistema';
        btn.innerHTML = '<i class="bi bi-box-arrow-in-right me-2"></i>Entrar';
        footerTexto.textContent = 'Não tem conta? ';
        link.textContent = 'Criar conta';
        camposExtras.forEach(el => el.classList.remove('visivel'));
    }
}

// ---- Toggle Visibilidade Senha ----
function toggleSenha() {
    const input = document.getElementById('inputSenha');
    const icone = document.getElementById('iconeSenha');
    if (input.type === 'password') {
        input.type = 'text';
        icone.className = 'bi bi-eye-slash';
    } else {
        input.type = 'password';
        icone.className = 'bi bi-eye';
    }
}

// ---- Alertas ----
function mostrarAlerta(mensagem, tipo) {
    const el = document.getElementById('loginAlerta');
    const icone = document.getElementById('alertaIcone');
    const texto = document.getElementById('alertaTexto');

    el.className = 'login-alert visivel';
    if (tipo === 'sucesso') {
        el.classList.add('alert-sucesso');
        icone.className = 'bi bi-check-circle-fill';
    } else {
        el.classList.add('alert-erro');
        icone.className = 'bi bi-exclamation-circle-fill';
    }
    texto.textContent = mensagem;
}

function ocultarAlerta() {
    const el = document.getElementById('loginAlerta');
    el.className = 'login-alert';
}
