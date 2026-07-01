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

    const titulo = document.getElementById('formTitle');
    const btn = document.getElementById('btnSubmit');
    const footerTexto = document.getElementById('footerTexto');
    const link = document.getElementById('linkAlternar');
    const camposExtras = document.querySelectorAll('.registro-extra');

    if (modoRegistro) {
        if (titulo) titulo.textContent = 'Criar nova conta';
        if (btn) btn.innerHTML = 'Registrar';
        if (footerTexto) footerTexto.textContent = 'Já tem conta? ';
        if (link) link.textContent = 'Fazer login';
        camposExtras.forEach(el => el.classList.add('active'));
    } else {
        if (titulo) titulo.textContent = 'Entrar';
        if (btn) btn.innerHTML = 'Entrar';
        if (footerTexto) footerTexto.textContent = 'Não tem conta? ';
        if (link) link.textContent = 'Criar conta';
        camposExtras.forEach(el => el.classList.remove('active'));
    }
}
// ---- Alertas ----
function mostrarAlerta(mensagem, tipo) {
    const el = document.getElementById('alertaLogin');
    const icone = document.getElementById('alertaIcone');
    const texto = document.getElementById('alertaTexto');

    if (el) {
        el.className = 'alert'; // remove d-none
        if (tipo === 'sucesso') {
            el.classList.add('alert-success');
            if(icone) icone.className = 'fas fa-check-circle';
        } else {
            el.classList.add('alert-danger');
            if(icone) icone.className = 'fas fa-exclamation-circle';
        }
        if(texto) texto.textContent = mensagem;
    }
}

function ocultarAlerta() {
    const el = document.getElementById('alertaLogin');
    if (el) {
        el.className = 'alert d-none';
    }
}
