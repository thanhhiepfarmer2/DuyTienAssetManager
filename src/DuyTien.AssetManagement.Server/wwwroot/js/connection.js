(() => {
    let dirty = false, refreshing = false, refreshAttempts = 0, nextRefresh = 0;
    window.addEventListener('pageshow', e => { if (e.persisted) location.reload(); });
    document.addEventListener('input', e => { if (e.target.closest('form[method="post"]')) dirty = true; });
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
            const labels = { ONLINE: 'Trực tuyến — dữ liệu được đọc lại từ trung tâm cho từng yêu cầu', OFFLINE_READ_ONLY: 'Ngoại tuyến — chỉ đọc. Chỉ dữ liệu/file đã lưu trên máy.', RECONNECTING: 'Đang kết nối lại — xác minh phiên, quyền và dữ liệu mới', SERVICE_UNAVAILABLE: 'Chưa xác minh được dịch vụ/kết nối — chưa thể ghi' };
            banner.textContent = (labels[state.state] || state.state) + (state.lastSyncedAt ? ' · Lần lưu cache gần nhất: ' + state.lastSyncedAt : ' · Chưa có dữ liệu offline') + (state.pendingWrite ? (state.writeRetryPermitted ? ' · Journal đã xác nhận: chỉ retry form/key/payload cũ.' : ' · Kết quả ghi chưa rõ: giữ form/key, cần đối soát online trước retry.') : '');
            if (state.pendingWrite && !state.writeRetryPermitted && state.state === 'ONLINE') {
                const button = document.createElement('button'); button.type = 'button'; button.textContent = 'Đối soát kết quả ghi';
                button.onclick = async () => {
                    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
                    if (!token) { banner.append(' Chưa có anti-forgery token; tải lại trang online để đối soát.'); return; }
                    button.disabled = true;
                    try { const result = await fetch('/api/connection/reconcile', { method: 'POST', headers: { RequestVerificationToken: token } }); const data = await result.json(); banner.append(' ' + (data.title || data.state)); }
                    catch { banner.append(' Chưa nhận được kết quả; giữ nguyên form/key.'); }
                    finally { button.disabled = false; }
                };
                banner.append(button);
            }
            if (state.state === 'OFFLINE_READ_ONLY') {
                const result = await fetch('/api/connection/files', { cache: 'no-store' });
                if (result.ok) {
                    const files = (await result.json()).files;
                    document.querySelectorAll('a[href*="/api/files/"]').forEach(link => {
                        const cached = files.find(f => f.url === new URL(link.href).pathname && f.offlineAvailable);
                        link.title = cached ? 'Bytes đã lưu offline' : 'Chưa có bytes offline — cần Internet để mở';
                        let label = link.nextElementSibling;
                        if (!label?.hasAttribute('data-offline-label')) { label = document.createElement('small'); label.setAttribute('data-offline-label', '1'); link.after(label); }
                        label.textContent = cached ? ' · Có offline' : ' · Chưa có file offline';
                    });
                }
            }
            document.querySelectorAll('form[method="post"] button, form[method="post"] input:not([type="hidden"]), form[method="post"] select, form[method="post"] textarea').forEach(el => {
                if (el.closest('form').action.includes('/Account/Logout')) return;
                const disabled = (state.state !== 'ONLINE' || (state.pendingWrite && !state.writeRetryPermitted)) && !location.pathname.startsWith('/Account/');
                if (disabled && !el.disabled) { el.disabled = true; el.dataset.connectionDisabled = '1'; }
                if (!disabled && el.dataset.connectionDisabled) { el.disabled = false; delete el.dataset.connectionDisabled; }
            });
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
