(function () {
    var storageKey = "apts.pending-challan.v1";
    var maxAttempts = 3;

    function showToast(message, isError) {
        if (!message || !document.body) {
            return;
        }

        var toast = document.createElement("div");
        toast.textContent = message;
        toast.setAttribute("role", "status");
        toast.style.position = "fixed";
        toast.style.left = "50%";
        toast.style.bottom = "24px";
        toast.style.transform = "translateX(-50%)";
        toast.style.zIndex = "9999";
        toast.style.maxWidth = "min(92vw, 420px)";
        toast.style.padding = "12px 16px";
        toast.style.borderRadius = "8px";
        toast.style.background = isError ? "#842029" : "#0f5132";
        toast.style.color = "#fff";
        toast.style.boxShadow = "0 12px 30px rgba(0,0,0,.22)";
        toast.style.fontSize = "14px";
        toast.style.textAlign = "center";
        document.body.appendChild(toast);

        window.setTimeout(function () {
            toast.style.opacity = "0";
            toast.style.transition = "opacity .2s ease";
            window.setTimeout(function () {
                if (toast.parentNode) {
                    toast.parentNode.removeChild(toast);
                }
            }, 250);
        }, 3200);
    }

    function normalizePending(raw) {
        var parsed = JSON.parse(raw);
        if (Array.isArray(parsed)) {
            return parsed;
        }

        return parsed ? [parsed] : [];
    }

    function savePendingChallans(items) {
        try {
            if (!items || !items.length) {
                window.sessionStorage.removeItem(storageKey);
                return;
            }

            window.sessionStorage.setItem(storageKey, JSON.stringify(items));
        } catch (error) {
        }
    }

    function readPendingChallans() {
        try {
            var raw = window.sessionStorage.getItem(storageKey);
            return raw ? normalizePending(raw) : [];
        } catch (error) {
            window.sessionStorage.removeItem(storageKey);
            return [];
        }
    }

    function isValidPending(pending) {
        return pending && pending.endpoint && pending.paramName && pending.code;
    }

    async function sendPendingChallan(pending) {
        if (!isValidPending(pending) || !window.fetch) {
            return { remove: true };
        }

        pending.attempts = (pending.attempts || 0) + 1;

        var body = new URLSearchParams();
        body.append(pending.paramName, pending.code);

        try {
            var response = await fetch(pending.endpoint, {
                method: "POST",
                credentials: "same-origin",
                headers: {
                    "Accept": "application/json",
                    "Content-Type": "application/x-www-form-urlencoded; charset=UTF-8",
                    "X-Requested-With": "XMLHttpRequest"
                },
                body: body.toString()
            });
            var contentType = response.headers.get("content-type") || "";
            var result = null;
            if (contentType.indexOf("application/json") >= 0) {
                result = await response.json();
            }

            if (response.ok && result && result.success) {
                showToast(result.message || "Challan generated and mail sent.");
                return { remove: true };
            }

            if (result && result.message) {
                pending.lastError = result.message;
                showToast(result.message, true);
            } else if (!response.ok) {
                pending.lastError = "Challan mail request failed. HTTP " + response.status + " " + response.statusText + ".";
            } else if (contentType.indexOf("text/html") >= 0) {
                pending.lastError = "Challan mail request returned login/error page instead of JSON. Please login again or deploy latest server files.";
            } else {
                pending.lastError = "Challan mail request did not return a valid server response.";
            }
        } catch (error) {
            pending.lastError = "Challan mail request failed in browser: " + (error && error.message ? error.message : "network/server error") + ".";
        }

        if (pending.attempts >= maxAttempts) {
            showToast(pending.lastError || "Challan mail not sent. Please check SMTP/server access.", true);
            return { remove: true };
        }

        return { remove: false, pending: pending };
    }

    async function processPendingChallan() {
        var pendingItems = readPendingChallans();
        if (!pendingItems.length) {
            return;
        }

        var remaining = [];
        for (var i = 0; i < pendingItems.length; i++) {
            var result = await sendPendingChallan(pendingItems[i]);
            if (!result.remove && result.pending) {
                remaining.push(result.pending);
            }
        }

        savePendingChallans(remaining);
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", processPendingChallan);
    } else {
        processPendingChallan();
    }

    window.addEventListener("online", processPendingChallan);
    window.addEventListener("focus", processPendingChallan);
}());