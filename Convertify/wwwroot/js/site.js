document.addEventListener('DOMContentLoaded', () => {
    const form = document.getElementById('convertForm');
    if (!form) return;

    const formats = JSON.parse(form.dataset.formats);
    const maxBytes = parseInt(form.dataset.maxMb, 10) * 1024 * 1024;

    const dropZone = document.getElementById('dropZone');
    const fileInput = document.getElementById('fileInput');
    const fileInfo = document.getElementById('fileInfo');
    const fileName = document.getElementById('fileName');
    const fileSize = document.getElementById('fileSize');
    const outputSelect = document.getElementById('outputFormat');
    const convertButton = document.getElementById('convertButton');
    const progress = document.getElementById('progress');
    const clientError = document.getElementById('clientError');

    function formatSize(bytes) {
        if (bytes < 1024) return bytes + ' B';
        if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB';
        return (bytes / (1024 * 1024)).toFixed(1) + ' MB';
    }

    function showError(message) {
        clientError.textContent = message;
        clientError.hidden = false;
    }

    function clearError() {
        clientError.hidden = true;
        clientError.textContent = '';
    }

    function resetSelection() {
        fileInput.value = '';
        fileInfo.hidden = true;
        outputSelect.innerHTML = '<option value="">Selecciona un archivo primero</option>';
        outputSelect.disabled = true;
        convertButton.disabled = true;
    }

    function handleFile(file) {
        clearError();

        const dot = file.name.lastIndexOf('.');
        const extension = dot >= 0 ? file.name.substring(dot).toLowerCase() : '';
        const outputs = formats[extension];

        if (!outputs) {
            resetSelection();
            showError('Tipo de archivo no soportado.');
            return;
        }

        if (file.size > maxBytes) {
            resetSelection();
            showError('El archivo supera el tamaño máximo de ' + (maxBytes / 1024 / 1024) + ' MB.');
            return;
        }

        fileName.textContent = file.name;
        fileSize.textContent = '(' + formatSize(file.size) + ')';
        fileInfo.hidden = false;

        outputSelect.innerHTML = '';
        outputs.forEach(output => {
            const option = document.createElement('option');
            option.value = output;
            option.textContent = output.toUpperCase();
            outputSelect.appendChild(option);
        });

        outputSelect.disabled = false;
        convertButton.disabled = false;
    }

    // Seleccionar archivo (clic o teclado sobre la zona).
    dropZone.addEventListener('click', () => fileInput.click());
    dropZone.addEventListener('keydown', (e) => {
        if (e.key === 'Enter' || e.key === ' ') {
            e.preventDefault();
            fileInput.click();
        }
    });

    fileInput.addEventListener('change', () => {
        if (fileInput.files.length) {
            handleFile(fileInput.files[0]);
        } else {
            resetSelection();
        }
    });

    // Drag & Drop básico (se mejora en la Etapa 13).
    ['dragenter', 'dragover'].forEach(type => {
        dropZone.addEventListener(type, (e) => {
            e.preventDefault();
            dropZone.classList.add('dragover');
        });
    });

    ['dragleave', 'drop'].forEach(type => {
        dropZone.addEventListener(type, (e) => {
            e.preventDefault();
            dropZone.classList.remove('dragover');
        });
    });

    dropZone.addEventListener('drop', (e) => {
        const file = e.dataTransfer.files[0];
        if (!file) return;

        const transfer = new DataTransfer();
        transfer.items.add(file);
        fileInput.files = transfer.files;
        handleFile(file);
    });

    // Enviar formulario.
    form.addEventListener('submit', (e) => {
        if (!fileInput.files.length) {
            e.preventDefault();
            showError('Selecciona un archivo.');
            return;
        }

        clearError();
        convertButton.disabled = true;
        progress.hidden = false;
    });

    // Si el usuario regresa con el botón "Atrás", se restablece el estado.
    window.addEventListener('pageshow', (e) => {
        if (e.persisted) {
            progress.hidden = true;
            convertButton.disabled = !fileInput.files.length;
        }
    });
});