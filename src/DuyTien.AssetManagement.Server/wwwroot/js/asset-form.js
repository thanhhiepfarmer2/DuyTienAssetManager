"use strict";
for (const form of document.querySelectorAll("form[data-asset-form]")) {
    form.addEventListener("submit", event => {
        if (event.defaultPrevented || !form.checkValidity()) return;
        form.setAttribute("aria-busy", "true");
        for (const button of form.querySelectorAll("button[type=submit]")) button.disabled = true;
        const status = form.querySelector("[data-save-status]");
        if (status) status.textContent = "Đang xác nhận dữ liệu… Vui lòng chờ kết quả.";
    });
}
window.addEventListener("pageshow", () => {
    for (const form of document.querySelectorAll("form[data-asset-form]")) {
        form.removeAttribute("aria-busy");
        for (const button of form.querySelectorAll("button[type=submit]")) button.disabled = button.dataset.connectionDisabled === "1";
        const status = form.querySelector("[data-save-status]");
        if (status) status.textContent = "";
    }
});
