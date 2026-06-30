const API_BASE_URL = 'http://localhost:5158';

document.addEventListener("DOMContentLoaded", () => {
    
    // ====================================================================
    // PARTE 1: AUTO-PREENCHIMENTO COM OS DADOS DA BUSCA
    // ====================================================================
    const dadosSalvos = localStorage.getItem("vistoriaEmAndamento");

    if (dadosSalvos) {
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
                document.getElementById('inputLocalizacao').removeAttribute('readonly');
                document.getElementById('inputInea').removeAttribute('readonly');
                
                // Torna obrigatório
                document.getElementById('inputDescricao').required = true;
                document.getElementById('inputLocalizacao').required = true;
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
                        const baseUrl = 'http://localhost:5158/fotos_vistorias/';
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

    // ====================================================================
    // PARTE 2: LÓGICA DOS QUADRADOS DE FOTO (CÂMERA/PREVIEW)
    // ====================================================================
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

    // ====================================================================
    // PARTE 3: ENVIO DO FORMULÁRIO PARA A API
    // ====================================================================
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
                const resposta = await fetch(`${API_BASE_URL}/api/vistorias/registrar`, {
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