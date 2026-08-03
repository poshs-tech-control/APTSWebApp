(function () {
    var storageKey = "apts.pending-challan.v1";

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

    function readPendingChallan() {
        try {
            var raw = window.sessionStorage.getItem(storageKey);
            if (!raw) {
                return null;
            }
            window.sessionStorage.removeItem(storageKey);
            return JSON.parse(raw);
        } catch (error) {
            return null;
        }
    }

    async function processPendingChallan() {
        var pending = readPendingChallan();
        if (!pending || !pending.endpoint || !pending.paramName || !pending.code || !window.fetch) {
            return;
        }

        var body = new URLSearchParams();
        body.append(pending.paramName, pending.code);

        try {
            var response = await fetch(pending.endpoint, {
                method: "POST",
                credentials: "same-origin",
                headers: {
                    "Accept": "application/json",
                    "Content-Type": "application/x-www-form-urlencoded; charset=UTF-8"
                },
                body: body.toString()
            });
            var result = response.ok ? await response.json() : null;
            if (result && result.success) {
                showToast(result.message || "Challan generated and mail sent.");
            } else if (result && result.message) {
                showToast(result.message, true);
            }
        } catch (error) {
        }
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", processPendingChallan);
    } else {
        processPendingChallan();
    }
}());