export function confirmReplacement(dialog) {
    dialog.returnValue = "cancel";
    return new Promise(resolve => {
        dialog.addEventListener("close", () => resolve(dialog.returnValue === "replace"), { once: true });
        dialog.showModal();
    });
}

export function closeReplacement(dialog) {
    if (dialog.open) dialog.close("cancel");
}
