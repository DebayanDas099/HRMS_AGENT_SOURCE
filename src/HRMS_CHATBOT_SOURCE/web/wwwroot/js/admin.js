(function () {
    'use strict';

    const sidebar = document.getElementById('sidebar');
    const sidebarToggle = document.getElementById('sidebarToggle');
    const mobileToggle = document.getElementById('mobileToggle');
    const logoutBtn = document.getElementById('logoutBtn');

    if (window.HrmsAdminAuth) {
        window.HrmsAdminAuth.restoreSession();
    }

    if (sidebarToggle && sidebar) {
        sidebarToggle.addEventListener('click', function () {
            sidebar.classList.toggle('expanded');
            document.body.classList.toggle('sidebar-expanded');
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

    const currentPath = window.location.pathname.toLowerCase().replace(/\/$/, '');
    document.querySelectorAll('.sidebar-nav .nav-item').forEach(function (link) {
        const href = (link.getAttribute('href') || '').toLowerCase().replace(/\/$/, '');
        if (href && href !== '#' && (currentPath === href || currentPath.startsWith(href + '/'))) {
            link.classList.add('active');
        }
    });
})();
