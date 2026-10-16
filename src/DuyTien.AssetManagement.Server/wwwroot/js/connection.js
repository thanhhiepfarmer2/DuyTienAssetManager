(() => {
    let dirty = false, refreshing = false, refreshAttempts = 0, nextRefresh = 0;
    window.addEventListener('pageshow', e => { if (e.persisted) location.reload(); });
    document.addEventListener('input', e => { if (e.target.closest('form[method="post"]')) dirty = true; });
    function setReadOnly(blocked) {
        document.body.dataset.writeBlocked = String(blocked);
        document.querySelectorAll('form[method="post"] button, form[method="post"] input:not([type="hidden"]), form[method="post"] select, form[method="post"] textarea').forEach(el => {
            if (el.closest('form').action.includes('/Account/Logout')) return;
            if (blocked && !el.disabled) { el.disabled = true; el.dataset.connectionDisabled = '1'; }
            if (!blocked && el.dataset.connectionDisabled) { if (el.closest('form').getAttribute('aria-busy') !== 'true') el.disabled = false; delete el.dataset.connectionDisabled; }
        });
    }
    if (document.body.dataset.connectionState === 'OFFLINE_READ_ONLY') setReadOnly(true);
    async function poll() {
        try {
            const response = await fetch('/api/connection', { cache: 'no-store' });
            if (!response.ok) return;
            const state = await response.json();
            const identity = document.querySelector('[data-viewer]');
            if (identity && identity.dataset.viewer !== 'anonymous' && !location.pathname.startsWith('/Account/') && (!state.hasOfflineSession || identity.dataset.viewer !== state.viewerId || identity.dataset.session !== state.sessionId || (identity.dataset.central && identity.dataset.central !== state.cacheId) || (identity.dataset.authority && identity.dataset.authority !== state.authorityHash))) {
                document.getElementById('main-content')?.replaceChildren();
                location.replace('/Account/Login'); return;
            }
            let banner = document.getElementById('connection-state');
            if (!banner) { banner = document.createElement('aside'); banner.id = 'connection-state'; banner.setAttribute('role', 'status'); document.body.prepend(banner); }
            const labels = { ONLINE: 'Trực tuyến — kiểm tra dữ liệu từ trung tâm khi mở nội dung', OFFLINE_READ_ONLY: 'Ngoại tuyến — chỉ đọc. Chỉ dữ liệu/file đã lưu trên máy.', RECONNECTING: 'Đang kết nối lại — xác minh phiên, quyền và dữ liệu mới', SERVICE_UNAVAILABLE: 'Chưa xác minh được dịch vụ/kết nối — chưa thể ghi' };
            banner.dataset.state = state.state;
            banner.textContent = (labels[state.state] || state.state) + (state.lastSyncedAt ? ' · Bản lưu gần nhất: ' + state.lastSyncedAt : ' · Chưa có bản lưu ngoại tuyến') + (state.pendingWrite ? (state.writeRetryPermitted ? ' · Đã đối soát: có thể thử lại yêu cầu đang giữ.' : ' · Kết quả lưu chưa rõ: giữ nội dung, đối soát trước khi thử lại.') : '');
            if (state.pendingWrite && !state.writeRetryPermitted && state.state === 'ONLINE') {
                const button = document.createElement('button'); button.type = 'button'; button.textContent = 'Đối soát kết quả ghi';
                button.onclick = async () => {
                    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
                    if (!token) { banner.append(' Chưa thể đối soát trên trang này; mở lại khi trực tuyến.'); return; }
                    button.disabled = true;
                    try { const result = await fetch('/api/connection/reconcile', { method: 'POST', headers: { RequestVerificationToken: token } }); const data = await result.json(); banner.append(' ' + (data.title || data.state)); }
                    catch { banner.append(' Chưa nhận được kết quả; giữ nguyên nội dung.'); }
                    finally { button.disabled = false; }
                };
                banner.append(button);
            }
            if (state.state === 'OFFLINE_READ_ONLY') {
                const result = await fetch('/api/connection/files', { cache: 'no-store' });
                if (result.ok) {
                    const files = (await result.json()).files;
                    document.querySelectorAll('a[href*="/api/files/"], a[href*="/Files/Content/"]').forEach(link => {
                        const cached = files.find(f => f.url === new URL(link.href).pathname + new URL(link.href).search && f.offlineAvailable);
                        link.title = cached ? 'Bản lưu cho liên kết này đã sẵn sàng' : 'Chưa có bản lưu cho liên kết này — cần Internet';
                        let label = link.nextElementSibling;
                        if (!label?.hasAttribute('data-offline-label')) { label = document.createElement('small'); label.setAttribute('data-offline-label', '1'); link.after(label); }
                        label.textContent = cached ? ' · Có bản lưu ngoại tuyến' : ' · Chưa có bản lưu ngoại tuyến';
                    });
                }
            }
            const readOnly = (state.state !== 'ONLINE' || (state.pendingWrite && !state.writeRetryPermitted)) && !location.pathname.startsWith('/Account/');
            setReadOnly(readOnly);
            if (['RECONNECTING', 'SERVICE_UNAVAILABLE'].includes(state.state) && state.hasOfflineSession && !refreshing && Date.now() >= nextRefresh && !location.pathname.startsWith('/Account/')) {
                refreshing = true;
                nextRefresh = Date.now() + Math.min(60000, 5000 * (2 ** Math.min(refreshAttempts++, 4)));
                const fresh = await fetch(state.refreshPath, { cache: 'no-store', headers: { 'X-DuyTien-Reconnect': '1' } });
                await fresh.text();
                if (fresh.ok && fresh.headers.get('X-DuyTien-State') === 'ONLINE') { refreshAttempts = 0; if (!dirty) location.assign(location.pathname === '/Offline' ? state.refreshPath : location.href); }
                refreshing = false;
            }
        } catch { refreshing = false; /* backend unavailable differs from Internet loss; preserve form and never replay writes */ }
        finally { setTimeout(poll, 5000); }
    }
    poll();
})();
