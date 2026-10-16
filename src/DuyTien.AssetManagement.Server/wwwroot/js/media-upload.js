"use strict";
for (const form of document.querySelectorAll("form[data-media-upload]")) {
    const file = form.querySelector("input[type='file']"), status = form.querySelector("[data-media-status]"), preview = form.querySelector("[data-media-preview]");
    let objectUrl; const fileName = form.querySelector("[data-media-file-name]"), localPreview = form.querySelector("[data-media-local-preview]");
    function clearPreview() { if (objectUrl) URL.revokeObjectURL(objectUrl); objectUrl = undefined; if (preview) { preview.removeAttribute("src"); preview.hidden = true; } if (localPreview) { localPreview.removeAttribute("href"); localPreview.hidden = true; } if (fileName) fileName.textContent = ""; }
    file?.addEventListener("change", () => { clearPreview(); if (fileName && file.files[0]) fileName.textContent = file.files[0].name + " · " + Math.ceil(file.files[0].size / 1024) + " KB · chưa lưu"; if (localPreview && file.files[0]?.type === "application/pdf") { objectUrl = URL.createObjectURL(file.files[0]); localPreview.href = objectUrl; localPreview.hidden = false; } if (preview && file.files[0] && ["image/png", "image/jpeg"].includes(file.files[0].type)) { objectUrl = URL.createObjectURL(file.files[0]); preview.src = objectUrl; preview.hidden = false; } });
    form.querySelector("[data-media-cancel]")?.addEventListener("click", () => { file.value = ""; clearPreview(); status.textContent = "Đã hủy lựa chọn; chưa gửi tệp."; });
    form.addEventListener("submit", async event => {
        if (!window.fetch || !form.checkValidity()) return;
        event.preventDefault(); if (form.getAttribute("aria-busy") === "true") return;
        const data = new FormData(form), url = event.submitter?.formAction || form.action;
        const controls = [...form.querySelectorAll("input, textarea, select, button")].map(control => [control, control.disabled]);
        for (const [control] of controls) control.disabled = true;
        form.setAttribute("aria-busy", "true"); status.textContent = "Đang tải lên và đối soát, vui lòng chờ…";
        try {
            const response = await fetch(url, { method: "POST", body: data, headers: { Accept: "application/json" }, credentials: "same-origin" });
            const result = response.headers.get("content-type")?.includes("application/json") ? await response.json() : null;
            if (!response.ok || result?.success !== true) throw new Error(result?.message || "Tải tệp chưa hoàn tất; kiểm tra phiên/kết nối. Tệp và nội dung đang chọn được giữ để thử lại.");
            status.textContent = "Lưu đã được xác nhận. Đang tải dữ liệu mới…"; clearPreview(); form.dispatchEvent(new Event("duytien:saved", { bubbles: true })); window.location.reload();
        } catch (error) { status.textContent = error.message || "Mất kết nối; giữ tệp và nội dung và thử lại cùng yêu cầu."; }
        finally { for (const [control, disabled] of controls) { const blocked = document.body.dataset.writeBlocked === "true"; if (blocked && !disabled) control.dataset.connectionDisabled = "1"; control.disabled = disabled || blocked || control.dataset.connectionDisabled === "1"; } form.removeAttribute("aria-busy"); }
    });
    window.addEventListener("pagehide", clearPreview, { once: true });
}
