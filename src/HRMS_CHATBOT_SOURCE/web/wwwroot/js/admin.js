(function () {
    'use strict';

    const sidebar = document.getElementById('sidebar');
    const sidebarToggle = document.getElementById('sidebarToggle');
    const mobileToggle = document.getElementById('mobileToggle');
    const logoutBtn = document.getElementById('logoutBtn');
    const themeToggle = document.getElementById('themeToggle');
    const THEME_STORAGE_KEY = 'hrms-admin-theme';

    function applyTheme(isDark) {
        document.documentElement.classList.toggle('theme-dark', isDark);
        if (themeToggle) {
            themeToggle.checked = isDark;
        }
        localStorage.setItem(THEME_STORAGE_KEY, isDark ? 'dark' : 'light');
    }

    if (themeToggle) {
        themeToggle.checked = document.documentElement.classList.contains('theme-dark');
        themeToggle.addEventListener('change', function () {
            applyTheme(themeToggle.checked);
        });
    }

    if (window.HrmsAdminAuth) {
        window.HrmsAdminAuth.restoreSession();
    }

    if (sidebarToggle && sidebar) {
        sidebarToggle.addEventListener('click', function () {
            const isExpanded = sidebar.classList.toggle('expanded');
            document.body.classList.toggle('sidebar-expanded', isExpanded);
            sidebarToggle.setAttribute('aria-expanded', isExpanded ? 'true' : 'false');
        });
    }

    if (mobileToggle && sidebar) {
        mobileToggle.addEventListener('click', function () {
            sidebar.classList.toggle('mobile-open');
        });
    }

    if (logoutBtn) {
        logoutBtn.addEventListener('click', function (e) {
            e.preventDefault();
            window.HrmsAdminAuth?.logout();
        });
    }

    document.addEventListener('click', function (e) {
        if (window.innerWidth <= 992 && sidebar?.classList.contains('mobile-open')) {
            if (!sidebar.contains(e.target) && !mobileToggle?.contains(e.target)) {
                sidebar.classList.remove('mobile-open');
            }
        }
    });

    const cards = document.querySelectorAll('.stat-card, .panel-card');
    const observer = new IntersectionObserver(function (entries) {
        entries.forEach(function (entry) {
            if (entry.isIntersecting) {
                entry.target.classList.add('visible');
            }
        });
    }, { threshold: 0.1 });

    cards.forEach(function (card) { observer.observe(card); });

    hydrateTopbarUser();
    void hydrateNotificationBell();

    const currentPath = window.location.pathname.toLowerCase().replace(/\/$/, '');
    document.querySelectorAll('.sidebar-nav .nav-item').forEach(function (link) {
        const href = (link.getAttribute('href') || '').toLowerCase().replace(/\/$/, '');
        if (href && href !== '#' && (currentPath === href || currentPath.startsWith(href + '/'))) {
            link.classList.add('active');
        }
    });
})();

async function hydrateNotificationBell() {
    const dot = document.getElementById('notifDot');
    if (!dot) {
        return;
    }

    try {
        const response = await fetch('/Admin/Approvals/PendingCount', { headers: { Accept: 'application/json' } });
        if (!response.ok) {
            return;
        }

        const count = await response.json();
        dot.hidden = !(Number(count) > 0);
    } catch (_) {
        /* the bell just stays as-is if this check fails */
    }
}

function hydrateTopbarUser() {
    const nameEl = document.getElementById('topbarUserName');
    const avatarEl = document.getElementById('topbarUserAvatar');
    if (!nameEl || !window.HrmsAdminAuth) {
        return;
    }

    const currentName = (nameEl.textContent || '').trim();
    if (currentName && currentName.toLowerCase() !== 'admin') {
        return;
    }

    const token = window.HrmsAdminAuth.getToken();
    if (!token) {
        return;
    }

    try {
        const payloadPart = token.split('.')[1];
        if (!payloadPart) {
            return;
        }

        const payload = JSON.parse(atob(payloadPart.replace(/-/g, '+').replace(/_/g, '/')));
        const userName = payload.UserName || payload.userName || payload.name;
        if (!userName) {
            return;
        }

        nameEl.textContent = userName;

        if (avatarEl) {
            const initials = userName
                .trim()
                .split(/\s+/)
                .filter(Boolean)
                .slice(0, 2)
                .map(function (part) { return part[0].toUpperCase(); })
                .join('');

            avatarEl.textContent = initials || userName.slice(0, 2).toUpperCase();
        }
    } catch (_) {
        /* ignore malformed token */
    }
}
