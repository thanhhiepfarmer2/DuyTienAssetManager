"use strict";
(() => {
    document.body.dataset.uiReady = "true";
    const menu = document.querySelector("[data-navigation-toggle]"), nav = document.getElementById("main-navigation");
    function closeMenu() { nav?.removeAttribute("data-open"); menu?.setAttribute("aria-expanded", "false"); }
    menu?.addEventListener("click", () => { const open = menu.getAttribute("aria-expanded") !== "true"; menu.setAttribute("aria-expanded", String(open)); nav?.toggleAttribute("data-open", open); });
    document.addEventListener("keydown", e => { if (e.key === "Escape" && nav?.hasAttribute("data-open")) { closeMenu(); menu?.focus(); } });
    document.querySelectorAll(".table-responsive").forEach(table => { table.tabIndex = 0; table.setAttribute("role", "region"); table.setAttribute("aria-label", table.querySelector("caption")?.textContent || "Bảng dữ liệu, có thể cuộn ngang"); });
    document.querySelectorAll("form[method='get']").forEach(form => form.addEventListener("submit", () => { form.setAttribute("aria-busy", "true"); }));
    const dirtyForms = new Set(); let navigatingWithSubmit = false;
    document.addEventListener("input", event => { const form = event.target.closest("form[method='post']"); if (form) dirtyForms.add(form); });
    document.addEventListener("change", event => { const form = event.target.closest("form[method='post']"); if (form) dirtyForms.add(form); });
    document.addEventListener("submit", event => { if (!event.defaultPrevented && event.target.checkValidity() && !event.target.hasAttribute("data-media-upload")) navigatingWithSubmit = true; });
    document.addEventListener("duytien:saved", event => dirtyForms.delete(event.target));
    window.addEventListener("beforeunload", event => { if (dirtyForms.size && !navigatingWithSubmit) { event.preventDefault(); event.returnValue = ""; } });
    window.addEventListener("pageshow", () => { navigatingWithSubmit = false; });
    const invalid = document.querySelector(".input-validation-error"); if (invalid) invalid.focus();
    document.querySelectorAll("form[data-confirm]").forEach(form => form.addEventListener("submit", e => { if (form.checkValidity() && !confirm(form.dataset.confirm)) { e.preventDefault(); e.stopImmediatePropagation(); } }));
    document.querySelectorAll("[data-confirm-action]").forEach(button => button.addEventListener("click", e => { if (button.form?.checkValidity() && !confirm(button.dataset.confirmAction)) { e.preventDefault(); e.stopImmediatePropagation(); } }));
    const identity = document.querySelector("[data-viewer]");
    const owner = identity ? [identity.dataset.workspace, identity.dataset.central, identity.dataset.viewer, identity.dataset.session, identity.dataset.authority].join(":") : null;
    const tree = document.querySelector("[data-category-tree]"), treeRows = [...(tree?.querySelectorAll("tbody tr[data-depth]") || [])];
    const storageKey = owner ? "dt-view:tree:" + owner + ":" + location.pathname + location.search : null;
    let closed = new Set();
    try { const saved = storageKey && JSON.parse(sessionStorage.getItem(storageKey) || "[]"); if (Array.isArray(saved)) closed = new Set(saved.filter(x => typeof x === "string").slice(0, 60)); } catch { /* Optional view state only. */ }
    function updateTree() {
        let hiddenBelow = null;
        treeRows.forEach(row => { const depth = Number(row.dataset.depth); if (hiddenBelow !== null && depth <= hiddenBelow) hiddenBelow = null; row.hidden = hiddenBelow !== null; const button = row.querySelector("[data-tree-toggle]"); const expanded = !closed.has(row.dataset.node); if (button) { button.setAttribute("aria-expanded", String(expanded)); button.textContent = expanded ? "−" : "+"; } if (!row.hidden && !expanded && button) hiddenBelow = depth; });
    }
    treeRows.forEach(row => row.querySelector("[data-tree-toggle]")?.addEventListener("click", () => { const id = row.dataset.node; if (closed.has(id)) closed.delete(id); else closed.add(id); updateTree(); try { if (storageKey) sessionStorage.setItem(storageKey, JSON.stringify([...closed].slice(0, 60))); } catch { /* No business data or credentials are stored. */ } }));
    updateTree();
    const selection = document.querySelector("[data-selection-status]");
    function countSelection() { if (selection) selection.textContent = "Đã chọn " + document.querySelectorAll('input[name="Form.AssetIds"]:checked, input[type="hidden"][name="Form.AssetIds"]').length + " tài sản; kiểm tra ảnh hưởng trước khi lưu."; }
    document.querySelectorAll('input[name="Form.AssetIds"]').forEach(input => input.addEventListener("change", countSelection)); countSelection();
})();
