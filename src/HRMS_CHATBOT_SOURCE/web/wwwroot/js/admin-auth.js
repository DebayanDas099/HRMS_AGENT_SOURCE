(function () {
    'use strict';

    const TOKEN_KEY = 'hrms_admin_token';
    const REMEMBER_KEY = 'hrms_admin_remember';
    const COOKIE_NAME = 'hrms_admin_token';

    window.HrmsAdminAuth = {
        TOKEN_KEY,
        REMEMBER_KEY,
        COOKIE_NAME,

        getStorage: function () {
            return localStorage.getItem(REMEMBER_KEY) === 'true' ? localStorage : sessionStorage;
        },

        getToken: function () {
            return this.getStorage().getItem(TOKEN_KEY) || '';
        },

        saveToken: function (token, rememberMe) {
            localStorage.setItem(REMEMBER_KEY, rememberMe ? 'true' : 'false');
            sessionStorage.removeItem(TOKEN_KEY);
            localStorage.removeItem(TOKEN_KEY);

            const storage = rememberMe ? localStorage : sessionStorage;
            storage.setItem(TOKEN_KEY, token);
            this.syncCookie(token, rememberMe);
        },

        clearToken: function () {
            localStorage.removeItem(TOKEN_KEY);
            sessionStorage.removeItem(TOKEN_KEY);
            localStorage.removeItem(REMEMBER_KEY);
            document.cookie = COOKIE_NAME + '=; Path=/; Expires=Thu, 01 Jan 1970 00:00:00 GMT; SameSite=Lax';
        },

        syncCookie: function (token, rememberMe) {
            if (!token) return;

            const maxAge = rememberMe ? 7 * 24 * 60 * 60 : 8 * 60 * 60;
            const secure = location.protocol === 'https:' ? '; Secure' : '';
            document.cookie = `${COOKIE_NAME}=${token}; Path=/; Max-Age=${maxAge}; SameSite=Lax${secure}`;
        },

        restoreSession: function () {
            const token = this.getToken();
            if (token) {
                const rememberMe = localStorage.getItem(REMEMBER_KEY) === 'true';
                this.syncCookie(token, rememberMe);
            }
            return token;
        },

        logout: async function () {
            const token = this.getToken();
            try {
                if (token) {
                    await fetch('/Admin/Account/LogoutPost', {
                        method: 'POST',
                        headers: { 'Authorization': 'Bearer ' + token }
                    });
                }
            } catch (_) { /* ignore */ }

            this.clearToken();
            window.location.href = '/Admin/Account/Login';
        }
    };

    if (!window.location.pathname.toLowerCase().includes('/admin/account/login')) {
        window.HrmsAdminAuth.restoreSession();
    }
})();
