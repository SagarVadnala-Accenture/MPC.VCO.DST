window.modelHistory = {
    setBodyScrollLocked(locked) {
        document.body.style.overflow = locked ? "hidden" : "";
    },
    focusById(elementId) {
        if (!elementId) {
            return;
        }

        const element = document.getElementById(elementId);
        element?.focus();
    },
    getNoteCaret(element) {
        return element?.selectionStart ?? 0;
    },
    setNoteCaret(element, position) {
        if (!element) {
            return;
        }

        element.focus();
        element.setSelectionRange(position, position);
    },
    attachMentionInput(element, dotnetReference) {
        if (!element) {
            return;
        }

        const handler = event => {
            if (element.parentElement?.dataset.mentionOpen !== "true"
                || !["ArrowDown", "ArrowUp", "Enter", "Escape"].includes(event.key)) {
                return;
            }

            event.preventDefault();
            event.stopPropagation();
            dotnetReference.invokeMethodAsync("HandleMentionKey", event.key);
        };

        element.addEventListener("keydown", handler);
        element._mentionKeyHandler = handler;
    },
    detachMentionInput(element) {
        if (element?._mentionKeyHandler) {
            element.removeEventListener("keydown", element._mentionKeyHandler);
            delete element._mentionKeyHandler;
        }
    }
};
