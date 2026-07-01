/**
 * AUTH.JS - Módulo de Autenticação do Sistema de Vistorias
 * 
 * Centraliza as operações de login, logout, verificação de token e gestão da sessão
 * do lado do cliente utilizando LocalStorage para armazenar o JWT.
 */

const AUTH_API_URL = '/api/auth';

// ---- Headers com Token para chamadas autenticadas ----

function obterHeadersAutenticados() {
    const token = obterToken();
    return token ? { 'Authorization': `Bearer ${token}` } : {};
}

// ---- Funções de LocalStorage ----

function obterToken() {
    return localStorage.getItem('auth_token') || '';
}

function obterUsuarioLogado() {
    try {
        const dados = localStorage.getItem('auth_usuario');
        return dados ? JSON.parse(dados) : null;
    } catch {
        return null;
    }
}

function salvarSessao(token, usuario) {
    localStorage.setItem('auth_token', token);
    localStorage.setItem('auth_usuario', JSON.stringify(usuario));
}

function limparSessao() {
    localStorage.removeItem('auth_token');
    localStorage.removeItem('auth_usuario');
}

// ---- Verificação de Autenticação (chamada em cada página protegida) ----

async function verificarAutenticacao() {
    const token = obterToken();

    if (!token) {
        window.location.href = '/Home/Login';
        return false;
    }

    try {
        const resposta = await fetch(`${AUTH_API_URL}/validar`, {
            method: 'GET',
            headers: { 'Authorization': `Bearer ${token}` }
        });

        if (!resposta.ok) {
            limparSessao();
            window.location.href = '/Home/Login';
            return false;
        }

        const dados = await resposta.json();

        // Atualiza dados do usuário localmente
        if (dados.usuario) {
            localStorage.setItem('auth_usuario', JSON.stringify(dados.usuario));
        }

        // Exibe nome do usuário na sidebar (se existir o elemento)
        exibirUsuarioNaSidebar();
        return true;

    } catch {
        // Se o servidor estiver offline, permitir acesso baseado no token local
        // para não bloquear uso offline total, mas exibir aviso
        console.warn('Não foi possível validar sessão com o servidor.');
        exibirUsuarioNaSidebar();
        return true;
    }
}

// ---- Exibir nome do usuário na sidebar ----

function exibirUsuarioNaSidebar() {
    const usuario = obterUsuarioLogado();
    const el = document.getElementById('sidebarUsuario');
    if (el && usuario) {
        el.textContent = usuario.nomeCompleto || usuario.nomeUsuario || 'Usuário';
    }
}

// ---- Login ----

async function realizarLogin(nomeUsuario, senha) {
    const resposta = await fetch(`${AUTH_API_URL}/login`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ nomeUsuario, senha })
    });

    const dados = await resposta.json();

    if (!resposta.ok) {
        throw new Error(dados.mensagem || 'Erro ao realizar login.');
    }

    salvarSessao(dados.token, dados.usuario);
    return dados;
}

// ---- Registro ----

async function realizarRegistro(nomeUsuario, senha, nomeCompleto) {
    const resposta = await fetch(`${AUTH_API_URL}/registro`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ nomeUsuario, senha, nomeCompleto })
    });

    const dados = await resposta.json();

    if (!resposta.ok) {
        throw new Error(dados.mensagem || 'Erro ao realizar registro.');
    }

    return dados;
}

// ---- Logout ----

async function realizarLogout() {
    const token = obterToken();

    try {
        if (token) {
            await fetch(`${AUTH_API_URL}/logout`, {
                method: 'POST',
                headers: { 'Authorization': `Bearer ${token}` }
            });
        }
    } catch {
        // Ignora erro de rede no logout — limpa sessão de qualquer forma
    }

    limparSessao();
    window.location.href = '/Home/Login';
}
