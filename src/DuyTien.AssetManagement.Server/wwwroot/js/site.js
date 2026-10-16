"use strict";
// Native POST stays native: preserve submitter/formaction and every form field for anti-forgery and retries.
for (const form of document.querySelectorAll("form[method='post']")) {
    form.addEventListener("submit", event => {
        if (!form.checkValidity() || event.defaultPrevented) return;
        if (form.dataset.nativePending === "true") { event.preventDefault(); return; }
        // AJAX upload owns its own pending/finally state and keeps File objects on errors.
        if (form.hasAttribute("data-media-upload")) return;
        form.dataset.nativePending = "true";
        form.setAttribute("aria-busy", "true");
        let status = form.querySelector("[data-submit-status], [data-save-status], [data-tree-status]");
        if (!status) { status = document.createElement("p"); status.setAttribute("role", "status"); status.setAttribute("aria-live", "polite"); status.dataset.nativeStatus = "true"; form.append(status); }
        status.textContent = "Đang xử lý… Chờ kết quả; không gửi yêu cầu khác khi chưa xác nhận.";
    });
}
window.addEventListener("pageshow", () => {
    for (const form of document.querySelectorAll("form[data-native-pending]")) {
        delete form.dataset.nativePending; form.removeAttribute("aria-busy");
        const status = form.querySelector("[data-native-status]"); if (status) status.textContent = "";
    }
});

// Keep only navigation state, never records/tokens/forms/passwords in web storage.
try {
const workspaceBanner = document.querySelector("[data-workspace]");
if (workspaceBanner) {
    const owner = [workspaceBanner.dataset.workspace, workspaceBanner.dataset.central, workspaceBanner.dataset.authority, workspaceBanner.dataset.viewer, workspaceBanner.dataset.session].join(":");
    if (sessionStorage.getItem("dt-view-owner") !== owner) { for (const k of Object.keys(sessionStorage)) if (k.startsWith("dt-view:")) sessionStorage.removeItem(k); sessionStorage.setItem("dt-view-owner", owner); }
    const key = "dt-view:" + owner + ":" + location.pathname + location.search;
    const saved = Number(sessionStorage.getItem(key)); if (Number.isFinite(saved) && saved > 0) requestAnimationFrame(() => window.scrollTo(0, saved));
    window.addEventListener("pagehide", () => sessionStorage.setItem(key, String(window.scrollY)));
}

} catch (error) {
    // Navigation state is optional when browser storage is denied; never cache business data.
    if (!(error instanceof DOMException)) throw error;
}
