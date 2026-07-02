let ativosDashboard = [];

/**
 * Executado quando a página do dashboard é carregada.
 * Busca os dados na API e monta os cards e tabelas.
 */
document.addEventListener("DOMContentLoaded", () => {
    const buscaTabela = document.getElementById("buscaTabela");
    if (buscaTabela) {
        buscaTabela.addEventListener("input", renderizarTabela);
        document.getElementById("filtroContrato").addEventListener("change", renderizarTabela);
        document.getElementById("filtroStatus").addEventListener("change", renderizarTabela);
        document.getElementById("filtroOrigem").addEventListener("change", renderizarTabela);
        window.addEventListener("resize", ajustarTabelaNotebook);
        carregarDashboard();
    }
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
    document.getElementById("totalAlterados").innerText = formatarNumero(resumo.alterados || 0);
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
                <button class="btn btn-primary" onclick="editarVistoria('${escaparHtml(ativo.patrimonioAgevap)}', '${escaparHtml(ativo.contratoGestao)}')" title="Editar Vistoria">
                    <i class="bi bi-pencil-square"></i>
                </button>
            </td>
        </tr>
    `).join("");

    ajustarTabelaNotebook();
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
    const alterados = ativosDashboard.filter(a => a.condicaoOriginal || a.instalacaoOriginal || a.patrimonioOrgaoOriginal || (a.novoEstadoConservacao && a.novoEstadoConservacao !== a.condicaoFuncional));
    const tbody = document.getElementById("tabelaAlterados");

    if (!alterados.length) {
        tbody.innerHTML = '<tr><td colspan="7" class="empty-state text-center py-4">Nenhum item alterado na vistoria foi encontrado.</td></tr>';
    } else {
        tbody.innerHTML = alterados.map(item => {
            const renderMudanca = (original, atual, icone) => {
                if (original) {
                    return `
                        <div class="d-flex flex-column">
                            <del class="text-muted small mb-1" style="font-size: 0.75rem;"><i class="bi bi-arrow-return-right me-1"></i>${escaparHtml(original)}</del>
                            <span style="font-weight: 600; color: #1e293b;"><i class="bi ${icone} me-1 text-primary"></i>${escaparHtml(atual)}</span>
                        </div>
                    `;
                }
                return `<span style="font-weight: 500; color: #475569;">${escaparHtml(atual)}</span>`;
            };

            const patrimonioStr = renderMudanca(item.patrimonioOrgaoOriginal, item.patrimonioOrgaoGestor || item.patrimonioAgevap, "bi-box-seam");
            const instalacaoStr = renderMudanca(item.instalacaoOriginal, item.instalacaoEndereco, "bi-geo-alt");
            let condRealOriginal = item.condicaoOriginal || item.condicaoFuncional;
            let condOriginal = null;
            let condAtual = item.condicaoFuncional;
            
            if (item.novoEstadoConservacao && item.novoEstadoConservacao !== condRealOriginal) {
                condOriginal = condRealOriginal;
                condAtual = item.novoEstadoConservacao;
            } else if (item.condicaoOriginal && item.condicaoOriginal !== item.condicaoFuncional) {
                condOriginal = item.condicaoOriginal;
                condAtual = item.condicaoFuncional;
            }
            const condicaoStr = renderMudanca(condOriginal, condAtual, "bi-activity");

            return `
            <tr style="transition: all 0.2s;">
                <td class="px-3" data-label="Patrimônio AGEVAP" style="font-weight: 700; color: #1e293b;">
                    <i class="bi bi-box-seam me-2 text-primary" style="opacity: 0.8;"></i>${escaparHtml(item.patrimonioAgevap)}
                </td>
                <td class="px-3" data-label="Contrato">
                    <span class="badge" style="background-color: #e0f2fe; color: #0369a1; border: 1px solid #bae6fd; font-weight: 500;">
                        ${escaparHtml(item.contratoGestao || "S/C")}
                    </span>
                </td>
                <td class="px-3" data-label="Patrimônio Órgão Gestor">
                    ${patrimonioStr}
                </td>
                <td class="px-3" data-label="Descrição">
                    <div style="max-width: 200px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; color: #475569;" title="${escaparHtml(item.descricao)}">
                        ${escaparHtml(item.descricao)}
                    </div>
                </td>
                <td class="px-3" data-label="Instalação">
                    ${instalacaoStr}
                </td>
                <td class="px-3" data-label="Condição Funcional">
                    ${condicaoStr}
                </td>
                <td class="px-3" data-label="Vistoriador" style="color: #64748b; font-weight: 500;">
                    <i class="bi bi-person-badge me-2" style="opacity: 0.7;"></i>${escaparHtml(item.usuarioVistoriador || "-")}
                </td>
            </tr>
            `;
        }).join("");
    }

    const modal = new bootstrap.Modal(document.getElementById('modalAlteradosInservivel'));
    modal.show();
}

/**
 * Ajusta a tabela para caber em telas de notebook ou maiores, evitando scroll horizontal.
 * Aplica quebra de linhas, diminui a fonte e o padding das células.
 */
function ajustarTabelaNotebook() {
    const tableWrap = document.querySelector('.table-wrap');
    const table = tableWrap ? tableWrap.querySelector('.table') : null;
    
    if (!tableWrap || !table) return;

    if (window.innerWidth >= 1024) {
        table.style.fontSize = '0.75rem';
        table.style.whiteSpace = 'normal';
        table.style.wordWrap = 'break-word';
        table.style.tableLayout = 'auto';
        table.style.width = '100%';
        
        const cells = table.querySelectorAll('th, td');
        cells.forEach(cell => {
            cell.style.padding = '0.3rem 0.3rem';
            if (cell.classList.contains('cell-truncate')) {
                cell.style.whiteSpace = 'normal';
            }
        });

        tableWrap.style.overflowX = 'visible';
    } else {
        table.style.fontSize = '';
        table.style.whiteSpace = 'nowrap';
        table.style.wordWrap = '';
        table.style.tableLayout = '';
        table.style.width = '';
        
        const cells = table.querySelectorAll('th, td');
        cells.forEach(cell => {
            cell.style.padding = '';
            if (cell.classList.contains('cell-truncate')) {
                cell.style.whiteSpace = 'nowrap';
            }
        });

        tableWrap.style.overflowX = 'auto';
    }
}

async function exportarAlteradosExcel() {
    try {
        const response = await fetch('/api/Dashboard/ExportarAlterados', {
            method: 'GET',
            headers: obterHeadersAutenticados()
        });

        if (!response.ok) {
            const errorText = await response.text();
            console.error("Backend error response:", errorText);
            throw new Error(`Falha ao exportar excel: ${response.status} ${response.statusText}. Details: ${errorText}`);
        }

        const blob = await response.blob();
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.style.display = 'none';
        a.href = url;
        a.download = 'Bens_Alterados.xlsx';
        document.body.appendChild(a);
        a.click();
        window.URL.revokeObjectURL(url);
    } catch (error) {
        console.error("Erro ao exportar:", error);
        alert("Não foi possível gerar o relatório em Excel.");
    }
}
