async function buscarAtivo() {
    const contrato = document.getElementById("contratoGestao").value;
    const patrimonio = document.getElementById("patrimonioInput").value;
    const erro = document.getElementById("mensagemErro");
    const btn = document.getElementById("btnBuscar");

    if (!contrato || !patrimonio) {
        erro.classList.remove("d-none");
        erro.innerText = "Por favor, selecione o contrato e introduza o número do património.";
        return;
    }

    erro.classList.add("d-none");
    
    const textoOriginal = btn.innerHTML;
    btn.innerHTML = '<span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span> A procurar...';
    btn.disabled = true;

    try {
        const resposta = await fetch(`http://localhost:5158/api/vistorias/buscar?patrimonio=${patrimonio}&contrato=${encodeURIComponent(contrato)}`, {
            headers: obterHeadersAutenticados()
        });
        
        if (resposta.ok) {
            const ativo = await resposta.json();
            localStorage.setItem("vistoriaEmAndamento", JSON.stringify(ativo));
            window.location.href = "/Home/Vistoria";
        } else {
            erro.classList.remove("d-none");
            erro.classList.remove("alert-danger");
            erro.classList.add("alert-warning");
            erro.innerHTML = `
                <strong>Ativo não encontrado.</strong><br>
                O patrimônio não consta na base de dados oficial. Deseja registrá-lo manualmente?<br><br>
                <button type="button" class="btn btn-warning btn-sm" onclick="registrarAvulso('${patrimonio}', '${contrato}')">
                    <i class="bi bi-plus-circle me-1"></i> Registrar Vistoria Avulsa
                </button>
            `;
        }
    } catch (error) {
        erro.classList.remove("d-none");
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