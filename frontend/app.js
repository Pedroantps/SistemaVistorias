async function buscarAtivo() {
    const contrato = document.getElementById("contratoSelect").value;
    const patrimonio = document.getElementById("patrimonioInput").value;
    const erro = document.getElementById("mensagemErro");
    const btn = document.getElementById("btnBuscar");

    // Validação simples
    if (!contrato || !patrimonio) {
        erro.classList.remove("d-none");
        erro.innerText = "Por favor, selecione o contrato e introduza o número do património.";
        return;
    }

    erro.classList.add("d-none");
    
    // Animação de "A carregar" no botão
    const textoOriginal = btn.innerHTML;
    btn.innerHTML = '<span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span> A procurar...';
    btn.disabled = true;

    try {
        // ATENÇÃO: Confirme se a sua API C# está a correr na porta 5158
        const resposta = await fetch(`http://localhost:5158/api/vistorias/buscar?patrimonio=${patrimonio}&contrato=${encodeURIComponent(contrato)}`);
        
        if (resposta.ok) {
            const ativo = await resposta.json();
            
            // Guarda os dados na memória do navegador para usar no ecrã seguinte
            localStorage.setItem("vistoriaEmAndamento", JSON.stringify(ativo));
            
            // Redireciona para o ecrã do formulário e da câmara
            window.location.href = "vistoria.html";
        } else {
            erro.classList.remove("d-none");
            erro.innerText = "Ativo não encontrado. Verifique se o número está correto e se o bem é considerado 'Inservível' no sistema.";
        }
    } catch (error) {
        erro.classList.remove("d-none");
        erro.innerText = "Erro ao contactar o servidor. Verifique se a API está a correr.";
    } finally {
        // Restaura o botão
        btn.innerHTML = textoOriginal;
        btn.disabled = false;
    }
}
