/**
 * Lê o input type="file" e exibe o nome do arquivo selecionado na tela de Importação.
 */
function mostrarNomeArquivo() {
    const input = document.getElementById('arquivoExcel');
    const display = document.getElementById('nomeArquivoSelecionado');
    const btnImportar = document.getElementById('btnImportar');

    if (input.files.length > 0) {
        display.innerHTML = `<i class="bi bi-file-earmark-spreadsheet me-2"></i> Arquivo selecionado: <strong>${input.files[0].name}</strong>`;
        display.classList.remove('d-none');
        btnImportar.disabled = false;
    } else {
        display.classList.add('d-none');
        btnImportar.disabled = true;
    }
}

// Lida com o envio do formulário
const formImportacao = document.getElementById('formImportacao');
if (formImportacao) {
    formImportacao.addEventListener('submit', async function(event) {
        event.preventDefault();
    
    const fileInput = document.getElementById('arquivoExcel');
    const file = fileInput.files[0];
    if (!file) return;

    // Prepara os elementos visuais de carregamento
    const btn = document.getElementById('btnImportar');
    const spinner = document.getElementById('spinnerImportar');
    const icone = document.getElementById('iconeBotao');
    const alerta = document.getElementById('alertImportacao');

    // Estado "Carregando..."
    btn.disabled = true;
    spinner.classList.remove('d-none');
    icone.classList.add('d-none');
    alerta.classList.add('d-none');
    
    // Prepara o arquivo para envio (Upload Multipart)
    const formData = new FormData();
    formData.append('arquivo', file);

    try {
        const response = await fetch('/api/importacao/upload', {
            method: 'POST',
            headers: obterHeadersAutenticados(),
            body: formData
        });

        const data = await response.json();

        alerta.classList.remove('d-none', 'alert-danger', 'alert-success');
        
        if (response.ok) {
            alerta.classList.add('alert-success');
            alerta.innerHTML = `
                <h5 class="alert-heading"><i class="bi bi-check-circle-fill me-2"></i>Sucesso!</h5>
                <p class="mb-0">${data.mensagem}</p>
            `;
            // Limpa o input
            fileInput.value = '';
            document.getElementById('nomeArquivoSelecionado').classList.add('d-none');
        } else {
            alerta.classList.add('alert-danger');
            alerta.innerHTML = `<i class="bi bi-exclamation-triangle-fill me-2"></i><strong>Erro:</strong> ${data.mensagem}`;
        }

    } catch (error) {
        console.error('Erro de requisição:', error);
        alerta.classList.remove('d-none', 'alert-danger', 'alert-success');
        alerta.classList.add('alert-danger');
        alerta.innerHTML = `<i class="bi bi-wifi-off me-2"></i>Erro ao conectar com o servidor. Verifique se o backend está rodando.`;
    } finally {
        // Restaura o botão
        btn.disabled = true; // Mantém desativado até escolherem outro arquivo
        spinner.classList.add('d-none');
        icone.classList.remove('d-none');
    }
    });
}