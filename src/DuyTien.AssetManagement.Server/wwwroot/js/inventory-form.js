"use strict";
// Keep the successful submitter and request fields in the native POST, then prevent a second submission.
document.querySelectorAll("[data-inventory-form]").forEach(form => {
    let submitting = false;
    form.addEventListener("submit", event => {
        if (submitting) { event.preventDefault(); return; }
        if (!form.checkValidity()) return;
        submitting = true;
        form.setAttribute("aria-busy", "true");
        const status = form.querySelector("[data-submit-status]");
        if (status) status.textContent = "Đang xử lý. Chờ response để xác nhận checkpoint; chưa coi là đã lưu.";
        // Do not disable controls: disabled inputs/submitter are omitted from POST.
    });
    window.addEventListener("pageshow", () => { submitting = false; form.removeAttribute("aria-busy"); });
});
