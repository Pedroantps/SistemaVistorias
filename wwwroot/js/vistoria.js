/**
 * Carrega os dados da vistoria em andamento a partir do LocalStorage.
 * Executado ao carregar a página de vistoria (Vistoria.cshtml).
 */
document.addEventListener("DOMContentLoaded", async () => {
    
    // 1. Carregar instalações dinamicamente do banco
    const selectLocalizacao = document.getElementById('inputLocalizacao');
    if (selectLocalizacao) {
        try {
            const resInstalacoes = await fetch('/api/vistorias/instalacoes', { headers: window.obterHeadersAutenticados ? window.obterHeadersAutenticados() : {} });
            if (resInstalacoes.ok) {
                const instalacoes = await resInstalacoes.json();
                instalacoes.forEach(inst => {
                    const opt = document.createElement('option');
                    opt.value = inst;
                    opt.textContent = inst;
                    selectLocalizacao.appendChild(opt);
                });
            }
        } catch (e) {
            console.warn("Erro ao buscar instalações", e);
        }
    }

    /**
     * Recupera os dados parciais de uma vistoria do LocalStorage e hidrata o formulário de preenchimento.
     */
    const dadosSalvos = localStorage.getItem("vistoriaEmAndamento");

    if (dadosSalvos) {
        const lblPatrimonio = document.getElementById('lblPatrimonio');
        if (!lblPatrimonio) return;
        try {
            const ativo = JSON.parse(dadosSalvos);

            // Preenche os campos de visualização (os SPANs do painel)
            document.getElementById('lblPatrimonio').innerText = ativo.patrimonioAgevap || ativo.patrimonioOrgaoGestor || "N/A";
            document.getElementById('lblContrato').innerText = ativo.contratoGestao || "N/A";

            // Preenche os campos do formulário
            document.getElementById('inputDescricao').value = ativo.descricao || "";
            document.getElementById('inputLocalizacao').value = ativo.instalacaoEndereco || "";
            document.getElementById('inputInea').value = ativo.patrimonioOrgaoGestor || "";

            // Preenche os campos ocultos do formulário (Para enviar ao C#)
            document.getElementById('hiddenPatrimonio').value = ativo.patrimonioAgevap || ativo.patrimonioOrgaoGestor;
            document.getElementById('hiddenContrato').value = ativo.contratoGestao;

            if (ativo.isNovo) {
                // Habilita edição
                document.getElementById('inputDescricao').removeAttribute('readonly');
                
                // Torna obrigatório
                document.getElementById('inputDescricao').required = true;
                document.getElementById('inputLocalizacao').required = true;
            }

            // Se veio da busca como "não-inservível", preserva a condição antiga
            if (ativo.marcarInservivel) {
                document.getElementById('hiddenCondicao').value = ativo.condicaoFuncional || "";

                // Mostra alerta informativo
                const alerta = document.getElementById('mensagemAlerta');
                alerta.className = "alert alert-warning mt-4";
                alerta.innerHTML = `<i class="bi bi-exclamation-triangle-fill me-2"></i><strong>Atenção:</strong> Este bem constava como <strong>${ativo.condicaoFuncional || "Regular"}</strong> na base de dados. Registre o Novo Estado de Conservação abaixo.`;
                alerta.classList.remove("d-none");
            } else {
                document.getElementById('hiddenCondicao').value = ativo.condicaoFuncional || "";
            }

            // Preenche os campos do formulário se já existir vistoria
            if (ativo.vistoriado) {
                if (ativo.novoEstadoConservacao) {
                    document.getElementById('novoEstado').value = ativo.novoEstadoConservacao;
                }
                if (ativo.numeroLaudo) {
                    document.getElementById('numeroLaudo').value = ativo.numeroLaudo;
                }

                // Remove a obrigatoriedade das fotos, pois o usuário pode não querer enviá-las de novo
                document.getElementById('fotoEsquerda').removeAttribute('required');
                document.getElementById('fotoDireita').removeAttribute('required');
                document.getElementById('fotoFrontal').removeAttribute('required');
                document.getElementById('fotoEtiqueta').removeAttribute('required');

                // Carrega as fotos existentes
                if (ativo.caminhoFotos) {
                    const fotos = ativo.caminhoFotos.split(';');
                    
                    fotos.forEach(fotoPath => {
                        const baseUrl = '/fotos_vistorias/';
                        const imageUrl = baseUrl + fotoPath;
                        
                        // Determinar qual div de preview atualizar com base no nome do arquivo
                        let inputId = null;
                        if (fotoPath.includes('Esquerda')) inputId = 'fotoEsquerda';
                        else if (fotoPath.includes('Direita')) inputId = 'fotoDireita';
                        else if (fotoPath.includes('Frontal')) inputId = 'fotoFrontal';
                        else if (fotoPath.includes('Etiqueta')) inputId = 'fotoEtiqueta';
                        else return;

                        const previewDiv = document.querySelector(`.photo-preview[data-input="${inputId}"]`);
                        if (previewDiv) {
                            previewDiv.innerHTML = `<img src="${imageUrl}" class="preview-image" alt="Preview da Vistoria" style="width: 100%; height: 100%; object-fit: cover; border-radius: 10px;">`;
                        }
                    });
                }
            }

        } catch (e) {
            console.error("Erro ao analisar dados salvos no localStorage:", e);
        }

        // Remove os dados para não bugar vistorias futuras
        localStorage.removeItem("vistoriaEmAndamento");
    }

    /**
     * Vincula eventos de clique aos componentes visuais (previews) 
     * para abrir o seletor nativo de arquivos do sistema.
     */
    const photoPreviews = document.querySelectorAll('.photo-preview');

    photoPreviews.forEach(preview => {
        // Ao clicar no quadrado, aciona o input de arquivo oculto
        preview.addEventListener('click', () => {
            const inputId = preview.getAttribute('data-input');
            document.getElementById(inputId).click();
        });
    });

    // Ao selecionar a foto, mostra a imagem dentro do quadrado
    const fileInputs = document.querySelectorAll('input[type="file"]');
    fileInputs.forEach(input => {
        input.addEventListener('change', function() {
            if (this.files && this.files[0]) {
                const reader = new FileReader();
                const previewDiv = document.querySelector(`.photo-preview[data-input="${this.id}"]`);
                
                reader.onload = function(e) {
                    previewDiv.innerHTML = `<img src="${e.target.result}" class="preview-image" alt="Preview da Vistoria" style="width: 100%; height: 100%; object-fit: cover; border-radius: 10px;">`;
                }
                
                reader.readAsDataURL(this.files[0]);
            }
        });
    });

    /**
     * Intercepta o envio do formulário, empacota os dados via FormData (incluindo binários) 
     * e os envia ao backend, lidando com os estados de carregamento (UI feedback).
     */
    const formVistoria = document.getElementById("formVistoria");
    
    if (formVistoria) {
        formVistoria.addEventListener("submit", async function(event) {
            event.preventDefault();

            const btnSubmit = document.getElementById("btnSalvar");
            const textoOriginal = btnSubmit.innerHTML;
            const alerta = document.getElementById("mensagemAlerta");
            
            // Estado de "Carregando"
            btnSubmit.disabled = true;
            btnSubmit.innerHTML = '<span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span> A salvar...';
            alerta.classList.add("d-none");

            // Empacota tudo (dados ocultos + texto + fotos)
            const formData = new FormData(formVistoria);

            try {
                const resposta = await fetch(`/api/vistorias/registrar`, {
                    method: 'POST',
                    headers: obterHeadersAutenticados(),
                    body: formData 
                });

                const resultado = await resposta.json();

                if (resposta.ok) {
                    alerta.className = "alert alert-success mt-4";
                    alerta.innerHTML = `<i class="bi bi-check-circle-fill me-2"></i> ${resultado.mensagem}`;
                    
                    formVistoria.reset();
                    
                    // Volta pra tela inicial após sucesso
                    setTimeout(() => {
                        window.location.href = "/Home/Index";
                    }, 2500);

                } else {
                    alerta.className = "alert alert-danger mt-4";
                    alerta.innerHTML = `<i class="bi bi-exclamation-triangle-fill me-2"></i> <strong>Erro:</strong> ${resultado.mensagem}`;
                }
            } catch (error) {
                console.error("Erro na requisição:", error);
                alerta.className = "alert alert-danger mt-4";
                alerta.innerHTML = `<i class="bi bi-wifi-off me-2"></i> Falha na comunicação com o servidor.`;
            } finally {
                btnSubmit.disabled = false;
                btnSubmit.innerHTML = textoOriginal;
            }
        });
    }
});