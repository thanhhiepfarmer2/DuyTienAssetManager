"use strict";
document.querySelectorAll("[data-report-filter]").forEach(form => {
    let busy = false;
    form.addEventListener("submit", event => {
        if (busy) { event.preventDefault(); return; }
        if (!form.checkValidity()) return;
        busy = true;
        form.setAttribute("aria-busy", "true");
        const status = form.querySelector("[data-report-status]");
        if (status) status.textContent = "Đang đọc báo cáo theo bộ lọc và quyền hiện hành…";
    });
    window.addEventListener("pageshow", () => { busy = false; form.removeAttribute("aria-busy"); });
});
