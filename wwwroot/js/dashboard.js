let ativosDashboard = [];

/**
 * Executado quando a página do dashboard é carregada.
 * Busca os dados na API e monta os cards e tabelas.
 */
document.addEventListener("DOMContentLoaded", () => {
    document.getElementById("buscaTabela").addEventListener("input", renderizarTabela);
    document.getElementById("filtroContrato").addEventListener("change", renderizarTabela);
    document.getElementById("filtroStatus").addEventListener("change", renderizarTabela);
    document.getElementById("filtroOrigem").addEventListener("change", renderizarTabela);
    carregarDashboard();
});

/**
 * Busca os dados consolidados no backend e os renderiza na tela (tabelas e gráficos numéricos).
 * Centraliza o tratamento de erro em caso de falha de conexão e trava a UI.
 */
async function carregarDashboard() {
    const btnAtualizar = document.getElementById("btnAtualizar");
    if (btnAtualizar) {
        btnAtualizar.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span>Atualizando...';
        btnAtualizar.disabled = true;
    }
    const alerta = document.getElementById("dashboardAlerta");
    alerta.classList.add("d-none");

    try {
        const resposta = await fetch("/api/dashboard", {
            headers: obterHeadersAutenticados()
        });
        if (!resposta.ok) {
            throw new Error("Nao foi possivel carregar os dados do dashboard.");
        }

        const dados = await resposta.json();
        ativosDashboard = dados.ativos || [];

        renderizarResumo(dados.resumo || {});
        renderizarContratos(dados.porContrato || []);
        renderizarEstados(dados.porEstado || []);
        preencherFiltroContrato(dados.porContrato || []);
        renderizarTabela();
    } catch (error) {
        alerta.innerText = error.message || "Erro ao contactar o servidor. Verifique se a API esta a correr.";
        alerta.classList.remove("d-none");
        document.getElementById("tabelaAtivos").innerHTML = '<tr><td colspan="9" class="empty-state">Nao foi possivel carregar os dados.</td></tr>';
    } finally {
        if (btnAtualizar) {
            btnAtualizar.innerHTML = '<i class="bi bi-arrow-clockwise me-2"></i>Atualizar';
            btnAtualizar.disabled = false;
        }
    }
}

function renderizarResumo(resumo) {
    const percentual = Number(resumo.percentualVistoriado || 0);

    document.getElementById("totalItens").innerText = formatarNumero(resumo.total);
    document.getElementById("totalVistoriados").innerText = formatarNumero(resumo.vistoriados);
    document.getElementById("totalPendentes").innerText = formatarNumero(resumo.pendentes);
    document.getElementById("totalAlterados").innerText = formatarNumero(resumo.alteradosInservivel || 0);
    document.getElementById("percentualVistoriado").innerText = `${formatarDecimal(percentual)}%`;
    document.getElementById("barraConclusao").style.width = `${Math.min(percentual, 100)}%`;
}

function renderizarContratos(contratos) {
    const container = document.getElementById("contratosLista");
    const maiorTotal = Math.max(...contratos.map(item => item.total), 1);

    if (!contratos.length) {
        container.innerHTML = '<div class="empty-state">Nenhum contrato encontrado.</div>';
        return;
    }

    container.innerHTML = contratos.map(item => {
        const largura = Math.round((item.total / maiorTotal) * 100);
        return `
            <div class="bar-row">
                <div title="${escaparHtml(item.contrato)}">${escaparHtml(item.contrato)}</div>
                <div class="bar-track"><div class="bar-fill" style="width: ${largura}%"></div></div>
                <strong>${formatarNumero(item.total)}</strong>
            </div>
        `;
    }).join("");
}

function renderizarEstados(estados) {
    const container = document.getElementById("estadosLista");
    const maiorTotal = Math.max(...estados.map(item => item.total), 1);

    if (!estados.length) {
        container.innerHTML = '<div class="empty-state">Nenhum estado encontrado.</div>';
        return;
    }

    container.innerHTML = estados.map(item => {
        const largura = Math.round((item.total / maiorTotal) * 100);
        return `
            <div class="bar-row">
                <div title="${escaparHtml(item.estado)}">${escaparHtml(item.estado)}</div>
                <div class="bar-track"><div class="bar-fill" style="width: ${largura}%"></div></div>
                <strong>${formatarNumero(item.total)}</strong>
            </div>
        `;
    }).join("");
}

