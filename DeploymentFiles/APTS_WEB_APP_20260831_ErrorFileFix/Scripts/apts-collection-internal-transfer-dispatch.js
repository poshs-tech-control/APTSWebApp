(function () {
    function bySelector(selector) {
        return document.querySelector(selector);
    }

    function setStatus(state, message) {
        var status = bySelector("[data-collection-transfer-dispatch-status]");

        if (!status) {
            return;
        }

        status.textContent = message || "";
        status.classList.toggle("is-valid", state === "valid");
        status.classList.toggle("is-invalid", state === "invalid");
        status.classList.toggle("is-loading", state === "loading");
    }

    function showPopup(message, onClose) {
        if (window.AptsProcessPopup) {
            window.AptsProcessPopup.show(message, onClose);
            return;
        }

        setStatus("valid", message);
        if (typeof onClose === "function") {
            onClose();
        }
    }


    function showToast(message) {
        if (!message) {
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
        toast.style.background = "#0f5132";
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
    async function generateChallanInBackground(form, dispatchCode) {
        var endpoint = form ? form.getAttribute("data-collection-transfer-challan-url") : "";
        var body = new URLSearchParams();
        body.append("dispatchCode", dispatchCode || "");

        if (!endpoint || !dispatchCode || !window.fetch) {
            return;
        }

        try {
            var response = await fetch(endpoint, {
                method: "POST",
                credentials: "same-origin",
                keepalive: true,
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
                showToast(result.message);
            }
        } catch (error) {
        }
    }
    function queueChallanAfterScreenClear(form, paramName, code) {
        var endpoint = form ? form.getAttribute("data-collection-transfer-challan-url") : "";
        if (!endpoint || !paramName || !code || !window.sessionStorage) {
            return false;
        }

        try {
            var storageKey = "apts.pending-challan.v1";
            var raw = window.sessionStorage.getItem(storageKey);
            var pendingItems = raw ? JSON.parse(raw) : [];
            if (!Array.isArray(pendingItems)) {
                pendingItems = pendingItems ? [pendingItems] : [];
            }
            pendingItems.push({
                endpoint: endpoint,
                paramName: paramName,
                code: code,
                attempts: 0
            });
            window.sessionStorage.setItem(storageKey, JSON.stringify(pendingItems));
            return true;
        } catch (error) {
            return false;
        }
    }
    function getTrimmedValue(form, selector) {
        var input = form.querySelector(selector);
        return input ? input.value.trim() : "";
    }

    function validateCollectionDispatchForm(form) {
        if (!getTrimmedValue(form, "input[name='TruckNumber']")) {
            showPopup("Truck Number is required.");
            return false;
        }

        if (!getTrimmedValue(form, "input[name='DriverName']")) {
            showPopup("Driver Name is required.");
            return false;
        }

        if (!getTrimmedValue(form, "input[name='DriverContactNo']")) {
            showPopup("Driver Contact No. is required.");
            return false;
        }

        if (!getTrimmedValue(form, "input[name='TransporterName']")) {
            showPopup("Transporter Name is required.");
            return false;
        }

        if (!form.querySelector("input[name^='Members'][name$='.Rfid']")) {
            showPopup("Please select member.");
            return false;
        }

        return true;
    }

    async function submitCollectionDispatch(form) {
        var url = form.getAttribute("data-collection-transfer-save-url") || form.action;
        var submitButton = form.querySelector("[type='submit']");

        if (!validateCollectionDispatchForm(form)) {
            return;
        }

        if (!url || !window.fetch) {
            showPopup("Unable to save Collection Internal Transfer.");
            return;
        }

        setStatus("loading", "");
        if (submitButton) {
            submitButton.disabled = true;
        }

        try {
            var response = await fetch(url, {
                method: "POST",
                credentials: "same-origin",
                body: new FormData(form),
                headers: { "Accept": "application/json" }
            });
            var result = response.ok ? await response.json() : null;

            if (result && result.success) {
                setStatus("", "");
                var successUrl = form.getAttribute("data-collection-transfer-success-url") || "/CollectionInternalTransfer/CollectionInternalTransfer";
                if (result.challanPending && result.matDispCode) {
                    queueChallanAfterScreenClear(form, "dispatchCode", result.matDispCode);
                }

                showPopup(result.message || "Collection Internal Transfer Saved Successfully", function () {
                    window.location.href = successUrl;
                });
                return;
            }

            setStatus("", "");
            showPopup(result && result.message ? result.message : "Unable to save Collection Internal Transfer.");
        } catch (error) {
            setStatus("", "");
            showPopup("Unable to save Collection Internal Transfer.");
        } finally {
            if (submitButton) {
                submitButton.disabled = false;
            }
        }
    }

    document.addEventListener("submit", function (event) {
        var form = event.target && event.target.closest("[data-collection-transfer-dispatch-form]");
        if (!form) {
            return;
        }

        event.preventDefault();
        submitCollectionDispatch(form);
    });
}());




