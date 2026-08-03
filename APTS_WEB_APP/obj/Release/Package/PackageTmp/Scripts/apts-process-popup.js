(function () {
    var popup = null;
    var messageElement = null;
    var iconElement = null;
    var closeButton = null;
    var lastFocusedElement = null;
    var closeCallback = null;

    function buildPopup() {
        if (popup) {
            return;
        }

        popup = document.createElement("section");
        popup.className = "apts-process-popup";
        popup.setAttribute("data-process-popup", "");
        popup.setAttribute("role", "dialog");
        popup.setAttribute("aria-modal", "true");
        popup.setAttribute("aria-label", "Process completed");
        popup.hidden = true;

        popup.innerHTML =
            '<div class="apts-process-popup__panel">' +
                '<div class="apts-process-popup__icon" aria-hidden="true">' +
                    '<svg viewBox="0 0 24 24"><path d="m5 12 4 4L19 6" /></svg>' +
                '</div>' +
                '<p data-process-popup-message></p>' +
                '<button type="button" data-process-popup-close>OK</button>' +
            '</div>';

        document.body.appendChild(popup);
        iconElement = popup.querySelector(".apts-process-popup__icon");
        messageElement = popup.querySelector("[data-process-popup-message]");
        closeButton = popup.querySelector("[data-process-popup-close]");
    }

    function close() {
        if (!popup) {
            return;
        }

        popup.hidden = true;
        document.body.classList.remove("apts-process-popup-open");

        if (lastFocusedElement && typeof lastFocusedElement.focus === "function") {
            lastFocusedElement.focus();
        }

        if (typeof closeCallback === "function") {
            var callback = closeCallback;
            closeCallback = null;
            callback();
        } else {
            closeCallback = null;
        }
    }

    function normalizeVariant(variant) {
        return variant === "error" || variant === "warning" ? "error" : "success";
    }

    function setVariant(variant) {
        var normalized = normalizeVariant(variant);
        popup.classList.toggle("is-error", normalized === "error");
        popup.classList.toggle("is-success", normalized === "success");
        popup.setAttribute("aria-label", normalized === "error" ? "Process message" : "Process completed");

        if (iconElement) {
            iconElement.innerHTML = normalized === "error"
                ? '<svg viewBox="0 0 24 24"><path d="M12 8v5M12 17h.01M10.29 3.86 1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z" /></svg>'
                : '<svg viewBox="0 0 24 24"><path d="m5 12 4 4L19 6" /></svg>';
        }
    }

    function show(message, onClose, variant) {
        if (typeof onClose === "string") {
            variant = onClose;
            onClose = null;
        }

        buildPopup();
        lastFocusedElement = document.activeElement;
        closeCallback = typeof onClose === "function" ? onClose : null;
        setVariant(variant);
        messageElement.textContent = message || "Process Completed Successfully";
        popup.hidden = false;
        document.body.classList.add("apts-process-popup-open");
        closeButton.focus();
    }

    document.addEventListener("click", function (event) {
        if (event.target.closest("[data-process-popup-close]")) {
            close();
            return;
        }

        if (popup && event.target === popup) {
            close();
        }
    });

    document.addEventListener("keydown", function (event) {
        if (event.key === "Escape" && popup && !popup.hidden) {
            close();
        }
    });

    document.addEventListener("apts:process-completed", function (event) {
        var detail = event.detail || {};
        show(detail.message, detail.variant);
    });

    window.AptsProcessPopup = {
        show: show,
        close: close
    };

    document.documentElement.setAttribute("data-apts-process-popup-ready", "true");
}());