function preencherFiltroContrato(contratos) {
    const filtro = document.getElementById("filtroContrato");
    const valorAtual = filtro.value;

    filtro.innerHTML = '<option value="">Todos os contratos</option>';
    contratos.forEach(item => {
        const option = document.createElement("option");
        option.value = item.contrato;
        option.innerText = item.contrato;
        filtro.appendChild(option);
    });

    filtro.value = valorAtual;
}

function renderizarTabela() {
    const tbody = document.getElementById("tabelaAtivos");
    const busca = normalizarTexto(document.getElementById("buscaTabela").value);
    const contrato = document.getElementById("filtroContrato").value;
    const status = document.getElementById("filtroStatus").value;
    const origem = document.getElementById("filtroOrigem").value;

    const filtrados = ativosDashboard.filter(ativo => {
        const atendeContrato = !contrato || ativo.contratoGestao === contrato;
        const atendeStatus = !status
            || (status === "vistoriado" && ativo.vistoriado)
            || (status === "pendente" && !ativo.vistoriado);

        const atendeOrigem = !origem
            || (origem === "avulso" && ativo.isAvulso)
            || (origem === "importado" && !ativo.isAvulso);

        const textoBusca = normalizarTexto([
            ativo.patrimonioAgevap,
            ativo.patrimonioOrgaoGestor,
            ativo.contratoGestao,
            ativo.descricao,
            ativo.condicaoFuncional,
            ativo.instalacaoEndereco,
            ativo.novoEstadoConservacao,
            ativo.numeroLaudo
        ].join(" "));

        return atendeContrato && atendeStatus && atendeOrigem && (!busca || textoBusca.includes(busca));
    });

    document.getElementById("contadorTabela").innerText = `${formatarNumero(filtrados.length)} de ${formatarNumero(ativosDashboard.length)} itens`;

    if (!filtrados.length) {
        tbody.innerHTML = '<tr><td colspan="11" class="empty-state">Nenhum item encontrado com os filtros atuais.</td></tr>';
        return;
    }

    tbody.innerHTML = filtrados.map(ativo => `
        <tr>
            <td data-label="Patrimônio">
                <strong>${escaparHtml(ativo.patrimonioAgevap)}</strong>
                ${ativo.isAvulso ? '<span class="badge bg-warning text-dark ms-1" style="font-size: 0.65rem;" title="Cadastrado Manualmente">AVULSO</span>' : ''}
            </td>
            <td data-label="Contrato" class="cell-truncate" title="${escaparHtml(ativo.contratoGestao)}">${escaparHtml(ativo.contratoGestao)}</td>
            <td data-label="Descrição" class="cell-truncate" title="${escaparHtml(ativo.descricao)}">${escaparHtml(ativo.descricao)}</td>
            <td data-label="Condição">${escaparHtml(ativo.condicaoFuncional)}</td>
            <td data-label="Localização" class="cell-truncate" title="${escaparHtml(ativo.instalacaoEndereco)}">${escaparHtml(ativo.instalacaoEndereco)}</td>
            <td data-label="Status">${renderizarStatus(ativo.vistoriado)}</td>
            <td data-label="Novo Estado">${escaparHtml(ativo.novoEstadoConservacao || "-")}</td>
            <td data-label="Laudo">${escaparHtml(ativo.numeroLaudo || "-")}</td>
            <td data-label="Data">${formatarData(ativo.dataVistoria)}</td>
            <td data-label="Vistoriador">${escaparHtml(ativo.usuarioVistoriador || "-")}</td>
            <td data-label="Ações">
                <button class="btn btn-sm btn-outline-primary" onclick="editarVistoria('${escaparHtml(ativo.patrimonioAgevap)}', '${escaparHtml(ativo.contratoGestao)}')" title="Editar Vistoria">
                    <i class="bi bi-pencil-square me-2"></i> Editar
                </button>
            </td>
        </tr>
    `).join("");
}

function renderizarStatus(vistoriado) {
    if (vistoriado) {
        return '<span class="status-pill status-done"><i class="bi bi-check2-circle"></i>Vistoriado</span>';
    }

    return '<span class="status-pill status-open"><i class="bi bi-clock"></i>Pendente</span>';
}

function editarVistoria(patrimonio, contrato) {
    const ativo = ativosDashboard.find(a => a.patrimonioAgevap === patrimonio && a.contratoGestao === contrato);
    if (ativo) {
        localStorage.setItem("vistoriaEmAndamento", JSON.stringify(ativo));
        window.location.href = "/Home/Vistoria";
    }
}

