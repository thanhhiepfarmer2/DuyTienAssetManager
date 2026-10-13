"use strict";
for (const form of document.querySelectorAll("form[data-tree-change]")) {
    const kind = form.querySelector("[name='Form.Kind']");
    function fields() { for (const part of form.querySelectorAll("[data-kind]")) part.hidden = part.dataset.kind !== kind?.value; }
    kind?.addEventListener("change", fields); fields();
    const category = form.querySelector("[name='Form.CategoryId']");
    category?.addEventListener("change", () => { const option = category.selectedOptions[0]; for (const [name, key] of [["Name", "name"], ["SegmentCode", "code"], ["Definition", "definition"]]) { const input = form.querySelector(`[name='Form.${name}']`); if (input) input.value = option?.dataset[key] || ""; } });
    form.addEventListener("submit", () => { if (!form.checkValidity()) return; form.setAttribute("aria-busy", "true"); const status = form.querySelector("[data-tree-status]"); if (status) status.textContent = "Đang kiểm tra/đối soát tác động… Vui lòng chờ kết quả."; });
}
window.addEventListener("pageshow", () => { for (const form of document.querySelectorAll("form[data-tree-change]")) { form.removeAttribute("aria-busy"); const status = form.querySelector("[data-tree-status]"); if (status) status.textContent = ""; } });
