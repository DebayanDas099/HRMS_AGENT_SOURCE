(function () {
    'use strict';

    const loaders = window.AdminLoaders || {
        setGridLoading: function () {},
        setComponentLoading: function () {},
        setButtonLoading: function () {},
        runWithButtonLoading: function (_btn, task) { return Promise.resolve().then(task); }
    };

    const payrollBuckets = [
        { key: 'onroll', label: 'Onroll' },
        { key: 'offroll', label: 'Offroll' }
    ];

    const endpoints = {
        groups: '/Admin/AgentControlPanel/GetGroups',
        models: '/Admin/AgentControlPanel/GetModels',
        updateModel: '/Admin/AgentControlPanel/UpdateModelActive',
        assignmentsForAgent: '/Admin/AgentControlPanel/GetAssignmentsForAgent',
        updateActive: '/Admin/AgentControlPanel/UpdateActive'
    };

    const elements = {
        status: document.getElementById('acpStatus'),
        statTotalGroups: document.getElementById('statTotalGroups'),
        statTotalAgents: document.getElementById('statTotalAgents'),
        statActiveAgents: document.getElementById('statActiveAgents'),
        statInactiveAgents: document.getElementById('statInactiveAgents'),
        refreshBtn: document.getElementById('refreshAgentsBtn'),
        saveModelsBtn: document.getElementById('saveModelsBtn'),
        modelsCountBadge: document.getElementById('acpModelsCountBadge'),
        modelsBody: document.getElementById('modelsTableBody'),
        statsGrid: document.getElementById('acpStatsGrid'),
        modelsPanel: document.getElementById('acpModelsPanel'),
        modal: document.getElementById('acpGroupModal'),
        modalBackdrop: document.getElementById('acpGroupModalBackdrop'),
        modalTitle: document.getElementById('acpGroupModalTitle'),
        modalSubtitle: document.getElementById('acpGroupModalSubtitle'),
        closeModal: document.getElementById('closeGroupModal'),
        saveModal: document.getElementById('doneGroupModal'),
        groupSearch: document.getElementById('acpGroupSearch'),
        enableAll: document.getElementById('acpEnableAll'),
        selectedCount: document.getElementById('acpSelectedCount'),
        groupTree: document.getElementById('acpGroupTree')
    };

    const agentIcons = {
        SupervisorAgent: 'ph:crown-duotone',
        LeaveApplicationAgent: 'ph:calendar-check-duotone',
        DocumentAgent: 'ph:folder-open-duotone',
        KnowledgeAgent: 'ph:book-open-duotone',
        CriticAgent: 'ph:shield-check-duotone',
        VoiceInputAgent: 'ph:microphone-duotone'
    };

    let groups = [];
    let modelItems = [];
    let assignmentCells = [];
    let payrollExpanded = { onroll: false, offroll: false };
    let selectedAgent = null;
    let searchText = '';

    function init() {
        if (!elements.modelsBody) {
            return;
        }

        elements.refreshBtn?.addEventListener('click', function () {
            void refreshPage(elements.refreshBtn);
        });

        elements.saveModelsBtn?.addEventListener('click', function () {
            void saveModels(elements.saveModelsBtn);
        });

        elements.modelsBody.addEventListener('change', function (event) {
            const checkbox = event.target.closest('[data-model-check]');
            if (checkbox) {
                setModelPending(Number(checkbox.getAttribute('data-agent-id')), checkbox.checked);
            }
        });

        elements.modelsBody.addEventListener('click', function (event) {
            const nameBtn = event.target.closest('[data-open-groups]');
            if (nameBtn) {
                void openGroupModal(Number(nameBtn.getAttribute('data-agent-id')));
            }
        });

        elements.closeModal?.addEventListener('click', closeGroupModal);
        elements.modalBackdrop?.addEventListener('click', closeGroupModal);
        elements.saveModal?.addEventListener('click', function () {
            void saveGroupAssignments(elements.saveModal);
        });

        document.addEventListener('keydown', function (event) {
            if (event.key === 'Escape' && elements.modal && !elements.modal.hidden) {
                closeGroupModal();
            }
        });

        elements.groupSearch?.addEventListener('input', function () {
            searchText = (elements.groupSearch.value || '').trim().toLowerCase();
            renderGroupTree();
        });

        elements.enableAll?.addEventListener('change', function () {
            toggleAllGroups(elements.enableAll.checked);
        });

        elements.groupTree?.addEventListener('click', function (event) {
            const header = event.target.closest('[data-toggle-payroll]');
            if (header) {
                const payroll = header.getAttribute('data-payroll');
                payrollExpanded[payroll] = !payrollExpanded[payroll];
                renderGroupTree();
            }
        });

        elements.groupTree?.addEventListener('change', function (event) {
            const checkbox = event.target.closest('[data-group-check]');
            if (!checkbox || checkbox.disabled) {
                return;
            }

            const groupCode = checkbox.getAttribute('data-group-code');
            const payroll = checkbox.getAttribute('data-payroll');
            const cell = findCell(groupCode, payroll);
            if (cell && cell.canToggle) {
                cell.isActive = checkbox.checked;
                renderGroupTree();
            }
        });

        void refreshPage();
    }

    async function refreshPage(button) {
        const run = async function () {
            loaders.setGridLoading(elements.statsGrid, true, 'Loading models...');
            loaders.setComponentLoading(elements.modelsPanel, true, 'Loading models...');
            try {
                await Promise.all([loadModels(), loadGroups()]);
            } finally {
                loaders.setGridLoading(elements.statsGrid, false);
                loaders.setComponentLoading(elements.modelsPanel, false);
            }
        };

        if (button) {
            await loaders.runWithButtonLoading(button, run);
            return;
        }

        await run();
    }

    async function loadModels() {
        const response = await fetch(endpoints.models, { headers: { Accept: 'application/json' } });
        const data = await readJson(response);
        if (!response.ok) {
            throw new Error(data.error_message || 'Unable to load agent models.');
        }

        setText(elements.statTotalAgents, data.total_agents);
        setText(elements.statActiveAgents, data.active_agents);
        setText(elements.statInactiveAgents, data.inactive_agents);
        if (elements.modelsCountBadge) {
            elements.modelsCountBadge.textContent = String(data.total_agents || 0);
        }

        modelItems = (data.items || []).map(function (agent) {
            const isActive = String(agent.am_active || '').toUpperCase() === 'Y';
            return Object.assign({}, agent, {
                originalActive: isActive,
                pendingActive: isActive
            });
        });

        renderModels();
    }

    async function loadGroups() {
        const response = await fetch(endpoints.groups, { headers: { Accept: 'application/json' } });
        const data = await readJson(response);
        if (!response.ok) {
            throw new Error(data.error_message || 'Unable to load user groups.');
        }

        groups = Array.isArray(data) ? data : [];
        setText(elements.statTotalGroups, groups.length);
        setStatus(groups.length
            ? 'Check models, then Save. Click an agent name to assign onroll and offroll groups.'
            : 'No active user groups were found.');
    }

    function setModelPending(agentId, isActive) {
        const row = modelItems.find(function (item) { return Number(item.am_id) === agentId; });
        if (!row) {
            return;
        }

        const canToggle = String(row.can_toggle || '').toUpperCase() === 'Y';
        if (!canToggle) {
            return;
        }

        row.pendingActive = isActive;
        renderModels();
    }

    function renderModels() {
        if (!modelItems.length) {
            elements.modelsBody.innerHTML = emptyRow(4, 'No models are registered in agent master.');
            return;
        }

        elements.modelsBody.innerHTML = modelItems.map(function (agent) {
            const isActive = !!agent.pendingActive;
            const isLocked = String(agent.is_locked || '').toUpperCase() === 'Y';
            const canToggle = String(agent.can_toggle || '').toUpperCase() === 'Y';
            const roleClass = (agent.role || 'Specialist').toLowerCase();
            const icon = agentIcons[agent.am_name] || 'ph:robot-duotone';
            const note = agent.am_name === 'VoiceInputAgent'
                ? 'Voice input for chat — assign per user group.'
                : (isLocked ? 'Always on — click name to assign groups' : 'Click name to assign user groups');

            return `
                <tr data-model-row data-agent-id="${agent.am_id}">
                    <td>${renderAgentCell(agent, icon, note)}</td>
                    <td><span class="acp-role-pill ${escapeHtml(roleClass)}">${escapeHtml(agent.role || 'Specialist')}</span></td>
                    <td><span class="acp-flag-pill ${isActive ? 'is-on' : 'is-off'}">${isActive ? 'Active' : 'Inactive'}</span></td>
                    <td class="acp-actions-col">${renderCheckbox({
                        agentId: agent.am_id,
                        isActive: isActive,
                        canToggle: canToggle,
                        isLocked: isLocked,
                        scope: 'model'
                    })}</td>
                </tr>`;
        }).join('');
    }

    function renderAgentCell(agent, icon, note) {
        return `
            <div class="acp-agent-cell">
                <div class="acp-agent-icon">
                    <iconify-icon icon="${icon}"></iconify-icon>
                </div>
                <div>
                    <button type="button" class="acp-agent-name-btn" data-open-groups data-agent-id="${agent.am_id}"
                        data-agent-name="${escapeHtml(agent.am_name)}"
                        data-am-active="${escapeHtml(agent.am_active || 'N')}"
                        data-is-locked="${escapeHtml(agent.is_locked || 'N')}">
                        ${escapeHtml(agent.am_name)}
                    </button>
                    ${note ? `<span class="acp-agent-note">${escapeHtml(note)}</span>` : ''}
                </div>
            </div>`;
    }

    async function saveModels(button) {
        const changes = modelItems.filter(function (item) {
            return String(item.can_toggle || '').toUpperCase() === 'Y'
                && item.pendingActive !== item.originalActive;
        });

        if (!changes.length) {
            setStatus('No model changes to save.');
            return;
        }

        const run = async function () {
            for (let i = 0; i < changes.length; i += 1) {
                const agent = changes[i];
                const response = await fetch(endpoints.updateModel, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
                    body: JSON.stringify({ agent_id: agent.am_id, is_active: agent.pendingActive })
                });

                const data = await readJson(response);
                if (!response.ok) {
                    throw new Error(data.error_message || `Unable to update ${agent.am_name}.`);
                }
            }

            await loadModels();
            setStatus('Agent model status saved.');
        };

        try {
            if (button) {
                await loaders.runWithButtonLoading(button, run);
                return;
            }

            await run();
        } catch (error) {
            window.alert(error.message || 'Unable to save model status.');
        }
    }

    async function openGroupModal(agentId) {
        const nameBtn = elements.modelsBody.querySelector(`[data-open-groups][data-agent-id="${agentId}"]`);
        selectedAgent = {
            agentId: agentId,
            agentName: nameBtn?.getAttribute('data-agent-name') || 'Agent',
            masterActive: String(nameBtn?.getAttribute('data-am-active') || 'N').toUpperCase() === 'Y',
            isLocked: String(nameBtn?.getAttribute('data-is-locked') || 'N').toUpperCase() === 'Y'
        };

        searchText = '';
        payrollExpanded = { onroll: false, offroll: false };
        if (elements.groupSearch) {
            elements.groupSearch.value = '';
        }

        if (elements.modalTitle) {
            elements.modalTitle.textContent = selectedAgent.agentName;
        }
        if (elements.modalSubtitle) {
            elements.modalSubtitle.textContent = 'Enable this agent for onroll and offroll user groups.';
        }

        elements.modal.hidden = false;
        elements.modal.setAttribute('aria-hidden', 'false');
        document.body.classList.add('acp-modal-open');

        await loadGroupAccess();
    }

    function closeGroupModal() {
        if (!elements.modal) {
            return;
        }

        elements.modal.hidden = true;
        elements.modal.setAttribute('aria-hidden', 'true');
        document.body.classList.remove('acp-modal-open');
        selectedAgent = null;
        assignmentCells = [];
    }

    async function loadGroupAccess() {
        if (!selectedAgent) {
            return;
        }

        loaders.setComponentLoading(elements.groupTree, true, 'Loading user groups...');
        try {
            const url = `${endpoints.assignmentsForAgent}?agent_id=${encodeURIComponent(selectedAgent.agentId)}`;
            const response = await fetch(url, { headers: { Accept: 'application/json' } });
            const data = await readJson(response);
            if (!response.ok) {
                throw new Error(data.error_message || 'Unable to load group assignments.');
            }

            assignmentCells = (data.items || []).map(function (item) {
                const isLocked = String(item.is_locked ?? (selectedAgent.isLocked ? 'Y' : 'N')).toUpperCase() === 'Y';
                const isActive = isLocked || String(item.aaug_active || '').toUpperCase() === 'Y';
                return {
                    groupCode: item.grp_user_group_code,
                    groupDesc: item.grp_user_group_desc || item.grp_user_group_code,
                    payroll: String(item.user_payroll || 'onroll').toLowerCase() === 'offroll' ? 'offroll' : 'onroll',
                    isActive: isActive,
                    originalActive: isActive,
                    canToggle: String(item.can_toggle || '').toUpperCase() === 'Y',
                    isLocked: isLocked
                };
            });

            renderGroupTree();
        } catch (error) {
            elements.groupTree.innerHTML = `
                <div class="empty-state compact">
                    <div class="empty-state-icon">
                        <iconify-icon icon="ph:warning-circle-duotone"></iconify-icon>
                    </div>
                    <h4>Unable to load groups</h4>
                    <p>${escapeHtml(error.message || 'Try again.')}</p>
                </div>`;
        } finally {
            loaders.setComponentLoading(elements.groupTree, false);
        }
    }

    function findCell(groupCode, payroll) {
        return assignmentCells.find(function (item) {
            return item.groupCode === groupCode && item.payroll === payroll;
        });
    }

    function filteredCells() {
        if (!searchText) {
            return assignmentCells;
        }

        return assignmentCells.filter(function (item) {
            return `${item.groupCode} ${item.groupDesc} ${item.payroll}`.toLowerCase().includes(searchText);
        });
    }

    function renderGroupTree() {
        const rows = filteredCells();
        const enabledCount = assignmentCells.filter(function (item) { return item.isActive; }).length;
        const toggleable = assignmentCells.filter(function (item) { return item.canToggle; });
        const enabledToggleable = toggleable.filter(function (item) { return item.isActive; }).length;

        if (elements.selectedCount) {
            elements.selectedCount.textContent = `${enabledCount} selected`;
        }

        if (elements.enableAll) {
            const allOn = toggleable.length > 0 && enabledToggleable === toggleable.length;
            const someOn = enabledToggleable > 0 && !allOn;
            elements.enableAll.checked = allOn;
            elements.enableAll.indeterminate = someOn;
            elements.enableAll.disabled = toggleable.length === 0;
        }

        if (!rows.length) {
            elements.groupTree.innerHTML = `
                <div class="empty-state compact">
                    <div class="empty-state-icon">
                        <iconify-icon icon="ph:users-three-duotone"></iconify-icon>
                    </div>
                    <h4>No user groups</h4>
                    <p>${searchText ? 'No groups match your search.' : 'No active user groups were found.'}</p>
                </div>`;
            return;
        }

        elements.groupTree.innerHTML = payrollBuckets.map(function (bucket) {
            const bucketRows = rows.filter(function (item) { return item.payroll === bucket.key; });
            if (!bucketRows.length) {
                return '';
            }

            const enabledInBucket = bucketRows.filter(function (item) { return item.isActive; }).length;
            const expanded = !!payrollExpanded[bucket.key];

            return `
                <div class="acp-tree-group ${expanded ? 'is-open' : ''}" data-payroll="${bucket.key}">
                    <button type="button" class="acp-tree-header" data-toggle-payroll data-payroll="${bucket.key}"
                        aria-expanded="${expanded ? 'true' : 'false'}">
                        <iconify-icon class="acp-tree-caret" icon="${expanded ? 'ph:caret-down-bold' : 'ph:caret-right-bold'}"></iconify-icon>
                        <span class="acp-tree-dot"></span>
                        <iconify-icon class="acp-tree-icon" icon="${bucket.key === 'onroll' ? 'ph:briefcase-duotone' : 'ph:identification-card-duotone'}"></iconify-icon>
                        <span class="acp-tree-title">${bucket.label}</span>
                        <span class="acp-tree-meta">${bucketRows.length} groups</span>
                        <span class="acp-tree-count">${enabledInBucket}/${bucketRows.length}</span>
                    </button>
                    <div class="acp-tree-children" ${expanded ? '' : 'hidden'}>
                        ${bucketRows.map(function (item) {
                            return `
                                <label class="acp-tree-child">
                                    <div class="acp-tree-child-info">
                                        <iconify-icon icon="ph:squares-four-duotone"></iconify-icon>
                                        <span>${escapeHtml(item.groupDesc)}</span>
                                    </div>
                                    ${renderCheckbox({
                                        agentId: selectedAgent.agentId,
                                        groupCode: item.groupCode,
                                        payroll: item.payroll,
                                        isActive: item.isActive,
                                        canToggle: item.canToggle,
                                        isLocked: item.isLocked,
                                        scope: 'group'
                                    })}
                                </label>`;
                        }).join('')}
                    </div>
                </div>`;
        }).join('');
    }

    function renderCheckbox(options) {
        const disabled = options.canToggle ? '' : 'disabled';
        const checked = options.isActive ? 'checked' : '';
        const title = options.isLocked
            ? 'SupervisorAgent stays active'
            : (options.scope === 'group' ? 'Enable for this user group' : 'Activate this model');
        const attr = options.scope === 'model'
            ? `data-model-check data-agent-id="${options.agentId}"`
            : `data-group-check data-agent-id="${options.agentId}" data-group-code="${escapeHtml(options.groupCode)}" data-payroll="${escapeHtml(options.payroll)}"`;

        return `
            <input type="checkbox" class="acp-check ${options.isLocked ? 'is-locked' : ''}"
                ${attr} ${checked} ${disabled} title="${title}" aria-label="${title}" />`;
    }

    function toggleAllGroups(enable) {
        assignmentCells.forEach(function (item) {
            if (item.canToggle) {
                item.isActive = enable;
            }
        });
        renderGroupTree();
    }

    async function saveGroupAssignments(button) {
        if (!selectedAgent) {
            return;
        }

        const changes = assignmentCells.filter(function (item) {
            return item.canToggle && item.isActive !== item.originalActive;
        });

        if (!changes.length) {
            closeGroupModal();
            setStatus('No group assignment changes to save.');
            return;
        }

        const run = async function () {
            for (let i = 0; i < changes.length; i += 1) {
                const cell = changes[i];
                const response = await fetch(endpoints.updateActive, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
                    body: JSON.stringify({
                        agent_id: selectedAgent.agentId,
                        user_grp_code: cell.groupCode,
                        user_payroll: cell.payroll,
                        is_active: cell.isActive
                    })
                });

                const data = await readJson(response);
                if (!response.ok) {
                    throw new Error(data.error_message || `Unable to update ${cell.groupDesc}.`);
                }

                cell.originalActive = cell.isActive;
            }

            const agentName = selectedAgent.agentName;
            closeGroupModal();
            setStatus(`${agentName} group assignments saved.`);
        };

        try {
            if (button) {
                await loaders.runWithButtonLoading(button, run);
                return;
            }

            await run();
        } catch (error) {
            window.alert(error.message || 'Unable to save group assignments.');
            renderGroupTree();
        }
    }

    function emptyRow(colspan, message) {
        return `
            <tr class="acp-empty-row">
                <td colspan="${colspan}">
                    <div class="empty-state compact">
                        <div class="empty-state-icon">
                            <iconify-icon icon="ph:robot-duotone"></iconify-icon>
                        </div>
                        <h4>No models to display</h4>
                        <p>${escapeHtml(message)}</p>
                    </div>
                </td>
            </tr>`;
    }

    function setStatus(message) {
        if (elements.status) {
            elements.status.textContent = message;
        }
    }

    function setText(element, value) {
        if (element) {
            element.textContent = value == null ? '0' : String(value);
        }
    }

    async function readJson(response) {
        const text = await response.text();
        if (!text) {
            return {};
        }

        try {
            return JSON.parse(text);
        } catch {
            return { error_message: text };
        }
    }

    function escapeHtml(value) {
        return String(value || '')
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }

    init();
})();
