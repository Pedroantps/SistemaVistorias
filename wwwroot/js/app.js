/**
 * Busca um ativo patrimonial na API.
 * Chamada a partir da view Index (Busca de Patrimônio).
 */
async function buscarAtivo() {
    const contrato = document.getElementById("contratoGestao").value;
    const patrimonio = document.getElementById("patrimonioInput").value;
    const erro = document.getElementById("mensagemErro");
    const btn = document.getElementById("btnBuscar");

    if (!contrato || !patrimonio) {
        erro.classList.remove("d-none");
        erro.className = "alert alert-danger";
        erro.innerText = "Por favor, selecione o contrato e introduza o número do património.";
        return;
    }

    erro.classList.add("d-none");
    
    const textoOriginal = btn.innerHTML;
    btn.innerHTML = '<span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span> A procurar...';
    btn.disabled = true;

    try {
        const resposta = await fetch(`/api/vistorias/buscar?patrimonio=${patrimonio}&contrato=${encodeURIComponent(contrato)}`, {
            headers: obterHeadersAutenticados()
        });
        
        if (resposta.ok) {
            const dados = await resposta.json();
            const ativo = dados.ativo;

            if (dados.isInservivel) {
                // Fluxo normal: item é inservível, abre vistoria direto
                localStorage.setItem("vistoriaEmAndamento", JSON.stringify(ativo));
                window.location.href = "/Home/Vistoria";
            } else {
                // Item existe mas NÃO é inservível
                erro.classList.remove("d-none");
                erro.className = "alert alert-warning";
                erro.innerHTML = `
                    <strong><i class="bi bi-exclamation-triangle me-1"></i>Este bem não está cadastrado como inservível.</strong><br>
                    Condição funcional atual: <em>${escaparHtmlApp(ativo.condicaoFuncional || "Não informada")}</em><br><br>
                    Deseja cadastrá-lo como <strong>inservível</strong> e iniciar a vistoria?<br><br>
                    <button type="button" class="btn btn-warning btn-sm" id="btnCadastrarInservivel">
                        <i class="bi bi-arrow-repeat me-1"></i> Cadastrar como Inservível e Vistoriar
                    </button>
                `;
                // Usa event listener ao invés de onclick inline para evitar problemas de escape
                document.getElementById("btnCadastrarInservivel").addEventListener("click", function() {
                    ativo.marcarInservivel = true;
                    localStorage.setItem("vistoriaEmAndamento", JSON.stringify(ativo));
                    window.location.href = "/Home/Vistoria";
                });
            }
        } else {
            erro.classList.remove("d-none");
            erro.className = "alert alert-warning";
            erro.innerHTML = `
                <strong>Ativo não encontrado.</strong><br>
                O patrimônio não consta na base de dados oficial. Deseja registrá-lo manualmente?<br><br>
                <button type="button" class="btn btn-warning btn-sm" onclick="registrarAvulso('${escaparHtmlApp(patrimonio)}', '${escaparHtmlApp(contrato)}')">
                    <i class="bi bi-plus-circle me-1"></i> Registrar Vistoria Avulsa
                </button>
            `;
        }
    } catch (error) {
        erro.classList.remove("d-none");
        erro.className = "alert alert-danger";
        erro.innerText = "Erro ao contactar o servidor. Verifique se a API está a correr.";
    } finally {
        btn.innerHTML = textoOriginal;
        btn.disabled = false;
    }
}

window.registrarAvulso = function(patrimonio, contrato) {
    const ativo = {
        patrimonioAgevap: patrimonio,
        contratoGestao: contrato,
        isNovo: true
    };
    localStorage.setItem("vistoriaEmAndamento", JSON.stringify(ativo));
    window.location.href = "/Home/Vistoria";
};

function escaparHtmlApp(valor) {
    return String(valor || "")
        .replaceAll("&", "&amp;")
        .replaceAll("<", "&lt;")
        .replaceAll(">", "&gt;")
        .replaceAll('"', "&quot;")
        .replaceAll("'", "&#039;");
}