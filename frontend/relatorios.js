async function baixarRelatorioDesfazimento() {
    const alerta = obterElementoAlertaRelatorio();
    const btn = document.getElementById("btnRelatorio");
    const textoOriginal = btn.innerHTML;

    ocultarAlertaRelatorio(alerta);
    btn.innerHTML = '<span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span> A gerar...';
    btn.disabled = true;

    try {
        const resposta = await fetch("http://localhost:5158/api/relatorios/desfazimento");

        if (!resposta.ok) {
            let mensagem = "Não foi possível gerar o relatório.";
            try {
                const resultado = await resposta.json();
                mensagem = resultado.mensagem || mensagem;
            } catch {
                // Mantém a mensagem padrão quando a API não retornar JSON.
                if (resposta.status === 404) {
                    mensagem = "Nenhum bem com vistoria realizada foi encontrado. Realize vistorias primeiro.";
                } else if (resposta.status === 500) {
                    mensagem = "Erro no servidor ao gerar o relatório.";
                }
            }

            mostrarAlertaRelatorio(alerta, mensagem);
            return;
        }

        const blob = await resposta.blob();
        const url = window.URL.createObjectURL(blob);
        const link = document.createElement("a");
        link.href = url;
        link.download = obterNomeArquivoRelatorio(resposta) || "Relatorio_Desfazimento.docx";
        document.body.appendChild(link);
        link.click();
        link.remove();
        window.URL.revokeObjectURL(url);
    } catch (error) {
        console.error("Erro:", error);
        mostrarAlertaRelatorio(alerta, "Erro ao contactar o servidor. Verifique se a API está a correr na porta 5158.");
    } finally {
        btn.innerHTML = textoOriginal;
        btn.disabled = false;
    }
}

function obterNomeArquivoRelatorio(resposta) {
    const contentDisposition = resposta.headers.get("content-disposition");
    if (!contentDisposition) {
        return "";
    }

    const filenameMatch = contentDisposition.match(/filename\*?=(?:UTF-8'')?["']?([^;"']+)/i);
    return filenameMatch ? decodeURIComponent(filenameMatch[1]) : "";
}

function obterElementoAlertaRelatorio() {
    return document.getElementById("dashboardAlerta") || document.getElementById("mensagemErro") || document.getElementById("mensagemAlerta");
}

function ocultarAlertaRelatorio(alerta) {
    if (alerta) {
        alerta.classList.add("d-none");
    }
}

function mostrarAlertaRelatorio(alerta, mensagem) {
    if (!alerta) {
        window.alert(mensagem);
        return;
    }

    alerta.className = "alert alert-danger";
    alerta.innerText = mensagem;
    alerta.classList.remove("d-none");
}
