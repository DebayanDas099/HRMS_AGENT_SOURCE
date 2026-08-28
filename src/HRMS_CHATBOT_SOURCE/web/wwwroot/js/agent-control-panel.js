(function () {
    'use strict';

    const loaders = window.AdminLoaders || {
        setGridLoading: function () {},
        setComponentLoading: function () {},
        setButtonLoading: function () {},
        runWithButtonLoading: function (_btn, task) { return Promise.resolve().then(task); }
    };

    const endpoints = {
        groups: '/Admin/AgentControlPanel/GetGroups',
        models: '/Admin/AgentControlPanel/GetModels',
        updateModel: '/Admin/AgentControlPanel/UpdateModelActive',
        assignments: '/Admin/AgentControlPanel/GetAssignments',
        updateActive: '/Admin/AgentControlPanel/UpdateActive'
    };

    const elements = {
        status: document.getElementById('acpStatus'),
        statTotalGroups: document.getElementById('statTotalGroups'),
        statTotalAgents: document.getElementById('statTotalAgents'),
        statActiveAgents: document.getElementById('statActiveAgents'),
        statInactiveAgents: document.getElementById('statInactiveAgents'),
        groupTabs: document.getElementById('acpGroupTabs'),
        refreshBtn: document.getElementById('refreshAgentsBtn'),
        tableTitle: document.getElementById('acpTableTitle'),
        tableSubtitle: document.getElementById('acpTableSubtitle'),
        countBadge: document.getElementById('acpCountBadge'),
        modelsCountBadge: document.getElementById('acpModelsCountBadge'),
        modelsBody: document.getElementById('modelsTableBody'),
        tableBody: document.getElementById('agentsTableBody'),
        statsGrid: document.getElementById('acpStatsGrid'),
        modelsPanel: document.getElementById('acpModelsPanel'),
        tablePanel: document.getElementById('acpTablePanel')
    };

    const agentIcons = {
        SupervisorAgent: 'ph:crown-duotone',
        LeaveApplicationAgent: 'ph:calendar-check-duotone',
        DocumentAgent: 'ph:folder-open-duotone',
        KnowledgeAgent: 'ph:book-open-duotone',
        CriticAgent: 'ph:shield-check-duotone'
    };

    let groups = [];
    let activeGroupCode = '';

    function init() {
        if (!elements.tableBody || !elements.modelsBody) {
            return;
        }

        elements.refreshBtn?.addEventListener('click', function () {
            void refreshPage(elements.refreshBtn);
        });

        elements.modelsBody.addEventListener('click', function (event) {
            const button = event.target.closest('[data-toggle-model]');
            if (button) {
                void toggleModel(button);
            }
        });

        elements.tableBody.addEventListener('click', function (event) {
            const button = event.target.closest('[data-toggle-group]');
            if (button) {
                void toggleGroup(button);
            }
        });

        void refreshPage();
    }

    async function refreshPage(button) {
        const run = async function () {
            loaders.setGridLoading(elements.statsGrid, true, 'Loading models...');
            loaders.setComponentLoading(elements.modelsPanel, true, 'Loading models...');
            loaders.setComponentLoading(elements.tablePanel, true, 'Loading groups...');
            try {
                await Promise.all([loadModels(), loadGroups()]);
                await loadAssignments();
            } finally {
                loaders.setGridLoading(elements.statsGrid, false);
                loaders.setComponentLoading(elements.modelsPanel, false);
                loaders.setComponentLoading(elements.tablePanel, false);
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

        renderModels(data.items || []);
    }

    async function loadGroups() {
        const response = await fetch(endpoints.groups, { headers: { Accept: 'application/json' } });
        const data = await readJson(response);
        if (!response.ok) {
            throw new Error(data.error_message || 'Unable to load user groups.');
        }

        groups = Array.isArray(data) ? data : [];
        if (!groups.some(function (group) { return group.grp_user_group_code === activeGroupCode; })) {
            activeGroupCode = groups[0]?.grp_user_group_code || '';
        }

        setText(elements.statTotalGroups, groups.length);
        renderGroupTabs();
        setStatus(groups.length
            ? 'Turn models on globally, then assign them to user groups.'
            : 'No active user groups were found.');
    }

    async function loadAssignments() {
        if (!activeGroupCode) {
            renderGroupEmpty('Select a user group to manage agent access.');
            return;
        }

        const url = `${endpoints.assignments}?user_grp_code=${encodeURIComponent(activeGroupCode)}`;
        const response = await fetch(url, { headers: { Accept: 'application/json' } });
        const data = await readJson(response);
        if (!response.ok) {
            throw new Error(data.error_message || 'Unable to load agent assignments.');
        }

        const group = groups.find(function (item) { return item.grp_user_group_code === activeGroupCode; });
        const groupLabel = group?.grp_user_group_desc || activeGroupCode;
        elements.tableTitle.textContent = `${groupLabel} assignments`;
        elements.tableSubtitle.textContent = `Enable globally active models for ${groupLabel}. SupervisorAgent stays on as the workflow coordinator.`;
        elements.countBadge.textContent = String(data.total_agents || 0);
        renderAssignments(data.items || []);
    }

    function renderGroupTabs() {
        if (!elements.groupTabs) {
            return;
        }

        if (!groups.length) {
            elements.groupTabs.innerHTML = '<span class="acp-agent-note">No user groups</span>';
            return;
        }

        elements.groupTabs.innerHTML = groups.map(function (group) {
            const isActive = group.grp_user_group_code === activeGroupCode;
            return `
                <button class="acp-group-tab ${isActive ? 'active' : ''}" type="button" role="tab"
                    aria-selected="${isActive ? 'true' : 'false'}"
                    data-group-code="${escapeHtml(group.grp_user_group_code)}">
                    <iconify-icon icon="ph:users-three-duotone"></iconify-icon>
                    <span>${escapeHtml(group.grp_user_group_desc || group.grp_user_group_code)}</span>
                </button>`;
        }).join('');

        elements.groupTabs.querySelectorAll('.acp-group-tab').forEach(function (tab) {
            tab.addEventListener('click', function () {
                const nextCode = tab.getAttribute('data-group-code') || '';
                if (nextCode === activeGroupCode) {
                    return;
                }

                activeGroupCode = nextCode;
                renderGroupTabs();
                void loadAssignmentsWithLoader();
            });
        });
    }

    async function loadAssignmentsWithLoader() {
        loaders.setComponentLoading(elements.tablePanel, true, 'Loading agents...');
        try {
            await loadAssignments();
        } catch (error) {
            setStatus(error.message || 'Unable to load agent assignments.');
            renderGroupEmpty(error.message || 'Unable to load agent assignments.');
        } finally {
            loaders.setComponentLoading(elements.tablePanel, false);
        }
    }

    function renderModels(items) {
        if (!items.length) {
            elements.modelsBody.innerHTML = emptyRow(4, 'No models are registered in agent master.');
            return;
        }

        elements.modelsBody.innerHTML = items.map(function (agent) {
            const isActive = String(agent.am_active || '').toUpperCase() === 'Y';
            const isLocked = String(agent.is_locked || '').toUpperCase() === 'Y';
            const canToggle = String(agent.can_toggle || '').toUpperCase() === 'Y';
            const roleClass = (agent.role || 'Specialist').toLowerCase();
            const icon = agentIcons[agent.am_name] || 'ph:robot-duotone';
            const note = isLocked ? 'Always on — workflow coordinator' : '';

            return `
                <tr data-model-row data-agent-id="${agent.am_id}">
                    <td>${renderAgentCell(agent.am_name, icon, note)}</td>
                    <td><span class="acp-role-pill ${escapeHtml(roleClass)}">${escapeHtml(agent.role || 'Specialist')}</span></td>
                    <td><span class="acp-flag-pill ${isActive ? 'is-on' : 'is-off'}">${isActive ? 'Active' : 'Inactive'}</span></td>
                    <td class="acp-actions-col">${renderStatusButton(agent.am_id, isActive, canToggle, isLocked, 'model')}</td>
                </tr>`;
        }).join('');
    }

    function renderAssignments(items) {
        if (!items.length) {
            renderGroupEmpty('No agents are registered in agent master.');
            return;
        }

        elements.tableBody.innerHTML = items.map(function (agent) {
            const isGroupActive = String(agent.aaug_active || '').toUpperCase() === 'Y';
            const isMasterActive = String(agent.am_active || '').toUpperCase() === 'Y';
            const isLocked = String(agent.is_locked || '').toUpperCase() === 'Y';
            const canToggle = String(agent.can_toggle || '').toUpperCase() === 'Y';
            const roleClass = (agent.role || 'Specialist').toLowerCase();
            const icon = agentIcons[agent.am_name] || 'ph:robot-duotone';
            const note = isLocked
                ? 'Always on — workflow coordinator'
                : (isMasterActive ? '' : 'Globally inactive');

            return `
                <tr data-agent-row data-agent-id="${agent.am_id}">
                    <td>${renderAgentCell(agent.am_name, icon, note)}</td>
                    <td><span class="acp-role-pill ${escapeHtml(roleClass)}">${escapeHtml(agent.role || 'Specialist')}</span></td>
                    <td><span class="acp-flag-pill ${isMasterActive ? 'is-on' : 'is-off'}">${isMasterActive ? 'Active' : 'Inactive'}</span></td>
                    <td><span class="acp-flag-pill ${isGroupActive ? 'is-on' : 'is-off'}">${isGroupActive ? 'Enabled' : 'Disabled'}</span></td>
                    <td class="acp-actions-col">${renderStatusButton(agent.am_id, isGroupActive, canToggle, isLocked, 'group')}</td>
                </tr>`;
        }).join('');
    }

    function renderAgentCell(name, icon, note) {
        return `
            <div class="acp-agent-cell">
                <div class="acp-agent-icon">
                    <iconify-icon icon="${icon}"></iconify-icon>
                </div>
                <div>
                    <span class="acp-agent-name">${escapeHtml(name)}</span>
                    ${note ? `<span class="acp-agent-note">${escapeHtml(note)}</span>` : ''}
                </div>
            </div>`;
    }

    function renderStatusButton(agentId, isActive, canToggle, isLocked, scope) {
        const meta = getStatusMeta(isActive, isLocked, scope);
        const disabled = canToggle ? '' : 'disabled';
        const scopeAttr = scope === 'model' ? 'data-toggle-model' : 'data-toggle-group';
        return `
            <button type="button" class="acp-action-btn acp-status-btn ${meta.className}"
                ${scopeAttr} data-agent-id="${agentId}" data-is-active="${isActive ? 'true' : 'false'}"
                title="${meta.title}" aria-label="${meta.ariaLabel}" ${disabled}>
                <iconify-icon icon="${meta.icon}"></iconify-icon>
            </button>`;
    }

    function getStatusMeta(isActive, isLocked, scope) {
        if (isLocked) {
            return {
                className: 'is-locked',
                title: 'SupervisorAgent stays active',
                ariaLabel: 'SupervisorAgent is locked on',
                icon: 'ph:lock-duotone'
            };
        }

        const forGroup = scope === 'group';
        return isActive
            ? {
                className: 'is-active',
                title: forGroup ? 'Deactivate agent for this group' : 'Deactivate this model',
                ariaLabel: forGroup ? 'Agent is enabled. Click to deactivate.' : 'Model is active. Click to deactivate.',
                icon: 'ph:check-circle-duotone'
            }
            : {
                className: 'is-inactive',
                title: forGroup ? 'Activate agent for this group' : 'Activate this model',
                ariaLabel: forGroup ? 'Agent is disabled. Click to activate.' : 'Model is inactive. Click to activate.',
                icon: 'ph:prohibit-duotone'
            };
    }

    async function toggleModel(button) {
        if (button.disabled) {
            return;
        }

        const agentId = Number(button.getAttribute('data-agent-id'));
        const newActive = button.getAttribute('data-is-active') !== 'true';

        button.disabled = true;
        loaders.setButtonLoading(button, true);

        try {
            const response = await fetch(endpoints.updateModel, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
                body: JSON.stringify({ agent_id: agentId, is_active: newActive })
            });

            const data = await readJson(response);
            if (!response.ok) {
                throw new Error(data.error_message || 'Unable to update model status.');
            }

            await loadModels();
            await loadAssignments();
            setStatus(newActive
                ? `${data.am_name || 'Model'} is now globally active.`
                : `${data.am_name || 'Model'} is now globally inactive.`);
        } catch (error) {
            window.alert(error.message || 'Unable to update model status.');
        } finally {
            loaders.setButtonLoading(button, false);
            button.disabled = false;
        }
    }

    async function toggleGroup(button) {
        if (button.disabled) {
            return;
        }

        const agentId = Number(button.getAttribute('data-agent-id'));
        const newActive = button.getAttribute('data-is-active') !== 'true';

        button.disabled = true;
        loaders.setButtonLoading(button, true);

        try {
            const response = await fetch(endpoints.updateActive, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
                body: JSON.stringify({
                    agent_id: agentId,
                    user_grp_code: activeGroupCode,
                    is_active: newActive
                })
            });

            const data = await readJson(response);
            if (!response.ok) {
                throw new Error(data.error_message || 'Unable to update agent status.');
            }

            await loadAssignments();
            setStatus(newActive
                ? `${data.am_name || 'Agent'} enabled for this group.`
                : `${data.am_name || 'Agent'} removed from this group's workflow.`);
        } catch (error) {
            window.alert(error.message || 'Unable to update agent status.');
        } finally {
            loaders.setButtonLoading(button, false);
            button.disabled = false;
        }
    }

    function renderGroupEmpty(message) {
        elements.tableBody.innerHTML = emptyRow(5, message);
    }

    function emptyRow(colspan, message) {
        return `
            <tr class="acp-empty-row">
                <td colspan="${colspan}">
                    <div class="empty-state compact">
                        <div class="empty-state-icon">
                            <iconify-icon icon="ph:robot-duotone"></iconify-icon>
                        </div>
                        <h4>No agents to display</h4>
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
