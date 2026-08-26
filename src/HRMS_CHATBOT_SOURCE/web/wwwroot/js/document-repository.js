(function () {
    'use strict';

    const PAGE_SIZE = 10;

    const endpoints = {
        statistics: '/Admin/DocumentRepository/GetStatistics',
        list: '/Admin/DocumentRepository/GetList',
        bulkUpload: '/Admin/DocumentRepository/BulkUpload',
        updateActive: '/Admin/DocumentRepository/UpdateActive',
        ingestDocument: '/Admin/DocumentRepository/IngestDocument'
    };

    const elements = {
        status: document.getElementById('docRepoStatus'),
        statTotalDocuments: document.getElementById('statTotalDocuments'),
        statPolicyDocuments: document.getElementById('statPolicyDocuments'),
        statTrainingDocuments: document.getElementById('statTrainingDocuments'),
        statActiveDocuments: document.getElementById('statActiveDocuments'),
        statInactiveDocuments: document.getElementById('statInactiveDocuments'),
        docTypeTabs: document.querySelectorAll('.doc-type-tab'),
        docTableTitle: document.getElementById('docTableTitle'),
        docTableSubtitle: document.getElementById('docTableSubtitle'),
        bulkUploadBtn: document.getElementById('bulkUploadBtn'),
        startIngestionBtn: document.getElementById('startIngestionBtn'),
        selectAllDocuments: document.getElementById('selectAllDocuments'),
        bulkFileInput: document.getElementById('bulkFileInput'),
        ingestionProgressPanel: document.getElementById('ingestionProgressPanel'),
        ingestionProgressTitle: document.getElementById('ingestionProgressTitle'),
        ingestionProgressCount: document.getElementById('ingestionProgressCount'),
        ingestionProgressFill: document.getElementById('ingestionProgressFill'),
        ingestionProgressMessage: document.getElementById('ingestionProgressMessage'),
        uploadProgressPanel: document.getElementById('uploadProgressPanel'),
        uploadProgressTitle: document.getElementById('uploadProgressTitle'),
        uploadProgressCount: document.getElementById('uploadProgressCount'),
        uploadProgressFill: document.getElementById('uploadProgressFill'),
        uploadProgressMessage: document.getElementById('uploadProgressMessage'),
        documentSearch: document.getElementById('documentSearch'),
        documentCountBadge: document.getElementById('documentCountBadge'),
        refreshDocumentsBtn: document.getElementById('refreshDocumentsBtn'),
        documentsTableBody: document.getElementById('documentsTableBody'),
        docPagination: document.getElementById('docPagination'),
        paginationInfo: document.getElementById('paginationInfo'),
        paginationPages: document.getElementById('paginationPages'),
        prevPageBtn: document.getElementById('prevPageBtn'),
        nextPageBtn: document.getElementById('nextPageBtn'),
        uploadModal: document.getElementById('uploadModal'),
        uploadModalBackdrop: document.getElementById('uploadModalBackdrop'),
        uploadModalList: document.getElementById('uploadModalList'),
        uploadModalSubtitle: document.getElementById('uploadModalSubtitle'),
        closeUploadModal: document.getElementById('closeUploadModal'),
        cancelUploadModal: document.getElementById('cancelUploadModal'),
        confirmUploadModal: document.getElementById('confirmUploadModal')
    };

    let searchTimer = null;
    let isUploading = false;
    let isIngesting = false;
    let activeCategory = 'Policy';
    let pendingUploadItems = [];

    const paginationState = {
        Policy: { page: 1, totalCount: 0, totalPages: 0 },
        Training: { page: 1, totalCount: 0, totalPages: 0 }
    };

    const categoryLabels = {
        Policy: {
            title: 'Policy Documents',
            subtitle: 'Uploaded policy files stored in Azure blob storage',
            empty: 'No policy documents yet. Use bulk upload to add policy files.'
        },
        Training: {
            title: 'Training Materials',
            subtitle: 'Uploaded training files stored in Azure blob storage',
            empty: 'No training materials yet. Use bulk upload to add training files.'
        }
    };

    function init() {
        if (!elements.documentsTableBody) {
            return;
        }

        bindEvents();
        refreshPage();
    }

    function bindEvents() {
        elements.bulkUploadBtn?.addEventListener('click', function () {
            elements.bulkFileInput?.click();
        });

        elements.startIngestionBtn?.addEventListener('click', function () {
            void startSelectedIngestion();
        });

        elements.selectAllDocuments?.addEventListener('change', function () {
            const checked = elements.selectAllDocuments.checked;
            elements.documentsTableBody?.querySelectorAll('.doc-select-checkbox').forEach(function (input) {
                if (!input.disabled) {
                    input.checked = checked;
                }
            });
            updateIngestionButtonState();
        });

        elements.bulkFileInput?.addEventListener('change', function () {
            if (elements.bulkFileInput.files?.length) {
                openUploadModal(Array.from(elements.bulkFileInput.files));
            }
        });

        elements.docTypeTabs?.forEach(function (tab) {
            tab.addEventListener('click', function () {
                setActiveCategory(tab.getAttribute('data-category') || 'Policy');
            });
        });

        elements.refreshDocumentsBtn?.addEventListener('click', refreshPage);

        elements.documentSearch?.addEventListener('input', function () {
            window.clearTimeout(searchTimer);
            searchTimer = window.setTimeout(function () {
                paginationState[activeCategory].page = 1;
                refreshDocuments();
            }, 300);
        });

        elements.prevPageBtn?.addEventListener('click', function () {
            goToPage(getCurrentPage() - 1);
        });

        elements.nextPageBtn?.addEventListener('click', function () {
            goToPage(getCurrentPage() + 1);
        });

        elements.closeUploadModal?.addEventListener('click', closeUploadModal);
        elements.cancelUploadModal?.addEventListener('click', closeUploadModal);
        elements.uploadModalBackdrop?.addEventListener('click', closeUploadModal);
        elements.confirmUploadModal?.addEventListener('click', function () {
            void confirmUploadFromModal();
        });

        document.addEventListener('keydown', function (event) {
            if (event.key === 'Escape' && elements.uploadModal && !elements.uploadModal.hidden) {
                closeUploadModal();
            }
        });
    }

    function getCurrentPage() {
        return paginationState[activeCategory]?.page || 1;
    }

    function goToPage(pageNumber) {
        const state = paginationState[activeCategory];
        if (pageNumber < 1 || pageNumber > state.totalPages || pageNumber === state.page) {
            return;
        }

        state.page = pageNumber;
        refreshDocuments();
    }

    function setActiveCategory(category) {
        activeCategory = category === 'Training' ? 'Training' : 'Policy';
        const labels = categoryLabels[activeCategory];

        elements.docTypeTabs?.forEach(function (tab) {
            const isActive = tab.getAttribute('data-category') === activeCategory;
            tab.classList.toggle('active', isActive);
            tab.setAttribute('aria-selected', isActive ? 'true' : 'false');
        });

        if (elements.docTableTitle) {
            elements.docTableTitle.textContent = labels.title;
        }

        if (elements.docTableSubtitle) {
            elements.docTableSubtitle.textContent = labels.subtitle;
        }

        refreshDocuments();
    }

    async function refreshPage() {
        await Promise.all([loadStatistics(), refreshDocuments()]);
    }

    async function loadStatistics() {
        try {
            const response = await fetch(endpoints.statistics, {
                headers: { Accept: 'application/json' }
            });
            const data = await response.json();
            if (!response.ok) {
                throw new Error(data.error_message || 'Unable to load document statistics.');
            }

            elements.statTotalDocuments.textContent = data.total_documents ?? 0;
            elements.statPolicyDocuments.textContent = data.policy_documents ?? 0;
            elements.statTrainingDocuments.textContent = data.training_documents ?? 0;
            elements.statActiveDocuments.textContent = data.active_documents ?? 0;
            elements.statInactiveDocuments.textContent = data.inactive_documents ?? 0;

            if (elements.status) {
                elements.status.textContent = (data.total_documents ?? 0) > 0
                    ? 'Document repository is ready.'
                    : 'Ready to ingest new documents.';
            }
        } catch (error) {
            if (elements.status) {
                elements.status.textContent = error.message || 'Unable to load statistics.';
            }
        }
    }

    async function refreshDocuments() {
        const params = new URLSearchParams();
        const searchText = elements.documentSearch?.value?.trim() || '';
        const pageNumber = getCurrentPage();

        params.set('category', activeCategory);
        params.set('page_number', String(pageNumber));
        params.set('page_size', String(PAGE_SIZE));

        if (searchText) {
            params.set('search_text', searchText);
        }

        try {
            const response = await fetch(`${endpoints.list}?${params.toString()}`, {
                headers: { Accept: 'application/json' }
            });
            const data = await response.json();
            if (!response.ok) {
                throw new Error(data.error_message || 'Unable to load documents.');
            }

            const items = Array.isArray(data.items) ? data.items : [];
            const totalCount = data.total_count ?? items.length;
            const totalPages = data.total_pages ?? 0;
            const currentPage = data.page_number ?? pageNumber;

            paginationState[activeCategory].page = currentPage;
            paginationState[activeCategory].totalCount = totalCount;
            paginationState[activeCategory].totalPages = totalPages;

            renderDocuments(items, totalCount, currentPage);
            renderPagination(currentPage, totalPages, totalCount, items.length);
        } catch (error) {
            renderDocuments([], 0, 1);
            renderPagination(1, 0, 0, 0);
            if (elements.status) {
                elements.status.textContent = error.message || 'Unable to load documents.';
            }
        }
    }

    function renderDocuments(documents, totalCount, currentPage) {
        if (elements.documentCountBadge) {
            elements.documentCountBadge.textContent = String(totalCount);
        }

        if (!documents.length) {
            const emptyMessage = categoryLabels[activeCategory]?.empty
                || 'Use bulk upload to add documents.';

            elements.documentsTableBody.innerHTML = `
                <tr class="doc-empty-row">
                    <td colspan="9">
                        <div class="empty-state compact">
                            <div class="empty-state-icon">
                                <iconify-icon icon="ph:folder-open-duotone"></iconify-icon>
                            </div>
                            <h4>No documents yet</h4>
                            <p>${escapeHtml(emptyMessage)}</p>
                        </div>
                    </td>
                </tr>`;
            return;
        }

        elements.documentsTableBody.innerHTML = documents.map(function (doc) {
            const categoryClass = (doc.dm_category || '').toLowerCase() === 'training' ? 'training' : 'policy';
            const isActive = String(doc.dm_active || '').toUpperCase() === 'Y';
            const ingestionStatus = (doc.dm_ingestion_status || 'Pending').trim();
            const ingestionClass = getIngestionStatusClass(ingestionStatus);
            const canSelect = isActive && ingestionStatus.toLowerCase() !== 'processing';
            const createdDate = doc.dm_created_date
                ? new Date(doc.dm_created_date).toLocaleString()
                : '—';

            return `
                <tr data-document-row="${doc.dm_id}">
                    <td class="doc-select-col">
                        <label class="doc-select-checkbox-wrap" title="Select for ingestion">
                            <input type="checkbox" class="doc-select-checkbox" data-select-id="${doc.dm_id}" ${canSelect ? '' : 'disabled'} />
                        </label>
                    </td>
                    <td>
                        <label class="doc-toggle">
                            <input type="checkbox" class="doc-active-toggle" data-document-id="${doc.dm_id}" ${isActive ? 'checked' : ''} />
                            <span class="doc-toggle-slider"></span>
                        </label>
                    </td>
                    <td><span class="doc-id-badge">${doc.dm_id}</span></td>
                    <td>${escapeHtml(doc.dm_name || '—')}</td>
                    <td><span class="doc-category-badge ${categoryClass}">${escapeHtml(doc.dm_category || '—')}</span></td>
                    <td class="doc-ingestion-cell">
                        <div class="doc-ingestion-status">
                            <span class="doc-ingestion-badge ${ingestionClass}" data-ingestion-badge="${doc.dm_id}">${escapeHtml(ingestionStatus)}</span>
                            ${doc.dm_chunk_count ? `<span class="doc-chunk-count">${doc.dm_chunk_count} chunks</span>` : ''}
                        </div>
                        <div class="doc-ingest-progress" data-ingest-progress="${doc.dm_id}" hidden>
                            <div class="doc-ingest-progress-bar">
                                <div class="doc-ingest-progress-fill" data-ingest-fill="${doc.dm_id}"></div>
                            </div>
                            <span class="doc-ingest-progress-text" data-ingest-text="${doc.dm_id}">0%</span>
                        </div>
                        ${doc.dm_ingestion_error ? `<span class="doc-ingestion-error" title="${escapeHtml(doc.dm_ingestion_error)}">${escapeHtml(doc.dm_ingestion_error)}</span>` : ''}
                    </td>
                    <td><span class="doc-path" title="${escapeHtml(doc.dm_path || '')}">${escapeHtml(doc.dm_path || '—')}</span></td>
                    <td>${escapeHtml(doc.dm_created_by || '—')}</td>
                    <td>${createdDate}</td>
                </tr>`;
        }).join('');

        elements.documentsTableBody.querySelectorAll('.doc-active-toggle').forEach(function (input) {
            input.addEventListener('change', function () {
                void toggleActive(input);
            });
        });

        elements.documentsTableBody.querySelectorAll('.doc-select-checkbox').forEach(function (input) {
            input.addEventListener('change', updateIngestionButtonState);
        });

        if (elements.selectAllDocuments) {
            elements.selectAllDocuments.checked = false;
        }

        updateIngestionButtonState();
    }

    function getIngestionStatusClass(status) {
        switch ((status || '').toLowerCase()) {
            case 'completed':
                return 'completed';
            case 'processing':
                return 'processing';
            case 'failed':
                return 'failed';
            case 'skipped':
                return 'skipped';
            default:
                return 'pending';
        }
    }

    function getSelectedDocumentIds() {
        return Array.from(elements.documentsTableBody?.querySelectorAll('.doc-select-checkbox:checked') || [])
            .map(function (input) { return Number(input.getAttribute('data-select-id')); })
            .filter(function (id) { return id > 0; });
    }

    function updateIngestionButtonState() {
        if (!elements.startIngestionBtn) {
            return;
        }

        const selectedCount = getSelectedDocumentIds().length;
        elements.startIngestionBtn.disabled = isIngesting || selectedCount === 0;
        elements.startIngestionBtn.querySelector('span').textContent = selectedCount > 0
            ? `Start Ingestion (${selectedCount})`
            : 'Start Ingestion';
    }

    function setRowIngestionProgress(documentId, percent, message, showBar) {
        const progressWrap = document.querySelector(`[data-ingest-progress="${documentId}"]`);
        const progressFill = document.querySelector(`[data-ingest-fill="${documentId}"]`);
        const progressText = document.querySelector(`[data-ingest-text="${documentId}"]`);
        const badge = document.querySelector(`[data-ingestion-badge="${documentId}"]`);

        if (progressWrap) {
            progressWrap.hidden = !showBar;
        }

        if (progressFill) {
            progressFill.style.width = `${Math.max(0, Math.min(100, percent))}%`;
        }

        if (progressText) {
            progressText.textContent = message || `${Math.round(percent)}%`;
        }

        if (badge && showBar) {
            badge.textContent = 'Processing';
            badge.className = 'doc-ingestion-badge processing';
        }
    }

    function setRowIngestionResult(documentId, status, errorMessage) {
        const progressWrap = document.querySelector(`[data-ingest-progress="${documentId}"]`);
        const progressFill = document.querySelector(`[data-ingest-fill="${documentId}"]`);
        const progressText = document.querySelector(`[data-ingest-text="${documentId}"]`);
        const badge = document.querySelector(`[data-ingestion-badge="${documentId}"]`);
        const statusClass = getIngestionStatusClass(status);
        const isSuccess = statusClass === 'completed';

        if (progressWrap) {
            progressWrap.hidden = false;
        }

        if (progressFill) {
            progressFill.style.width = isSuccess ? '100%' : '100%';
            progressFill.classList.toggle('failed', statusClass === 'failed');
        }

        if (progressText) {
            progressText.textContent = isSuccess ? '100%' : (errorMessage || status);
        }

        if (badge) {
            badge.textContent = status;
            badge.className = `doc-ingestion-badge ${statusClass}`;
        }
    }

    async function startSelectedIngestion() {
        const documentIds = getSelectedDocumentIds();
        if (isIngesting || !documentIds.length) {
            return;
        }

        isIngesting = true;
        updateIngestionButtonState();

        let completed = 0;
        let successCount = 0;
        let failedCount = 0;

        showIngestionProgress(0, documentIds.length, 'Starting ingestion...');

        for (const documentId of documentIds) {
            const selectCheckbox = document.querySelector(`.doc-select-checkbox[data-select-id="${documentId}"]`);
            if (selectCheckbox) {
                selectCheckbox.checked = false;
                selectCheckbox.disabled = true;
            }

            setRowIngestionProgress(documentId, 5, 'Starting...', true);

            let simulatedProgress = 5;
            const progressTimer = window.setInterval(function () {
                simulatedProgress = Math.min(simulatedProgress + 4, 90);
                setRowIngestionProgress(documentId, simulatedProgress, `${simulatedProgress}%`, true);
            }, 400);

            try {
                const response = await fetch(endpoints.ingestDocument, {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        Accept: 'application/json'
                    },
                    body: JSON.stringify({ document_id: documentId })
                });

                const data = await response.json();
                window.clearInterval(progressTimer);

                if (!response.ok) {
                    throw new Error(data.error_message || 'Ingestion failed.');
                }

                const status = data.status || 'Completed';
                if (String(status).toLowerCase() === 'completed') {
                    successCount++;
                    setRowIngestionResult(documentId, status, null);
                } else if (String(status).toLowerCase() === 'skipped') {
                    setRowIngestionResult(documentId, status, data.error_message);
                } else {
                    failedCount++;
                    setRowIngestionResult(documentId, status, data.error_message || 'Ingestion failed.');
                }
            } catch (error) {
                window.clearInterval(progressTimer);
                failedCount++;
                setRowIngestionResult(documentId, 'Failed', error.message || 'Ingestion failed.');
            }

            completed++;
            updateIngestionProgress(
                (completed / documentIds.length) * 100,
                `${completed} / ${documentIds.length}`,
                `Processed ${completed} of ${documentIds.length} documents.`
            );
        }

        if (elements.ingestionProgressTitle) {
            elements.ingestionProgressTitle.textContent = 'Ingestion complete';
        }

        if (elements.ingestionProgressMessage) {
            elements.ingestionProgressMessage.textContent = `${successCount} succeeded, ${failedCount} failed.`;
        }

        isIngesting = false;
        updateIngestionButtonState();
        await refreshDocuments();

        window.setTimeout(function () {
            if (elements.ingestionProgressPanel) {
                elements.ingestionProgressPanel.hidden = true;
            }
        }, 3000);
    }

    function showIngestionProgress(percent, countText, message) {
        if (elements.ingestionProgressPanel) {
            elements.ingestionProgressPanel.hidden = false;
        }

        if (elements.ingestionProgressTitle) {
            elements.ingestionProgressTitle.textContent = 'Ingesting documents...';
        }

        updateIngestionProgress(percent, countText, message);
    }

    function updateIngestionProgress(percent, countText, message) {
        if (elements.ingestionProgressFill) {
            elements.ingestionProgressFill.style.width = `${Math.max(0, Math.min(100, percent))}%`;
        }

        if (elements.ingestionProgressCount) {
            elements.ingestionProgressCount.textContent = countText;
        }

        if (elements.ingestionProgressMessage) {
            elements.ingestionProgressMessage.textContent = message;
        }
    }

    function renderPagination(currentPage, totalPages, totalCount, pageItemCount) {
        if (!elements.docPagination) {
            return;
        }

        if (!totalCount) {
            elements.docPagination.hidden = true;
            return;
        }

        elements.docPagination.hidden = false;

        const start = ((currentPage - 1) * PAGE_SIZE) + 1;
        const end = start + pageItemCount - 1;

        if (elements.paginationInfo) {
            elements.paginationInfo.textContent = `Showing ${start}-${end} of ${totalCount}`;
        }

        if (elements.prevPageBtn) {
            elements.prevPageBtn.disabled = currentPage <= 1;
        }

        if (elements.nextPageBtn) {
            elements.nextPageBtn.disabled = currentPage >= totalPages;
        }

        if (elements.paginationPages) {
            elements.paginationPages.innerHTML = buildPageButtons(currentPage, totalPages);
            elements.paginationPages.querySelectorAll('[data-page]').forEach(function (button) {
                button.addEventListener('click', function () {
                    goToPage(Number(button.getAttribute('data-page')));
                });
            });
        }
    }

    function buildPageButtons(currentPage, totalPages) {
        if (totalPages <= 1) {
            return `<button class="doc-page-btn active" type="button" data-page="1">1</button>`;
        }

        const pages = [];
        const windowSize = 5;
        let start = Math.max(1, currentPage - Math.floor(windowSize / 2));
        let end = Math.min(totalPages, start + windowSize - 1);

        if (end - start + 1 < windowSize) {
            start = Math.max(1, end - windowSize + 1);
        }

        for (let page = start; page <= end; page++) {
            pages.push(`<button class="doc-page-btn${page === currentPage ? ' active' : ''}" type="button" data-page="${page}">${page}</button>`);
        }

        return pages.join('');
    }

    function openUploadModal(files) {
        pendingUploadItems = files.map(function (file) {
            return {
                file: file,
                title: defaultTitle(file.name)
            };
        });

        if (elements.uploadModalSubtitle) {
            elements.uploadModalSubtitle.textContent = `Add a title for each ${activeCategory === 'Training' ? 'training material' : 'policy document'} before uploading.`;
        }

        renderUploadModalList();
        showUploadModal();
    }

    function renderUploadModalList() {
        if (!elements.uploadModalList) {
            return;
        }

        elements.uploadModalList.innerHTML = pendingUploadItems.map(function (item, index) {
            return `
                <div class="doc-upload-item">
                    <div class="doc-upload-item-file">
                        <div class="doc-upload-item-icon">
                            <iconify-icon icon="ph:file-duotone"></iconify-icon>
                        </div>
                        <div>
                            <strong>${escapeHtml(item.file.name)}</strong>
                            <span>${formatFileSize(item.file.size)}</span>
                        </div>
                    </div>
                    <label class="doc-upload-item-title">
                        <span>Title</span>
                        <input type="text" maxlength="500" data-upload-index="${index}" value="${escapeHtml(item.title)}" placeholder="Enter document title" />
                    </label>
                </div>`;
        }).join('');

        elements.uploadModalList.querySelectorAll('input[data-upload-index]').forEach(function (input) {
            input.addEventListener('input', function () {
                const index = Number(input.getAttribute('data-upload-index'));
                if (pendingUploadItems[index]) {
                    pendingUploadItems[index].title = input.value;
                    input.classList.toggle('invalid', !input.value.trim());
                }
            });
        });
    }

    function showUploadModal() {
        if (!elements.uploadModal) {
            return;
        }

        elements.uploadModal.hidden = false;
        elements.uploadModal.setAttribute('aria-hidden', 'false');
        document.body.classList.add('doc-modal-open');
    }

    function closeUploadModal() {
        if (!elements.uploadModal) {
            return;
        }

        elements.uploadModal.hidden = true;
        elements.uploadModal.setAttribute('aria-hidden', 'true');
        document.body.classList.remove('doc-modal-open');
        pendingUploadItems = [];

        if (elements.bulkFileInput) {
            elements.bulkFileInput.value = '';
        }
    }

    async function confirmUploadFromModal() {
        let hasError = false;

        elements.uploadModalList?.querySelectorAll('input[data-upload-index]').forEach(function (input) {
            const index = Number(input.getAttribute('data-upload-index'));
            const title = input.value.trim();
            pendingUploadItems[index].title = title;
            const invalid = !title;
            input.classList.toggle('invalid', invalid);
            if (invalid) {
                hasError = true;
            }
        });

        if (hasError) {
            if (elements.status) {
                elements.status.textContent = 'Please enter a title for each document.';
            }
            return;
        }

        const items = pendingUploadItems.slice();
        closeUploadModal();
        await uploadFiles(items);
    }

    async function toggleActive(input) {
        const documentId = Number(input.getAttribute('data-document-id'));
        const isActive = input.checked;

        try {
            const response = await fetch(endpoints.updateActive, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    Accept: 'application/json'
                },
                body: JSON.stringify({
                    document_id: documentId,
                    is_active: isActive
                })
            });

            const data = await response.json();
            if (!response.ok) {
                throw new Error(data.error_message || 'Unable to update document status.');
            }

            await loadStatistics();
        } catch (error) {
            input.checked = !isActive;
            window.alert(error.message || 'Unable to update document status.');
        }
    }

    async function uploadFiles(items) {
        if (isUploading || !items.length) {
            return;
        }

        isUploading = true;
        const category = activeCategory;
        let completed = 0;
        let successCount = 0;
        let failedCount = 0;

        showProgress(0, items.length, 'Starting bulk upload...');

        for (const item of items) {
            const formData = new FormData();
            formData.append('category', category);
            formData.append('title', item.title.trim());
            formData.append('files', item.file, item.file.name);

            try {
                await uploadSingleFile(formData, item.file.name, function (percent) {
                    const overall = ((completed + (percent / 100)) / items.length) * 100;
                    updateProgress(overall, `${completed} / ${items.length}`, `Uploading ${item.title.trim()}...`);
                });

                successCount++;
            } catch (error) {
                failedCount++;
                updateProgress(((completed + 1) / items.length) * 100, `${completed + 1} / ${items.length}`, error.message || `Failed to upload ${item.title.trim()}.`);
            }

            completed++;
            updateProgress((completed / items.length) * 100, `${completed} / ${items.length}`, `Processed ${completed} of ${items.length} files.`);
        }

        if (elements.uploadProgressTitle) {
            elements.uploadProgressTitle.textContent = 'Upload complete';
        }

        if (elements.uploadProgressMessage) {
            elements.uploadProgressMessage.textContent = `${successCount} succeeded, ${failedCount} failed.`;
        }

        paginationState[category].page = 1;
        await refreshPage();
        isUploading = false;

        window.setTimeout(function () {
            if (elements.uploadProgressPanel) {
                elements.uploadProgressPanel.hidden = true;
            }
        }, 2500);
    }

    function uploadSingleFile(formData, fileName, onProgress) {
        return new Promise(function (resolve, reject) {
            const xhr = new XMLHttpRequest();
            xhr.open('POST', endpoints.bulkUpload);
            xhr.setRequestHeader('Accept', 'application/json');

            xhr.upload.addEventListener('progress', function (event) {
                if (event.lengthComputable && typeof onProgress === 'function') {
                    onProgress((event.loaded / event.total) * 100);
                }
            });

            xhr.addEventListener('load', function () {
                let data = {};
                try {
                    data = JSON.parse(xhr.responseText || '{}');
                } catch (_) {
                    data = {};
                }

                if (xhr.status >= 200 && xhr.status < 300) {
                    const result = Array.isArray(data.results) ? data.results[0] : null;
                    if (result && result.success === false) {
                        reject(new Error(result.message || `Failed to upload ${fileName}.`));
                        return;
                    }

                    resolve(data);
                    return;
                }

                reject(new Error(data.error_message || `Failed to upload ${fileName}.`));
            });

            xhr.addEventListener('error', function () {
                reject(new Error(`Network error while uploading ${fileName}.`));
            });

            xhr.send(formData);
        });
    }

    function showProgress(percent, countText, message) {
        if (elements.uploadProgressPanel) {
            elements.uploadProgressPanel.hidden = false;
        }

        if (elements.uploadProgressTitle) {
            elements.uploadProgressTitle.textContent = 'Uploading documents...';
        }

        updateProgress(percent, countText, message);
    }

    function updateProgress(percent, countText, message) {
        if (elements.uploadProgressFill) {
            elements.uploadProgressFill.style.width = `${Math.max(0, Math.min(100, percent))}%`;
        }

        if (elements.uploadProgressCount) {
            elements.uploadProgressCount.textContent = countText;
        }

        if (elements.uploadProgressMessage) {
            elements.uploadProgressMessage.textContent = message;
        }
    }

    function defaultTitle(fileName) {
        const name = String(fileName || '').replace(/\\/g, '/').split('/').pop() || fileName;
        const dotIndex = name.lastIndexOf('.');
        return dotIndex > 0 ? name.slice(0, dotIndex) : name;
    }

    function formatFileSize(bytes) {
        if (!bytes) {
            return '0 B';
        }

        const units = ['B', 'KB', 'MB', 'GB'];
        const index = Math.min(Math.floor(Math.log(bytes) / Math.log(1024)), units.length - 1);
        const value = bytes / Math.pow(1024, index);
        return `${value.toFixed(index === 0 ? 0 : 1)} ${units[index]}`;
    }

    function escapeHtml(value) {
        return String(value)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
