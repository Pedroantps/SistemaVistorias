// URL base da sua API
const API_BASE_URL = 'http://localhost:5158';

document.addEventListener("DOMContentLoaded", () => {
    
    // ====================================================================
    // PARTE 1: AUTO-PREENCHIMENTO COM OS DADOS DA BUSCA
    // ====================================================================
    const dadosSalvos = localStorage.getItem("vistoriaEmAndamento");

    if (dadosSalvos) {
        try {
            // Converte os dados guardados em texto de volta para um objeto
            const ativo = JSON.parse(dadosSalvos);

            // Procura os campos no HTML (Certifique-se de que os IDs no seu vistoria.html são estes)
            const inputPatrimonio = document.getElementById('patrimonioAgevap');
            const selectContrato = document.getElementById('contratoGestao');

            if (inputPatrimonio) {
                // Preenche com o património (usa o AGEVAP ou o fallback do Órgão Gestor)
                inputPatrimonio.value = ativo.patrimonioAgevap || ativo.patrimonioOrgaoGestor;
                inputPatrimonio.readOnly = true; // Bloqueia para evitar que o utilizador altere sem querer
            }
            
            if (selectContrato) {
                selectContrato.value = ativo.contratoGestao;
                // Deixa o contrato desativado visualmente (opcional, mas recomendado)
                selectContrato.style.pointerEvents = 'none';
                selectContrato.style.backgroundColor = '#e9ecef';
            }
        } catch (e) {
            console.error("Erro ao analisar dados salvos no localStorage:", e);
        }

        // Limpa a memória para que uma vistoria futura ou avulsa não venha com dados antigos
        localStorage.removeItem("vistoriaEmAndamento");
    }

    // ====================================================================
    // PARTE 2: ENVIO DO FORMULÁRIO E DAS FOTOS PARA A API
    // ====================================================================
    const formVistoria = document.getElementById("formVistoria");
    
    if (formVistoria) {
        formVistoria.addEventListener("submit", async function(event) {
            // Impede a página de recarregar
            event.preventDefault();

            // Procura o botão de submissão e uma div de alerta para mostrar mensagens
            const btnSubmit = document.getElementById("btnSalvarVistoria") || formVistoria.querySelector('button[type="submit"]');
            const textoOriginal = btnSubmit.innerHTML;
            const alerta = document.getElementById("alertaVistoria"); // Se não tiver esta div, crie uma <div id="alertaVistoria"></div> no html
            
            // Estado de "A carregar"
            btnSubmit.disabled = true;
            btnSubmit.innerHTML = '<span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span> A guardar...';
            
            if(alerta) alerta.classList.add("d-none");

            // O FormData empacota automaticamente todos os inputs, incluindo os ficheiros das fotos
            const formData = new FormData(formVistoria);

            // (Hack) Se o select do contrato foi bloqueado, o FormData pode ignorá-lo. 
            // Vamos garantir que ele é enviado adicionando-o manualmente se faltar:
            if(!formData.has('contratoGestao')) {
                const selectContrato = document.getElementById('contratoGestao');
                if(selectContrato) formData.append('contratoGestao', selectContrato.value);
            }

            try {
                // Envia para o backend C# (A rota que criámos no VistoriasController)
                const resposta = await fetch(`${API_BASE_URL}/api/vistorias/registrar`, {
                    method: 'POST',
                    body: formData // NOTA: Nunca defina 'Content-Type' manualmente ao enviar ficheiros (o browser faz isso por si com o boundary correto)
                });

                const resultado = await resposta.json();

                if (resposta.ok) {
                    // SUCESSO
                    if(alerta) {
                        alerta.className = "alert alert-success mt-3";
                        alerta.innerHTML = `<i class="bi bi-check-circle-fill me-2"></i> ${resultado.mensagem}`;
                    } else {
                        alert(resultado.mensagem);
                    }
                    
                    formVistoria.reset();
                    
                    // Redireciona o utilizador de volta para o ecrã de busca após 2.5 segundos
                    setTimeout(() => {
                        window.location.href = "index.html";
                    }, 2500);

                } else {
                    // ERRO DO BACKEND (Ex: Ativo não encontrado)
                    if(alerta) {
                        alerta.className = "alert alert-danger mt-3";
                        alerta.innerHTML = `<i class="bi bi-exclamation-triangle-fill me-2"></i> <strong>Erro:</strong> ${resultado.mensagem}`;
                    } else {
                        alert("Erro: " + resultado.mensagem);
                    }
                }
            } catch (error) {
                // ERRO DE CONEXÃO (Servidor offline)
                console.error("Erro na requisição HTTP:", error);
                if(alerta) {
                    alerta.className = "alert alert-danger mt-3";
                    alerta.innerHTML = `<i class="bi bi-wifi-off me-2"></i> Falha na comunicação com o servidor. Verifique a ligação.`;
                } else {
                    alert("Erro de conexão com o servidor.");
                }
            } finally {
                // Restaura o botão caso o formulário não tenha redirecionado
                btnSubmit.disabled = false;
                btnSubmit.innerHTML = textoOriginal;
            }
        });
    }
});