function formatarNumero(valor) {
    return Number(valor || 0).toLocaleString("pt-BR");
}

function formatarDecimal(valor) {
    return Number(valor || 0).toLocaleString("pt-BR", {
        minimumFractionDigits: 0,
        maximumFractionDigits: 1
    });
}

function formatarData(valor) {
    if (!valor) {
        return "-";
    }

    return new Date(valor).toLocaleDateString("pt-BR");
}

function normalizarTexto(valor) {
    return String(valor || "")
        .normalize("NFD")
        .replace(/[\u0300-\u036f]/g, "")
        .toLowerCase();
}

function escaparHtml(valor) {
    return String(valor || "")
        .replaceAll("&", "&amp;")
        .replaceAll("<", "&lt;")
        .replaceAll(">", "&gt;")
        .replaceAll('"', "&quot;")
        .replaceAll("'", "&#039;");
}

/**
 * Executa a requisição DELETE para apagar o banco de dados. 
 * Esta função bloqueia o botão de confirmação e controla a visibilidade do modal do Bootstrap.
 */
async function limparBancoDeDados() {
    const btnConfirmar = document.getElementById("btnConfirmarLimpeza");
    btnConfirmar.disabled = true;
    btnConfirmar.innerHTML = '<span class="spinner-border spinner-border-sm me-1"></span>Limpando...';

    try {
        const resposta = await fetch("/api/importacao/limpar-banco", {
            method: "DELETE",
            headers: obterHeadersAutenticados()
        });

        if (!resposta.ok) {
            throw new Error("Erro ao limpar o banco de dados.");
        }

        // Fecha o modal
        try {
            const modalEl = document.getElementById("modalLimparBanco");
            const modal = bootstrap.Modal.getInstance(modalEl);
            if (modal) modal.hide();
        } catch(e) { console.warn("Aviso ao fechar modal:", e); }

        // Recarrega o dashboard
        carregarDashboard();

        // Feedback visual
        const alerta = document.getElementById("dashboardAlerta");
        alerta.className = "alert alert-success";
        alerta.innerText = "Banco de dados limpo com sucesso!";
        alerta.classList.remove("d-none");
        setTimeout(() => alerta.classList.add("d-none"), 4000);
    } catch (error) {
        const alerta = document.getElementById("dashboardAlerta");
        alerta.className = "alert alert-danger";
        alerta.innerText = error.message;
        alerta.classList.remove("d-none");
    } finally {
        btnConfirmar.disabled = false;
        btnConfirmar.innerHTML = '<i class="bi bi-trash3 me-1"></i>Sim, limpar tudo';
    }
}

function abrirModalLimpeza() {
    try {
        const modal = new bootstrap.Modal(document.getElementById('modalLimparBanco'));
        modal.show();
    } catch(e) {
        console.warn("Aviso ao abrir modal:", e);
    }
}

function abrirModalAlterados() {
    const alterados = ativosDashboard.filter(a => a.condicaoOriginal);
    const tbody = document.getElementById("tabelaAlterados");

    if (!alterados.length) {
        tbody.innerHTML = '<tr><td colspan="6" class="empty-state text-center py-4">Nenhum item encontrado.</td></tr>';
    } else {
        tbody.innerHTML = alterados.map(item => `
            <tr>
                <td data-label="Patrimônio" style="font-weight: 600;">${escaparHtml(item.patrimonioAgevap || item.patrimonioOrgaoGestor)}</td>
                <td data-label="Contrato"><span class="badge bg-light text-dark border">${escaparHtml(item.contratoGestao || "S/C")}</span></td>
                <td data-label="Descrição">
                    <div style="max-width: 250px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap;" title="${escaparHtml(item.descricao)}">
                        ${escaparHtml(item.descricao)}
                    </div>
                </td>
                <td data-label="Condição Orig."><span class="badge" style="background-color: #f1f5f9; color: #475569; border: 1px solid #cbd5e1;">${escaparHtml(item.condicaoOriginal)}</span></td>
                <td data-label="Condição Atual"><span class="badge" style="background-color: #fef2f2; color: #991b1b; border: 1px solid #fecaca;">${escaparHtml(item.condicaoFuncional)}</span></td>
                <td data-label="Vistoriador">${escaparHtml(item.usuarioVistoriador || "-")}</td>
            </tr>
        `).join("");
    }

    const modal = new bootstrap.Modal(document.getElementById('modalAlteradosInservivel'));
    modal.show();
}
