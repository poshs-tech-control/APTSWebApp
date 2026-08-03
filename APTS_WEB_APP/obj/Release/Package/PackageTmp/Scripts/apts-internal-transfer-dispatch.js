(function () {
    var transferDraftKey = "apts.internal-transfer.draft.v1";

    function bySelector(selector, root) {
        return (root || document).querySelector(selector);
    }

    function setStatus(state, message) {
        var status = bySelector("[data-transfer-dispatch-status]");

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
        var endpoint = form ? form.getAttribute("data-challan-url") : "";
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
            }
        } catch (error) {
        }
    }
    function queueChallanAfterScreenClear(form, paramName, code) {
        var endpoint = form ? form.getAttribute("data-challan-url") : "";
        if (!endpoint || !paramName || !code) {
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
    function getTrimmedValue(form, selector) {
        var input = bySelector(selector, form);
        return input ? input.value.trim() : "";
    }

    function setInputValue(form, selector, value) {
        var input = bySelector(selector, form);
        if (input) {
            input.value = value || "";
            input.classList.remove("is-invalid");
        }
    }

    function markInvalid(form, selector) {
        var input = bySelector(selector, form);

        if (input) {
            input.classList.add("is-invalid");
            input.focus();
        }
    }

    function clearInvalidFields(form) {
        form.querySelectorAll(".is-invalid").forEach(function (input) {
            input.classList.remove("is-invalid");
        });
    }

    function getTransportMode(form) {
        var selected = bySelector("input[name='TransportEntryMode']:checked", form);
        return selected ? selected.value : "Master";
    }

    function isTransportMasterMode(form) {
        return getTransportMode(form) === "Master";
    }

    function resetTruckDropdown(form) {
        var field = bySelector("[data-internal-transfer-truck-field]", form);
        var input = bySelector("[data-internal-transfer-truck-code]", form);
        var label = field ? bySelector("[data-custom-select-label]", field) : null;

        if (input) {
            input.value = "";
        }

        if (label) {
            label.textContent = "Select Truck";
        }

        if (field) {
            field.querySelectorAll("[role='option']").forEach(function (option) {
                option.setAttribute("aria-selected", "false");
            });
        }
    }

    function clearTransportDetails(form) {
        setInputValue(form, "input[name='TruckNumber']", "");
        setInputValue(form, "input[name='DriverName']", "");
        setInputValue(form, "input[name='DriverContactNo']", "");
        setInputValue(form, "input[name='TransporterName']", "");
    }

    function setTransportInputsReadonly(form, isReadonly) {
        [
            "input[name='TruckNumber']",
            "input[name='DriverName']",
            "input[name='DriverContactNo']",
            "input[name='TransporterName']"
        ].forEach(function (selector) {
            var input = bySelector(selector, form);
            if (input) {
                input.readOnly = isReadonly;
            }
        });
    }

    function findSelectedTruckOption(form) {
        var truckCode = getTrimmedValue(form, "[data-internal-transfer-truck-code]");
        var options = form.querySelectorAll("[data-internal-transfer-truck-field] [data-value]");
        var selectedOption = null;

        options.forEach(function (option) {
            if ((option.getAttribute("data-value") || "") === truckCode) {
                selectedOption = option;
            }
        });

        return selectedOption;
    }

    function applyTruckDetailsFromOption(form, option) {
        if (!option) {
            clearTransportDetails(form);
            return;
        }

        setInputValue(form, "input[name='TruckNumber']", option.getAttribute("data-truck-no") || "");
        setInputValue(form, "input[name='DriverName']", option.getAttribute("data-driver-name") || "");
        setInputValue(form, "input[name='DriverContactNo']", option.getAttribute("data-driver-contact") || "");
        setInputValue(form, "input[name='TransporterName']", option.getAttribute("data-transporter-name") || "");
    }

    function updateTransportMode(form) {
        var masterMode = isTransportMasterMode(form);
        var truckField = bySelector("[data-internal-transfer-truck-field]", form);

        if (truckField) {
            truckField.hidden = !masterMode;
        }

        setTransportInputsReadonly(form, masterMode);

        if (masterMode) {
            applyTruckDetailsFromOption(form, findSelectedTruckOption(form));
        } else {
            resetTruckDropdown(form);
            clearTransportDetails(form);
        }
    }

    function validateDispatchForm(form) {
        clearInvalidFields(form);

        if (isTransportMasterMode(form) && !getTrimmedValue(form, "[data-internal-transfer-truck-code]")) {
            showPopup("Please select Truck.");
            return false;
        }

        if (!getTrimmedValue(form, "input[name='TruckNumber']")) {
            markInvalid(form, "input[name='TruckNumber']");
            showPopup("Please enter Truck Number.");
            return false;
        }

        if (!getTrimmedValue(form, "input[name='DriverName']")) {
            markInvalid(form, "input[name='DriverName']");
            showPopup("Please enter Driver Name.");
            return false;
        }

        if (!getTrimmedValue(form, "input[name='DriverContactNo']")) {
            markInvalid(form, "input[name='DriverContactNo']");
            showPopup("Please enter Driver Contact No.");
            return false;
        }

        if (!getTrimmedValue(form, "input[name='TransporterName']")) {
            markInvalid(form, "input[name='TransporterName']");
            showPopup("Please enter Transporter Name.");
            return false;
        }

        if (!form.querySelector("input[name^='Members'][name$='.Rfid']")) {
            showPopup("Please select member.");
            return false;
        }

        return true;
    }

    async function submitDispatch(form) {
        var url = form.getAttribute("data-save-url") || form.action;
        var submitButton = form.querySelector("[type='submit']");

        if (!validateDispatchForm(form)) {
            return;
        }

        if (!url || !window.fetch) {
            showPopup("Unable to save Internal Transfer.");
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
                try {
                    window.sessionStorage.removeItem(transferDraftKey);
                } catch (error) {
                }
                setStatus("", "");
                var successUrl = form.getAttribute("data-success-url") || "/InternalTransfer/InternalTransfer";
                if (result.challanPending && result.matDispCode) {
                    queueChallanAfterScreenClear(form, "dispatchCode", result.matDispCode);
                }

                showPopup(result.message || "Internal Transfer Saved Successfully", function () {
                    window.location.href = successUrl;
                });
                return;
            }

            setStatus("", "");
            showPopup(result && result.message ? result.message : "Unable to save Internal Transfer.");
        } catch (error) {
            setStatus("", "");
            showPopup("Unable to save Internal Transfer.");
        } finally {
            if (submitButton) {
                submitButton.disabled = false;
            }
        }
    }

    document.addEventListener("submit", function (event) {
        var form = event.target && event.target.closest("[data-transfer-dispatch-form]");
        if (!form) {
            return;
        }

        event.preventDefault();
        submitDispatch(form);
    });

    document.addEventListener("change", function (event) {
        var form = event.target && event.target.closest("[data-transfer-dispatch-form]");
        if (!form) {
            return;
        }

        if (event.target.matches("input[name='TransportEntryMode']")) {
            updateTransportMode(form);
            return;
        }

        if (event.target.matches("[data-internal-transfer-truck-code]")) {
            applyTruckDetailsFromOption(form, findSelectedTruckOption(form));
        }
    });

    document.addEventListener("input", function (event) {
        if (event.target.closest("[data-transfer-dispatch-form]")) {
            event.target.classList.remove("is-invalid");
        }
    });

    document.addEventListener("reset", function (event) {
        var form = event.target && event.target.closest("[data-transfer-dispatch-form]");
        if (!form) {
            return;
        }

        window.setTimeout(function () {
            updateTransportMode(form);
        }, 0);
    });

    document.querySelectorAll("[data-transfer-dispatch-form]").forEach(function (form) {
        updateTransportMode(form);
    });
}());


