(function () {
    function setStatus(state, message) {
        var status = document.querySelector("[data-dispatch-save-status]");
        if (!status) {
            return;
        }

        status.textContent = message || "";
        status.classList.toggle("is-valid", state === "valid");
        status.classList.toggle("is-invalid", state === "invalid");
        status.classList.toggle("is-loading", state === "loading");
    }

    function showProcessMessage(message, onClose) {
        if (window.AptsProcessPopup) {
            window.AptsProcessPopup.show(message, onClose);
        } else {
            setStatus("invalid", message);
            if (typeof onClose === "function") {
                onClose();
            }
        }
    }


    function queueChallanAfterScreenClear(form, paramName, code) {
        var endpoint = form ? form.getAttribute("data-dispatch-challan-url") : "";
        if (!endpoint || !paramName || !code || !window.sessionStorage) {
            return false;
        }

        try {
            window.sessionStorage.setItem("apts.pending-challan.v1", JSON.stringify({
                endpoint: endpoint,
                paramName: paramName,
                code: code
            }));
            return true;
        } catch (error) {
            return false;
        }
    }
    function redirectToStart(form) {
        var url = form ? form.getAttribute("data-dispatch-start-url") : "";
        if (url) {
            window.location.href = url;
        }
    }

    function clearEntryFields(form) {
        ["TruckNumber", "DriverName", "DriverContactNo", "TransporterName"].forEach(function (id) {
            var input = form.querySelector("#" + id);
            if (input) {
                input.value = "";
            }
        });

        var challan = form.querySelector("[name='GenerateChallan']");
        if (challan) {
            challan.checked = true;
        }

        setStatus("", "");
    }

    async function saveDispatch(form) {
        var submitButton = form.querySelector("[type='submit']");

        if (submitButton) {
            submitButton.disabled = true;
        }

        setStatus("loading", "");

        try {
            var response = await fetch(form.action, {
                method: "POST",
                credentials: "same-origin",
                body: new FormData(form),
                headers: { "Accept": "application/json" }
            });
            var result = response.ok ? await response.json() : null;

            if (result && result.success) {
                if (result.challanPending && result.palletDispCode) {
                    queueChallanAfterScreenClear(form, "palletDispatchCode", result.palletDispCode);
                }
                showProcessMessage(result.message || "Pallet Dispatch Saved Successfully", function () {
                    redirectToStart(form);
                });
                setStatus("valid", result.message || "Pallet Dispatch Saved Successfully");
                clearEntryFields(form);
                return;
            }

            showProcessMessage(result && result.message ? result.message : "Unable to save Pallet Dispatch.");
            setStatus("invalid", result && result.message ? result.message : "Unable to save Pallet Dispatch.");
        } catch (error) {
            showProcessMessage("Unable to save Pallet Dispatch.");
            setStatus("invalid", "Unable to save Pallet Dispatch.");
        } finally {
            if (submitButton) {
                submitButton.disabled = false;
            }
        }
    }

    document.addEventListener("submit", function (event) {
        var form = event.target.closest("[data-dispatch-save-form]");
        if (!form) {
            return;
        }

        event.preventDefault();
        saveDispatch(form);
    });
}());



