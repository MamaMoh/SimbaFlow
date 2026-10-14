/**
 * The two functions that run inside the portal's own page.
 *
 * Kept apart from the popup because they are injected, not called: Chrome sends the source of
 * these across and runs them in the page, where nothing from this extension exists. They can
 * therefore reference nothing outside themselves — no imports, no constants from the popup —
 * and keeping them in their own file is what makes that rule visible rather than a trap.
 *
 * It also makes them testable against a saved copy of a real form, which is the only honest way
 * to find out whether a label resolver works.
 */

function collectLabelsOnPage() {
  const seen = new Set();
  const add = (raw) => {
    const s = String(raw || "")
      .replace(/\s+/g, " ")
      .replace(/[*:]/g, "")
      .trim()
      .toLowerCase();
    if (s && s.length <= 60) seen.add(s);
  };
  document.querySelectorAll("label, legend, th").forEach((el) => add(el.textContent));
  document.querySelectorAll("input, select, textarea").forEach((el) => {
    add(el.getAttribute("placeholder"));
    add(el.getAttribute("aria-label"));
    add((el.getAttribute("name") || "").replace(/[_\-.]+/g, " "));
  });
  return Array.from(seen);
}

function fillOnPage(steps) {
  const norm = (s) =>
    String(s || "")
      .replace(/[*:]/g, " ")
      .replace(/\s+/g, " ")
      .trim()
      .toLowerCase();

  // Never a checkbox, a radio, a button or a file box. The declarations on a visa application
  // are a person's attestation, and the photo has rules this knows nothing about.
  const fillable = Array.from(document.querySelectorAll("input, select, textarea")).filter(
    // Read-only boxes stay in: a custom dropdown is very often a read-only text input, and a
    // box we cannot type into is worth telling the person about by name rather than reporting
    // as missing from a page it is plainly on.
    (el) =>
      !el.disabled &&
      !["hidden", "submit", "button", "reset", "checkbox", "radio", "file", "image"].includes(
        el.type,
      ),
  );

  const labelsOf = (el) => {
    const out = [];
    if (el.id)
      document
        .querySelectorAll(`label[for="${CSS.escape(el.id)}"]`)
        .forEach((l) => out.push(l.textContent));
    const wrapping = el.closest("label");
    if (wrapping) out.push(wrapping.textContent);
    out.push(el.getAttribute("aria-label"));
    const by = el.getAttribute("aria-labelledby");
    if (by)
      by.split(/\s+/).forEach((id) => {
        const n = document.getElementById(id);
        if (n) out.push(n.textContent);
      });
    out.push(el.getAttribute("placeholder"));
    out.push((el.getAttribute("name") || "").replace(/[_\-.]+/g, " "));

    // These portals print the label beside or above the box and tie the two together with
    // nothing at all — no `for`, no aria. So the text just before the box counts as its name,
    // whether it sits next to the box itself or next to the group the box is in.
    //
    // Anything containing a control is skipped on the way back: the previous field's own value
    // is not this field's label, and treating it as one puts the passport number into the box
    // underneath it.
    const isControl = (n) =>
      ["INPUT", "SELECT", "TEXTAREA"].includes(n.tagName) ||
      n.querySelector?.("input, select, textarea");

    const backFrom = (node) => {
      let prev = node?.previousElementSibling;
      for (let hops = 0; prev && hops < 3; hops += 1) {
        if (!isControl(prev)) out.push(prev.textContent);
        prev = prev.previousElementSibling;
      }
    };

    backFrom(el);
    backFrom(el.closest("td, .form-group, .field, .col, div"));

    return out.map(norm).filter(Boolean);
  };

  const indexed = fillable.map((el) => ({ el, labels: labelsOf(el) }));
  const used = new Set();

  const find = (label) => {
    const want = norm(label);
    const free = indexed.filter((c) => !used.has(c.el));
    const hit =
      free.find((c) => c.labels.includes(want)) ||
      free.find((c) => c.labels.some((l) => l.startsWith(want))) ||
      free.find((c) => c.labels.some((l) => l.includes(want)));
    return hit?.el ?? null;
  };

  // React and Angular listen for the events a real keystroke raises, not for `.value =`.
  const put = (el, value) => {
    const proto =
      el instanceof HTMLTextAreaElement
        ? HTMLTextAreaElement.prototype
        : el instanceof HTMLSelectElement
          ? HTMLSelectElement.prototype
          : HTMLInputElement.prototype;
    const setter = Object.getOwnPropertyDescriptor(proto, "value")?.set;
    if (setter) setter.call(el, value);
    else el.value = value;
    el.dispatchEvent(new Event("input", { bubbles: true }));
    el.dispatchEvent(new Event("change", { bubbles: true }));
  };

  const choose = (el, spellings) => {
    const options = Array.from(el.options);
    for (const want of spellings) {
      const w = norm(want);
      if (!w) continue;
      const hit =
        options.find((o) => norm(o.textContent) === w) ||
        options.find((o) => norm(o.value) === w) ||
        options.find((o) => norm(o.textContent).startsWith(w));
      if (hit) {
        el.value = hit.value;
        el.dispatchEvent(new Event("change", { bubbles: true }));
        return true;
      }
    }
    return false;
  };

  const results = [];
  for (const step of steps) {
    const el = find(step.label);
    if (!el) {
      results.push({ label: step.label, status: "notfound" });
      continue;
    }
    used.add(el);

    if (step.keepTheirs && String(el.value || "").trim()) {
      results.push({ label: step.label, status: "kept" });
      continue;
    }

    if (el.tagName === "SELECT") {
      const ok = choose(el, [step.value, step.display, ...(step.alternatives || [])]);
      results.push({ label: step.label, status: ok ? "filled" : "nooption" });
      continue;
    }

    // A dropdown that is not a <select> is somebody's own widget with its own idea of state.
    // Typing into it would leave a box that reads correctly and has selected nothing — which is
    // worse than an empty box, because it looks done.
    if (step.kind === "select" || el.getAttribute("role") === "combobox") {
      results.push({ label: step.label, status: "dropdown" });
      continue;
    }

    if (el.readOnly) {
      results.push({ label: step.label, status: "locked" });
      continue;
    }

    put(el, el.type === "date" ? step.value : step.display || step.value);
    results.push({
      label: step.label,
      status: String(el.value || "").trim() ? "filled" : "rejected",
    });
  }
  return results;
}

