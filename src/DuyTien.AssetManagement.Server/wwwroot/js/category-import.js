"use strict";
for (const form of document.querySelectorAll("form[data-category-import]")) {
    form.addEventListener("submit", () => {
        const status = document.getElementById("category-import-status");
        if (status) status.textContent = form.dataset.categoryImport === "preview"
            ? "Đang kiểm tra file và chuẩn bị preview…"
            : "Đang lưu phiên bản DRAFT. Vui lòng chờ kết quả…";
        form.setAttribute("aria-busy", "true");
        for (const button of form.querySelectorAll("button[type=submit], button:not([type])")) button.disabled = true;
    });
}
