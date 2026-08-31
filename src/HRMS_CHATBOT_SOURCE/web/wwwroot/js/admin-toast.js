(function () {
    'use strict';

    const ICONS = {
        success: 'ph:check-circle-duotone',
        error: 'ph:warning-circle-duotone',
        info: 'ph:info-duotone'
    };

    function ensureContainer() {
        let container = document.querySelector('.admin-toast-stack');
        if (!container) {
            container = document.createElement('div');
            container.className = 'admin-toast-stack';
            container.setAttribute('role', 'status');
            container.setAttribute('aria-live', 'polite');
            document.body.appendChild(container);
        }

        return container;
    }

    function dismiss(toast) {
        if (!toast || toast.dataset.dismissing === 'true') {
            return;
        }

        toast.dataset.dismissing = 'true';
        toast.classList.add('is-leaving');
        window.setTimeout(function () {
            toast.remove();
        }, 220);
    }

    function show(message, type, duration) {
        if (!message) {
            return null;
        }

        const variant = ICONS[type] ? type : 'info';
        const container = ensureContainer();
        const toast = document.createElement('div');
        toast.className = `admin-toast ${variant}`;

        const icon = document.createElement('iconify-icon');
        icon.setAttribute('icon', ICONS[variant]);
        icon.className = 'admin-toast-icon';

        const text = document.createElement('p');
        text.className = 'admin-toast-text';
        text.textContent = message;

        const close = document.createElement('button');
        close.type = 'button';
        close.className = 'admin-toast-close';
        close.setAttribute('aria-label', 'Dismiss notification');
        close.innerHTML = '<iconify-icon icon="ph:x-bold"></iconify-icon>';
        close.addEventListener('click', function () {
            dismiss(toast);
        });

        toast.append(icon, text, close);
        container.appendChild(toast);

        window.setTimeout(function () {
            dismiss(toast);
        }, duration || 4000);

        return toast;
    }

    window.AdminToast = {
        show: show,
        success: function (message, duration) { return show(message, 'success', duration); },
        error: function (message, duration) { return show(message, 'error', duration || 6000); },
        info: function (message, duration) { return show(message, 'info', duration); }
    };
})();
