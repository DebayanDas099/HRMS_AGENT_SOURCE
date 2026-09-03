(function () {
    'use strict';

    const loaders = window.AdminLoaders || {
        setGridLoading: function () {},
        setButtonLoading: function () {},
        renderTableSkeleton: function () {},
        runWithButtonLoading: function (_btn, task) { return Promise.resolve().then(task); }
    };
    const toast = window.AdminToast || { success: function () {}, error: function () {} };

    // Same endpoints and request/response shapes as before - this redesign only
    // touches markup/CSS/selection-tracking, not the API contract.
    const endpoints = {
        pending: '/Admin/Approvals/GetPending',
        submit: '/Admin/Approvals/Submit'
    };

    const elements = {
        countBadge: document.getElementById('approvalsCountBadge'),
        selectedLabel: document.getElementById('approvalsSelectedLabel'),
        panel: document.getElementById('approvalsPanel'),
        body: document.getElementById('approvalsTableBody'),
        search: document.getElementById('approvalsSearch'),
        submitBtn: document.getElementById('submitDecisionsBtn'),
        refreshBtn: document.getElementById('refreshApprovalsBtn')
    };

    let applications = [];
    // Decisions and remarks are tracked independently of the DOM (reference -> value)
    // so a search filter, or re-rendering after an Approve/Reject click, never loses
    // what the admin already picked or typed.
    const decisions = new Map();
    const remarks = new Map();
    let searchText = '';

    function init() {
        if (!elements.body) {
            return;
        }

        elements.refreshBtn?.addEventListener('click', function () {
            void loadPending();
        });

        elements.submitBtn?.addEventListener('click', function () {
            void submitDecisions();
        });

        elements.search?.addEventListener('input', function () {
            searchText = (elements.search.value || '').trim().toLowerCase();
            renderTable();
        });

        elements.body.addEventListener('click', function (event) {
            const actionBtn = event.target.closest('[data-decision]');
            if (!actionBtn) {
                return;
            }

            const reference = actionBtn.getAttribute('data-reference');
            const decision = actionBtn.getAttribute('data-decision');

            // Clicking the already-selected action clears it - a row can go back to
            // untouched/pending, matching "rows left untouched stay pending".
            if (decisions.get(reference) === decision) {
                decisions.delete(reference);
            } else {
                decisions.set(reference, decision);
            }

            renderTable();
            updateSelectionUi();
        });

        elements.body.addEventListener('input', function (event) {
            const input = event.target.closest('.apr-remarks-input');
            if (!input) {
                return;
            }

            const reference = input.getAttribute('data-reference');
            const value = input.value;
            if (value.trim()) {
                remarks.set(reference, value);
            } else {
                remarks.delete(reference);
            }

            // Live-clear the required styling as soon as the admin starts typing,
            // without a full re-render (which would steal focus from the field).
            input.classList.toggle('is-required', decisions.get(reference) === 'Rejected' && !value.trim());
            const hint = input.closest('td')?.querySelector('.apr-remarks-hint');
            if (hint) {
                hint.hidden = !(decisions.get(reference) === 'Rejected' && !value.trim());
            }
        });

        void loadPending();
    }

    async function readJson(response) {
        try {
            return await response.json();
        } catch {
            return {};
        }
    }

    function formatDate(value) {
        if (!value) {
            return '-';
        }
        const date = new Date(value);
        return isNaN(date.getTime()) ? '-' : date.toLocaleDateString();
    }

    async function loadPending() {
        loaders.setGridLoading(elements.panel, true, 'Loading pending applications...');
        loaders.renderTableSkeleton(elements.body, 8, 4);

        try {
            const response = await fetch(endpoints.pending, { headers: { Accept: 'application/json' } });
            const data = await readJson(response);
            if (!response.ok) {
                throw new Error(data.error_message || 'Unable to load pending applications.');
            }

            applications = Array.isArray(data) ? data : [];

            // Applications that already had a decision selected but are no longer in the
            // pending list (e.g. someone else actioned them) should not linger as stale state.
            const stillPending = new Set(applications.map(a => a.application_reference));
            [...decisions.keys()].forEach(function (reference) {
                if (!stillPending.has(reference)) {
                    decisions.delete(reference);
                    remarks.delete(reference);
                }
            });

            renderTable();
            updateSelectionUi();
        } catch (error) {
            toast.error(error.message || 'Unable to load pending applications.');
        } finally {
            loaders.setGridLoading(elements.panel, false);
        }
    }

    function filteredApplications() {
        if (!searchText) {
            return applications;
        }

        return applications.filter(function (app) {
            const haystack = `${app.employee_name || ''} ${app.mobile || ''} ${app.reason || ''}`.toLowerCase();
            return haystack.includes(searchText);
        });
    }

    function renderTable() {
        if (!elements.body) {
            return;
        }

        const visible = filteredApplications();

        if (applications.length === 0) {
            elements.body.innerHTML = emptyStateRow('Nothing awaiting approval right now.');
            return;
        }

        if (visible.length === 0) {
            elements.body.innerHTML = emptyStateRow('No applications match your search.');
            return;
        }

        elements.body.innerHTML = visible.map(function (app) {
            const reference = app.application_reference;
            const referenceAttr = escapeHtml(reference);
            const name = escapeHtml(app.employee_name || app.mobile || 'Unknown');
            const selected = decisions.get(reference);
            const remarksValue = remarks.get(reference) || '';
            const remarksRequired = selected === 'Rejected' && !remarksValue.trim();

            return `
                <tr data-reference="${referenceAttr}">
                    <td class="apr-employee-cell">${name}</td>
                    <td>${formatDate(app.from_date)}</td>
                    <td>${formatDate(app.to_date)}</td>
                    <td class="apr-reason-cell" title="${escapeHtml(app.reason || '')}">${escapeHtml(app.reason || '-')}</td>
                    <td>${formatDate(app.applied_on)}</td>
                    <td><span class="apr-status-badge">Pending</span></td>
                    <td class="apr-remarks-col">
                        <input type="text" class="apr-remarks-input ${remarksRequired ? 'is-required' : ''}"
                               data-reference="${referenceAttr}"
                               placeholder="${selected === 'Rejected' ? 'Reason for rejection...' : 'Optional remarks...'}"
                               value="${escapeHtml(remarksValue)}" />
                        <span class="apr-remarks-hint" ${remarksRequired ? '' : 'hidden'}>Required for rejection</span>
                    </td>
                    <td class="apr-action-col">
                        <div class="apr-action-group">
                            <button type="button" class="apr-action-btn apr-approve ${selected === 'Approved' ? 'is-selected' : ''}"
                                    data-reference="${referenceAttr}" data-decision="Approved" aria-pressed="${selected === 'Approved'}">
                                <iconify-icon icon="ph:check-bold"></iconify-icon><span>Approve</span>
                            </button>
                            <button type="button" class="apr-action-btn apr-reject ${selected === 'Rejected' ? 'is-selected' : ''}"
                                    data-reference="${referenceAttr}" data-decision="Rejected" aria-pressed="${selected === 'Rejected'}">
                                <iconify-icon icon="ph:x-bold"></iconify-icon><span>Reject</span>
                            </button>
                        </div>
                    </td>
                </tr>`;
        }).join('');
    }

    function emptyStateRow(message) {
        return `
            <tr class="apr-empty-row">
                <td colspan="8">
                    <div class="apr-empty-state">
                        <iconify-icon icon="ph:calendar-check-duotone"></iconify-icon>
                        <p>${escapeHtml(message)}</p>
                    </div>
                </td>
            </tr>`;
    }

    function updateSelectionUi() {
        setText(elements.countBadge, `${applications.length} pending`);

        const count = decisions.size;
        if (elements.selectedLabel) {
            elements.selectedLabel.hidden = count === 0;
            setText(elements.selectedLabel, `${count} decision${count === 1 ? '' : 's'} selected`);
        }

        if (elements.submitBtn) {
            elements.submitBtn.disabled = count === 0;
        }
    }

    async function submitDecisions() {
        if (decisions.size === 0) {
            toast.error('Pick Approve or Reject for at least one application before submitting.');
            return;
        }

        const missingRemarks = [...decisions.entries()]
            .filter(([reference, decision]) => decision === 'Rejected' && !(remarks.get(reference) || '').trim())
            .map(([reference]) => reference);

        if (missingRemarks.length > 0) {
            renderTable();
            toast.error('Remarks are required when rejecting an application.');
            return;
        }

        const payload = [...decisions.entries()].map(function ([reference, decision]) {
            return { application_reference: reference, decision: decision, remarks: (remarks.get(reference) || '').trim() || null };
        });

        await loaders.runWithButtonLoading(elements.submitBtn, async function () {
            try {
                const response = await fetch(endpoints.submit, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
                    body: JSON.stringify({ decisions: payload })
                });

                const data = await readJson(response);
                if (!response.ok) {
                    throw new Error(data.error_message || 'Unable to submit decisions.');
                }

                const processedCount = Array.isArray(data) ? data.length : payload.length;
                toast.success(`${processedCount} decision${processedCount === 1 ? '' : 's'} submitted.`);
                decisions.clear();
                remarks.clear();
                await loadPending();
            } catch (error) {
                toast.error(error.message || 'Unable to submit decisions.');
            }
        });
    }

    function setText(el, value) {
        if (el) {
            el.textContent = value;
        }
    }

    function escapeHtml(value) {
        return String(value ?? '').replace(/[&<>"']/g, function (ch) {
            return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[ch];
        });
    }

    init();
})();
