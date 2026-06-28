// Variáveis globais para guardar a chave do bem
let patrimonioAtual = "";
let contratoAtual = "";

// 1. Inicialização: Recupera os dados assim que a página carrega
document.addEventListener("DOMContentLoaded", () => {
    const vistoriaSalva = localStorage.getItem("vistoriaEmAndamento");
    
    // Se não tiver dados inicializados, volta para a tela de busca para evitar erros
    if (!vistoriaSalva) {
        window.location.href = "index.html";
        return;
    }

    // Processa os dados recuperados
    const ativo = JSON.parse(vistoriaSalva);
    
    patrimonioAtual = ativo.patrimonioAgevap;
    contratoAtual = ativo.contratoGestao;

    // Preenche as etiquetas na tela (C# envia propriedades minúsculas no JSON padrão)
    document.getElementById("lblPatrimonio").innerText = ativo.patrimonioAgevap;
    document.getElementById("lblContrato").innerText = ativo.contratoGestao;
    document.getElementById("lblDescricao").innerText = ativo.descricao;
    document.getElementById("lblCondicao").innerText = ativo.condicaoFuncional;
    document.getElementById("lblLocalizacao").innerText = ativo.instalacaoEndereco;
});

// 2. Envio do Formulário para a API C#
document.getElementById("formVistoria").addEventListener("submit", async function(evento) {
    evento.preventDefault(); // Impede a página de recarregar
    
    const btnSalvar = document.getElementById("btnSalvar");
    const alerta = document.getElementById("mensagemAlerta");
    
    const novoEstado = document.getElementById("novoEstado").value;
    const numeroLaudo = document.getElementById("numeroLaudo").value;
    const arquivos = [
        document.getElementById("fotoEsquerda"),
        document.getElementById("fotoDireita"),
        document.getElementById("fotoFrontal"),
        document.getElementById("fotoEtiqueta")
    ];

    const arquivosSelecionados = arquivos.filter(input => input.files.length > 0);
    if (arquivosSelecionados.length < 4) {
        alerta.className = "alert alert-warning";
        alerta.innerText = "Selecione uma foto para cada quadrado: Esquerda, Direita, Frontal e Etiqueta.";
        alerta.classList.remove("d-none");
        return;
    }

    // Prepara os dados no formato multipart/form-data
    const formData = new FormData();
    formData.append("patrimonioAgevap", patrimonioAtual);
    formData.append("contratoGestao", contratoAtual);
    formData.append("novoEstado", novoEstado);
    formData.append("numeroLaudo", numeroLaudo);

    // Anexa cada foto selecionada no formulário
    arquivos.forEach(input => {
        formData.append("fotos", input.files[0]);
    });

    // Atualiza botão para estado de carregamento
    const textoOriginal = btnSalvar.innerHTML;
    btnSalvar.innerHTML = '<span class="spinner-border spinner-border-sm"></span> A enviar...';
    btnSalvar.disabled = true;
    alerta.classList.add("d-none");

    try {
        // Envia para o nosso Back-end na porta 5158
        const resposta = await fetch("http://localhost:5158/api/vistorias/registrar", {
            method: "POST",
            body: formData
        });

        const resultado = await resposta.json();

        if (resposta.ok) {
            alerta.className = "alert alert-success";
            alerta.innerText = resultado.mensagem;
            alerta.classList.remove("d-none");
            
            // Limpa o armazenamento e os campos após sucesso
            localStorage.removeItem("vistoriaEmAndamento");
            document.getElementById("formVistoria").reset();
            
            // Redireciona de volta após 3 segundos
            setTimeout(() => {
                window.location.href = "index.html";
            }, 3000);
        } else {
            alerta.className = "alert alert-danger";
            alerta.innerText = resultado.mensagem || "Erro ao registrar vistoria.";
            alerta.classList.remove("d-none");
        }
    } catch (erro) {
        alerta.className = "alert alert-danger";
        alerta.innerText = "Erro de conexão com o servidor. Verifique a sua rede.";
        alerta.classList.remove("d-none");
    } finally {
        btnSalvar.innerHTML = textoOriginal;
        btnSalvar.disabled = false;
    }
});

const inputEsquerda = document.getElementById("fotoEsquerda");
const inputDireita = document.getElementById("fotoDireita");
const inputFrontal = document.getElementById("fotoFrontal");
const inputEtiqueta = document.getElementById("fotoEtiqueta");

const previewElementos = [
    { preview: document.getElementById("previewEsquerda"), input: inputEsquerda },
    { preview: document.getElementById("previewDireita"), input: inputDireita },
    { preview: document.getElementById("previewFrontal"), input: inputFrontal },
    { preview: document.getElementById("previewEtiqueta"), input: inputEtiqueta }
];

previewElementos.forEach(({ preview, input }) => {
    const openInput = () => input.click();
    preview.addEventListener("click", openInput);
    preview.addEventListener("keydown", event => {
        if (event.key === "Enter" || event.key === " ") {
            event.preventDefault();
            openInput();
        }
    });

    input.addEventListener("change", () => {
        updatePreview(preview, input.files[0]);
    });
});

function updatePreview(preview, arquivo) {
    preview.innerHTML = "";
    if (arquivo) {
        const img = document.createElement("img");
        img.src = URL.createObjectURL(arquivo);
        img.alt = "Pré-visualização";
        img.className = "preview-image";
        preview.appendChild(img);
    } else {
        const placeholder = document.createElement("div");
        placeholder.className = "preview-placeholder";
        placeholder.innerHTML = `<i class="bi bi-camera-fill"></i><span>${preview.closest('.photo-card').querySelector('.photo-card-label').innerText}</span>`;
        preview.appendChild(placeholder);
    }
}

// Inicializa placeholders sem imagens
previewElementos.forEach(({ preview }) => updatePreview(preview, null));