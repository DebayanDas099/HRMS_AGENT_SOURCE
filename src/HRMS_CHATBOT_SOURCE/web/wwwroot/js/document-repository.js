(function () {
    'use strict';

    const PAGE_SIZE = 10;
    const TABLE_COLUMN_COUNT = 7;

    const loaders = window.AdminLoaders || {
        setGridLoading: function () {},
        setComponentLoading: function () {},
        renderTableSkeleton: function () {},
        setButtonLoading: function () {},
        runWithButtonLoading: function (_btn, task) { return Promise.resolve().then(task); }
    };

    const endpoints = {
        statistics: '/Admin/DocumentRepository/GetStatistics',
        list: '/Admin/DocumentRepository/GetList',
        bulkUpload: '/Admin/DocumentRepository/BulkUpload',
        updateActive: '/Admin/DocumentRepository/UpdateActive',
        ingestDocument: '/Admin/DocumentRepository/IngestDocument',
        downloadDocument: '/Admin/DocumentRepository/Download',
        deleteDocument: '/Admin/DocumentRepository/Delete'
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
        confirmUploadModal: document.getElementById('confirmUploadModal'),
        docStatsGrid: document.getElementById('docStatsGrid'),
        docTablePanel: document.getElementById('docTablePanel'),
        deleteConfirmModal: document.getElementById('deleteConfirmModal'),
        deleteConfirmBackdrop: document.getElementById('deleteConfirmBackdrop'),
        deleteConfirmDocName: document.getElementById('deleteConfirmDocName'),
        cancelDeleteBtn: document.getElementById('cancelDeleteBtn'),
        confirmDeleteBtn: document.getElementById('confirmDeleteBtn'),
        confirmDeleteBtnLabel: document.getElementById('confirmDeleteBtnLabel')
    };

    const toast = window.AdminToast || {
        success: function () {},
        error: function () {}
    };

    let searchTimer = null;
    let isUploading = false;
    let isIngesting = false;
    let isDeleting = false;
    let activeCategory = 'Policy';
    let pendingUploadItems = [];
    let pendingDelete = null;

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

    const INGESTIBLE_EXTENSIONS = new Set([
        '.pdf', '.doc', '.docx', '.txt', '.ppt', '.pptx', '.xls', '.xlsx', '.csv', '.md'
    ]);

    const MEDIA_EXTENSIONS = new Set([
        '.jpg', '.jpeg', '.png', '.gif', '.webp', '.bmp', '.svg',
        '.mp4', '.mov', '.avi', '.wmv', '.mkv', '.webm', '.m4v', '.mpeg', '.mpg'
    ]);

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

        elements.refreshDocumentsBtn?.addEventListener('click', function () {
            void refreshPage(elements.refreshDocumentsBtn);
        });

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

        elements.cancelDeleteBtn?.addEventListener('click', closeDeleteConfirm);
        elements.deleteConfirmBackdrop?.addEventListener('click', closeDeleteConfirm);
        elements.confirmDeleteBtn?.addEventListener('click', function () {
            void confirmDelete();
        });

        document.addEventListener('keydown', function (event) {
            if (event.key !== 'Escape') {
                return;
            }

            if (elements.deleteConfirmModal && !elements.deleteConfirmModal.hidden) {
                closeDeleteConfirm();
                return;
            }

            if (elements.uploadModal && !elements.uploadModal.hidden) {
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

    async function refreshPage(triggerButton) {
        if (triggerButton) {
            loaders.setButtonLoading(triggerButton, true);
        }

        try {
            await Promise.all([loadStatistics(), refreshDocuments()]);
        } finally {
            if (triggerButton) {
                loaders.setButtonLoading(triggerButton, false);
            }
        }
    }

    async function loadStatistics() {
        loaders.setGridLoading(elements.docStatsGrid, true, 'Fetching statistics...');

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
        } finally {
            loaders.setGridLoading(elements.docStatsGrid, false);
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

        loaders.setComponentLoading(elements.docTablePanel, true, 'Loading documents...');
        loaders.renderTableSkeleton(elements.documentsTableBody, TABLE_COLUMN_COUNT, 5);

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
        } finally {
            loaders.setComponentLoading(elements.docTablePanel, false);
        }
    }

    function getInitials(name) {
        if (!name) {
            return '?';
        }

        return name
            .split(/\s+/)
            .filter(Boolean)
            .slice(0, 2)
            .map(function (part) { return part[0].toUpperCase(); })
            .join('');
    }

    function getFileExtension(doc) {
        const source = String(doc.dm_path || doc.dm_name || '').trim();
        const fileName = source.replace(/\\/g, '/').split('/').pop() || source;
        const dotIndex = fileName.lastIndexOf('.');
        if (dotIndex <= 0) {
            return '';
        }

        return fileName.slice(dotIndex).toLowerCase();
    }

    function isIngestibleDocument(doc) {
        const extension = getFileExtension(doc);
        if (!extension) {
            return false;
        }

        if (MEDIA_EXTENSIONS.has(extension)) {
            return false;
        }

        return INGESTIBLE_EXTENSIONS.has(extension);
    }

    function formatDateTime(value) {
        const date = new Date(value);
        if (Number.isNaN(date.getTime())) {
            return '—';
        }

        const pad = function (part) { return String(part).padStart(2, '0'); };
        const day = pad(date.getDate());
        const month = pad(date.getMonth() + 1);
        const year = date.getFullYear();
        let hours = date.getHours();
        const meridiem = hours >= 12 ? 'PM' : 'AM';
        hours = hours % 12;
        if (hours === 0) {
            hours = 12;
        }

        return `${day}/${month}/${year} ${pad(hours)}:${pad(date.getMinutes())} ${meridiem}`;
    }

    function getStatusButtonMeta(isActive) {
        return isActive
            ? {
                className: 'is-active',
                title: 'Deactivate document',
                ariaLabel: 'Document is active. Click to deactivate.',
                icon: 'ph:check-circle-duotone'
            }
            : {
                className: 'is-inactive',
                title: 'Activate document',
                ariaLabel: 'Document is inactive. Click to activate.',
                icon: 'ph:prohibit-duotone'
            };
    }

    function renderStatusButton(documentId, isActive) {
        const meta = getStatusButtonMeta(isActive);
        return `
            <button type="button" class="doc-action-btn doc-status-btn ${meta.className}" data-document-id="${documentId}" data-is-active="${isActive ? 'true' : 'false'}" title="${meta.title}" aria-label="${meta.ariaLabel}">
                <iconify-icon icon="${meta.icon}"></iconify-icon>
            </button>`;
    }

    function updateStatusButtonAppearance(button, isActive) {
        const meta = getStatusButtonMeta(isActive);
        button.setAttribute('data-is-active', isActive ? 'true' : 'false');
        button.classList.toggle('is-active', isActive);
        button.classList.toggle('is-inactive', !isActive);
        button.title = meta.title;
        button.setAttribute('aria-label', meta.ariaLabel);

        const icon = button.querySelector('iconify-icon');
        if (icon) {
            icon.setAttribute('icon', meta.icon);
        }
    }

    function renderIngestSelectCell(doc, isActive, ingestionStatus) {
        if (!isIngestibleDocument(doc)) {
            return '<td class="doc-ingest-col"><span class="doc-ingest-na">—</span></td>';
        }

        const canSelect = isActive && ingestionStatus.toLowerCase() !== 'processing';
        return `
            <td class="doc-ingest-col">
                <label class="doc-toggle" title="Select for ingestion">
                    <input type="checkbox" class="doc-ingest-toggle" data-select-id="${doc.dm_id}" ${canSelect ? '' : 'disabled'} />
                    <span class="doc-toggle-slider"></span>
                </label>
            </td>`;
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
                    <td colspan="7">
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
            const isProcessing = ingestionStatus.toLowerCase() === 'processing';
            const createdDate = doc.dm_created_date
                ? formatDateTime(doc.dm_created_date)
                : '—';

            const createdBy = doc.dm_created_by || '—';
            const createdInitials = getInitials(createdBy);

            return `
                <tr data-document-row="${doc.dm_id}">
                    ${renderIngestSelectCell(doc, isActive, ingestionStatus)}
                    <td class="doc-title-cell" title="${escapeHtml(doc.dm_name || '')}">${escapeHtml(doc.dm_name || '—')}</td>
                    <td><span class="doc-category-badge ${categoryClass}">${escapeHtml(doc.dm_category || '—')}</span></td>
                    <td class="doc-ingestion-cell">
                        <div class="doc-ingestion-status">
                            <span class="doc-ingestion-badge ${ingestionClass}" data-ingestion-badge="${doc.dm_id}">${escapeHtml(ingestionStatus)}</span>
                        </div>
                        <div class="doc-ingestion-stats" data-ingestion-stats="${doc.dm_id}">
                            ${renderIngestionStatsHtml(doc, ingestionStatus)}
                        </div>
                        <div class="doc-ingest-progress" data-ingest-progress="${doc.dm_id}"${isProcessing ? '' : ' hidden'}>
                            <div class="doc-ingest-progress-bar">
                                <div class="doc-ingest-progress-fill" data-ingest-fill="${doc.dm_id}"></div>
                            </div>
                            <span class="doc-ingest-progress-text" data-ingest-text="${doc.dm_id}">${isProcessing ? 'Processing...' : '0%'}</span>
                        </div>
                        ${doc.dm_ingestion_error ? `<span class="doc-ingestion-error" title="${escapeHtml(doc.dm_ingestion_error)}">${escapeHtml(doc.dm_ingestion_error)}</span>` : ''}
                    </td>
                    <td>
                        <span class="user-pill">
                            <span class="user-pill-avatar">${escapeHtml(createdInitials)}</span>
                            <span class="user-pill-name">${escapeHtml(createdBy)}</span>
                        </span>
                    </td>
                    <td class="doc-date-cell" title="${escapeHtml(createdDate)}">${escapeHtml(createdDate)}</td>
                    <td class="doc-actions-col">
                        <div class="doc-row-actions">
                            <button type="button" class="doc-action-btn doc-download-btn" data-document-id="${doc.dm_id}" data-file-name="${escapeHtml(getDownloadFileName(doc))}" title="Download document" aria-label="Download document">
                                <iconify-icon icon="ph:download-simple-duotone"></iconify-icon>
                            </button>
                            ${renderStatusButton(doc.dm_id, isActive)}
                            <button type="button" class="doc-action-btn doc-delete-btn" data-document-id="${doc.dm_id}" data-document-name="${escapeHtml(doc.dm_name || '')}" title="Delete document" aria-label="Delete document">
                                <iconify-icon icon="ph:trash-duotone"></iconify-icon>
                            </button>
                        </div>
                    </td>
                </tr>`;
        }).join('');

        elements.documentsTableBody.querySelectorAll('.doc-status-btn').forEach(function (button) {
            button.addEventListener('click', function () {
                void toggleActiveButton(button);
            });
        });

        elements.documentsTableBody.querySelectorAll('.doc-download-btn').forEach(function (button) {
            button.addEventListener('click', function () {
                void downloadDocument(button);
            });
        });

        elements.documentsTableBody.querySelectorAll('.doc-delete-btn').forEach(function (button) {
            button.addEventListener('click', function () {
                openDeleteConfirm(button);
            });
        });

        elements.documentsTableBody.querySelectorAll('.doc-ingest-toggle').forEach(function (input) {
            input.addEventListener('change', updateIngestionButtonState);
        });

        updateIngestionButtonState();
    }

    function getIngestionStatusClass(status) {
        switch ((status || '').toLowerCase()) {
            case 'completed':
                return 'completed';
            case 'failed':
                return 'failed';
            default:
                return 'pending';
        }
    }

    function renderIngestionStatsHtml(doc, ingestionStatus) {
        const status = (ingestionStatus || 'Pending').toLowerCase();
        const chunkCount = Number(doc.dm_chunk_count) || 0;
        const ingestedAt = doc.dm_ingested_at ? formatDateTime(doc.dm_ingested_at) : null;

        if (status === 'processing') {
            return '<span class="doc-ingestion-stat">Preparing chunks and embeddings...</span>';
        }

        if (status === 'completed') {
            if (chunkCount > 0) {
                return `<span class="doc-ingestion-stat"><strong>${chunkCount}</strong> chunk${chunkCount === 1 ? '' : 's'} indexed${ingestedAt ? ` · ${escapeHtml(ingestedAt)}` : ''}</span>`;
            }

            return '<span class="doc-ingestion-stat">Indexed successfully</span>';
        }

        if (status === 'failed') {
            return chunkCount > 0
                ? `<span class="doc-ingestion-stat">${chunkCount} chunk${chunkCount === 1 ? '' : 's'} before failure</span>`
                : '<span class="doc-ingestion-stat muted">Ingestion failed</span>';
        }

        if (status === 'skipped') {
            return '<span class="doc-ingestion-stat muted">Ingestion skipped</span>';
        }

        return '<span class="doc-ingestion-stat muted">Awaiting ingestion</span>';
    }

    function updateIngestionStatsCell(documentId, status, chunkCount, ingestedAt) {
        const statsEl = document.querySelector(`[data-ingestion-stats="${documentId}"]`);
        if (!statsEl) {
            return;
        }

        statsEl.innerHTML = renderIngestionStatsHtml({
            dm_chunk_count: chunkCount,
            dm_ingested_at: ingestedAt
        }, status);
    }

    function getSelectedDocumentIds() {
        return Array.from(elements.documentsTableBody?.querySelectorAll('.doc-ingest-toggle:checked') || [])
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
            badge.className = 'doc-ingestion-badge pending';
        }

        updateIngestionStatsCell(documentId, 'Processing', null, null);
    }

    function setRowIngestionResult(documentId, status, errorMessage, chunkCount, vectorCount) {
        const progressWrap = document.querySelector(`[data-ingest-progress="${documentId}"]`);
        const progressFill = document.querySelector(`[data-ingest-fill="${documentId}"]`);
        const progressText = document.querySelector(`[data-ingest-text="${documentId}"]`);
        const badge = document.querySelector(`[data-ingestion-badge="${documentId}"]`);
        const statusClass = getIngestionStatusClass(status);

        if (progressWrap) {
            progressWrap.hidden = true;
        }

        if (progressFill) {
            progressFill.style.width = '0%';
            progressFill.classList.remove('failed');
        }

        if (progressText) {
            progressText.textContent = '0%';
        }

        if (badge) {
            badge.textContent = status;
            badge.className = `doc-ingestion-badge ${statusClass}`;
        }

        const resolvedChunkCount = Number(chunkCount) || 0;
        const normalizedStatus = String(status).toLowerCase();

        if (normalizedStatus === 'completed' && resolvedChunkCount > 0) {
            const vectorSuffix = Number(vectorCount) > 0
                ? ` · <strong>${vectorCount}</strong> vector${Number(vectorCount) === 1 ? '' : 's'} stored`
                : '';
            const ingestedLabel = formatDateTime(new Date().toISOString());
            const statsEl = document.querySelector(`[data-ingestion-stats="${documentId}"]`);
            if (statsEl) {
                statsEl.innerHTML = `<span class="doc-ingestion-stat"><strong>${resolvedChunkCount}</strong> chunk${resolvedChunkCount === 1 ? '' : 's'} indexed${vectorSuffix} · ${escapeHtml(ingestedLabel)}</span>`;
            }
        } else {
            updateIngestionStatsCell(documentId, status, resolvedChunkCount, new Date().toISOString());
        }

        if (errorMessage) {
            const row = document.querySelector(`[data-document-row="${documentId}"]`);
            const cell = row?.querySelector('.doc-ingestion-cell');
            let errorEl = cell?.querySelector('.doc-ingestion-error');
            if (cell && !errorEl) {
                errorEl = document.createElement('span');
                errorEl.className = 'doc-ingestion-error';
                cell.appendChild(errorEl);
            }

            if (errorEl) {
                errorEl.title = errorMessage;
                errorEl.textContent = errorMessage;
            }
        }
    }

    async function startSelectedIngestion() {
        const documentIds = getSelectedDocumentIds();
        if (isIngesting || !documentIds.length) {
            return;
        }

        isIngesting = true;
        loaders.setButtonLoading(elements.startIngestionBtn, true);
        updateIngestionButtonState();

        let completed = 0;
        let successCount = 0;
        let failedCount = 0;

        showIngestionProgress(0, documentIds.length, 'Starting ingestion...');

        for (const documentId of documentIds) {
            const ingestToggle = document.querySelector(`.doc-ingest-toggle[data-select-id="${documentId}"]`);
            if (ingestToggle) {
                ingestToggle.checked = false;
                ingestToggle.disabled = true;
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
                const chunkCount = data.chunk_count ?? 0;
                const vectorCount = data.vector_count ?? 0;
                if (String(status).toLowerCase() === 'completed') {
                    successCount++;
                    setRowIngestionResult(documentId, status, null, chunkCount, vectorCount);
                } else if (String(status).toLowerCase() === 'skipped') {
                    setRowIngestionResult(documentId, status, data.error_message, chunkCount, vectorCount);
                } else {
                    failedCount++;
                    setRowIngestionResult(documentId, status, data.error_message || 'Ingestion failed.', chunkCount, vectorCount);
                }
            } catch (error) {
                window.clearInterval(progressTimer);
                failedCount++;
                setRowIngestionResult(documentId, 'Failed', error.message || 'Ingestion failed.', 0, 0);
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
        loaders.setButtonLoading(elements.startIngestionBtn, false);
        updateIngestionButtonState();
        await refreshPage();

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
            elements.paginationInfo.textContent = `Showing ${start} to ${end} of ${totalCount} documents`;
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
        loaders.setButtonLoading(elements.confirmUploadModal, true);

        try {
            closeUploadModal();
            await uploadFiles(items);
        } finally {
            loaders.setButtonLoading(elements.confirmUploadModal, false);
        }
    }

    function getDownloadFileName(doc) {
        const path = String(doc.dm_path || '').trim();
        if (path) {
            const blobName = path.replace(/\\/g, '/').split('/').pop() || path;
            const underscoreIndex = blobName.indexOf('_');
            if (underscoreIndex >= 0 && underscoreIndex < blobName.length - 1) {
                return blobName.slice(underscoreIndex + 1);
            }

            return blobName;
        }

        return doc.dm_name || 'document';
    }

    async function downloadDocument(button) {
        const documentId = Number(button.getAttribute('data-document-id'));
        const fallbackName = button.getAttribute('data-file-name') || 'document';
        if (!documentId) {
            return;
        }

        button.disabled = true;
        loaders.setButtonLoading(button, true);

        try {
            const response = await fetch(`${endpoints.downloadDocument}?document_id=${documentId}`);
            if (!response.ok) {
                let message = 'Unable to download document.';
                try {
                    const data = await response.json();
                    message = data.error_message || data.title || message;
                } catch (_) {
                    // Binary or empty error body — keep default message.
                }

                throw new Error(message);
            }

            const blob = await response.blob();
            const disposition = response.headers.get('Content-Disposition') || '';
            const match = disposition.match(/filename\*?=(?:UTF-8''|")?([^";]+)/i);
            const fileName = match ? decodeURIComponent(match[1].replace(/"/g, '')) : fallbackName;
            const url = window.URL.createObjectURL(blob);
            const link = document.createElement('a');
            link.href = url;
            link.download = fileName;
            document.body.appendChild(link);
            link.click();
            link.remove();
            window.URL.revokeObjectURL(url);
        } catch (error) {
            window.alert(error.message || 'Unable to download document.');
        } finally {
            loaders.setButtonLoading(button, false);
            button.disabled = false;
        }
    }

    function openDeleteConfirm(button) {
        const documentId = Number(button.getAttribute('data-document-id'));
        if (!documentId || !elements.deleteConfirmModal) {
            return;
        }

        pendingDelete = {
            documentId: documentId,
            documentName: button.getAttribute('data-document-name') || '',
            triggerButton: button
        };

        if (elements.deleteConfirmDocName) {
            elements.deleteConfirmDocName.textContent = pendingDelete.documentName;
            elements.deleteConfirmDocName.hidden = !pendingDelete.documentName;
        }

        setDeleteButtonsBusy(false);
        elements.deleteConfirmModal.hidden = false;
        elements.deleteConfirmModal.setAttribute('aria-hidden', 'false');
        document.body.classList.add('doc-modal-open');
        elements.cancelDeleteBtn?.focus();
    }

    function closeDeleteConfirm() {
        if (!elements.deleteConfirmModal || isDeleting) {
            return;
        }

        elements.deleteConfirmModal.hidden = true;
        elements.deleteConfirmModal.setAttribute('aria-hidden', 'true');
        document.body.classList.remove('doc-modal-open');

        const trigger = pendingDelete?.triggerButton;
        pendingDelete = null;

        if (trigger && document.body.contains(trigger)) {
            trigger.focus();
        }
    }

    function setDeleteButtonsBusy(busy) {
        if (elements.confirmDeleteBtnLabel) {
            elements.confirmDeleteBtnLabel.textContent = busy ? 'Deleting...' : 'Yes, Delete';
        }

        if (elements.cancelDeleteBtn) {
            elements.cancelDeleteBtn.disabled = busy;
        }

        loaders.setButtonLoading(elements.confirmDeleteBtn, busy);
    }

    async function confirmDelete() {
        if (isDeleting || !pendingDelete) {
            return;
        }

        const { documentId } = pendingDelete;

        isDeleting = true;
        setDeleteButtonsBusy(true);

        try {
            const response = await fetch(endpoints.deleteDocument, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    Accept: 'application/json'
                },
                body: JSON.stringify({ document_id: documentId })
            });

            const data = await response.json();
            if (!response.ok) {
                throw new Error(data.error_message || 'Unable to delete document.');
            }

            isDeleting = false;
            setDeleteButtonsBusy(false);
            closeDeleteConfirm();
            toast.success('Document deleted successfully.');
            await refreshPage();
        } catch (error) {
            isDeleting = false;
            setDeleteButtonsBusy(false);
            toast.error(error.message || 'Unable to delete document.');
        }
    }

    async function toggleActiveButton(button) {
        const documentId = Number(button.getAttribute('data-document-id'));
        const isActive = button.getAttribute('data-is-active') === 'true';
        const newActive = !isActive;

        button.disabled = true;
        loaders.setButtonLoading(button, true);

        try {
            const response = await fetch(endpoints.updateActive, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    Accept: 'application/json'
                },
                body: JSON.stringify({
                    document_id: documentId,
                    is_active: newActive
                })
            });

            const data = await response.json();
            if (!response.ok) {
                throw new Error(data.error_message || 'Unable to update document status.');
            }

            updateStatusButtonAppearance(button, newActive);

            const row = button.closest('[data-document-row]');
            const ingestToggle = row?.querySelector('.doc-ingest-toggle');
            if (ingestToggle) {
                if (!newActive) {
                    ingestToggle.checked = false;
                }

                const badge = row.querySelector(`[data-ingestion-badge="${documentId}"]`);
                const ingestionStatus = badge?.textContent?.trim() || 'Pending';
                ingestToggle.disabled = !newActive || ingestionStatus.toLowerCase() === 'processing';
            }

            updateIngestionButtonState();
            await loadStatistics();
        } catch (error) {
            window.alert(error.message || 'Unable to update document status.');
        } finally {
            loaders.setButtonLoading(button, false);
            button.disabled = false;
        }
    }

    async function uploadFiles(items) {
        if (isUploading || !items.length) {
            return;
        }

        isUploading = true;
        loaders.setButtonLoading(elements.bulkUploadBtn, true);
        const category = activeCategory;
        let completed = 0;
        let successCount = 0;
        let failedCount = 0;

        try {
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

            window.setTimeout(function () {
                if (elements.uploadProgressPanel) {
                    elements.uploadProgressPanel.hidden = true;
                }
            }, 2500);
        } finally {
            isUploading = false;
            loaders.setButtonLoading(elements.bulkUploadBtn, false);
        }
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
