(function () {
    'use strict';

    function ensureGridOverlay(container) {
        let overlay = container.querySelector(':scope > .admin-loader-grid');
        if (overlay) {
            return overlay;
        }

        overlay = document.createElement('div');
        overlay.className = 'admin-loader-grid';
        overlay.hidden = true;
        overlay.innerHTML = `
            <div class="loader-grid-mesh" aria-hidden="true"></div>
            <div class="loader-component-panel">
                <div class="loader-orb" aria-hidden="true"></div>
                <p class="loader-status-text">Loading data...</p>
            </div>`;
        container.appendChild(overlay);
        return overlay;
    }

    function ensureComponentOverlay(container, message) {
        let overlay = container.querySelector(':scope > .admin-loader-component');
        if (!overlay) {
            overlay = document.createElement('div');
            overlay.className = 'admin-loader-component';
            overlay.innerHTML = `
                <div class="loader-component-panel">
                    <div class="loader-orb" aria-hidden="true"></div>
                    <p class="loader-status-text"></p>
                </div>`;
            container.appendChild(overlay);
        }

        const text = overlay.querySelector('.loader-status-text');
        if (text) {
            text.textContent = message || 'Loading...';
        }

        return overlay;
    }

    function buildSkeletonCell(index) {
        if (index === 0 || index === 6) {
            return '<span class="skeleton-block circle"></span>';
        }

        if (index === 1) {
            return '<span class="skeleton-block wide"></span>';
        }

        if (index === 3) {
            return '<span class="skeleton-block short"></span><span class="skeleton-block wide" style="margin-top:0.35rem"></span>';
        }

        return '<span class="skeleton-block"></span>';
    }

    window.AdminLoaders = {
        setGridLoading: function (container, loading, message) {
            if (!container) {
                return;
            }

            container.classList.add('loader-grid-host');
            container.classList.toggle('is-grid-loading', !!loading);
            container.setAttribute('aria-busy', loading ? 'true' : 'false');

            const overlay = ensureGridOverlay(container);
            const text = overlay.querySelector('.loader-status-text');
            if (text && message) {
                text.textContent = message;
            }

            overlay.hidden = !loading;
        },

        setComponentLoading: function (container, loading, message) {
            if (!container) {
                return;
            }

            container.classList.add('loader-component-host');
            container.classList.toggle('is-component-loading', !!loading);
            container.setAttribute('aria-busy', loading ? 'true' : 'false');

            const overlay = ensureComponentOverlay(container, message);
            overlay.hidden = !loading;
        },

        renderTableSkeleton: function (tbody, columnCount, rowCount) {
            if (!tbody) {
                return;
            }

            const rows = rowCount || 5;
            const cols = columnCount || 7;
            const html = [];

            for (let row = 0; row < rows; row++) {
                html.push('<tr class="skeleton-row">');
                for (let col = 0; col < cols; col++) {
                    html.push(`<td>${buildSkeletonCell(col)}</td>`);
                }
                html.push('</tr>');
            }

            tbody.innerHTML = html.join('');
        },

        setButtonLoading: function (button, loading) {
            if (!button) {
                return;
            }

            button.classList.toggle('is-btn-loading', !!loading);
            button.setAttribute('aria-busy', loading ? 'true' : 'false');

            if (loading) {
                if (button.dataset.loaderLocked !== 'true') {
                    button.dataset.loaderPrevDisabled = button.disabled ? 'true' : 'false';
                }
                button.dataset.loaderLocked = 'true';
                button.disabled = true;
                return;
            }

            if (button.dataset.loaderLocked === 'true') {
                button.disabled = button.dataset.loaderPrevDisabled === 'true';
                delete button.dataset.loaderLocked;
                delete button.dataset.loaderPrevDisabled;
            }
        },

        runWithButtonLoading: function (button, task) {
            this.setButtonLoading(button, true);
            return Promise.resolve()
                .then(task)
                .finally(function () {
                    window.AdminLoaders.setButtonLoading(button, false);
                });
        }
    };
})();
