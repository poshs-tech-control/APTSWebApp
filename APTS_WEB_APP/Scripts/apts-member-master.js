(function () {
    var existingWithQrMode = "existing-with-qr";
    var activeVisionStream = null;

    function bySelector(selector) {
        return document.querySelector(selector);
    }

    function setValue(selector, value) {
        var input = bySelector(selector);
        if (!input) {
            return;
        }

        input.value = value || "";
        input.dispatchEvent(new Event("input", { bubbles: true }));
        input.dispatchEvent(new Event("change", { bubbles: true }));
    }

    function setRawValue(selector, value) {
        var input = bySelector(selector);
        if (input) {
            input.value = value || "";
        }
    }

    function getForm() {
        return bySelector(".apts-member-form");
    }

    function getFormUrl(attributeName) {
        var form = getForm();
        return form ? form.getAttribute(attributeName) : "";
    }

    function normalizeScan(value) {
        return (value || "").replace(/\s+/g, "").trim().toUpperCase();
    }

    function decodeHtmlEntities(value) {
        return String(value || "")
            .replace(/&lt;/gi, "<")
            .replace(/&gt;/gi, ">")
            .replace(/&#60;/gi, "<")
            .replace(/&#62;/gi, ">");
    }

    function containsHtmlInjection(value) {
        var decodedValue = decodeHtmlEntities(value);
        return /<|>|javascript\s*:|on[a-z]+\s*=/i.test(String(value || ""))
            || /<|>|javascript\s*:|on[a-z]+\s*=/i.test(decodedValue);
    }

    function validatePlainInput(selector, label, statusSetter) {
        var input = bySelector(selector);
        if (!input || !containsHtmlInjection(input.value)) {
            return true;
        }

        statusSetter("invalid", "HTML markup is not allowed in " + label + ".");
        return false;
    }

    function showPopup(message) {
        if (window.AptsProcessPopup) {
            window.AptsProcessPopup.show(message || "Done");
        }
    }

    function clearStoredSkewDetails() {
        setValue("#SkuWeight", "");
        setValue("#SkuMemberTypeCode", "");
        setValue("#SkuMemberSize", "");
    }

    function storeSkewDetails(details) {
        setValue("#SkuWeight", details && details.weight);
        setValue("#SkuMemberTypeCode", details && details.memberType);
        setValue("#SkuMemberSize", details && details.memberSize);
    }

    function setSaveStatus(state, message) {
        var status = bySelector("[data-member-save-status]");
        if (!status) {
            return;
        }

        status.textContent = message || "";
        status.classList.toggle("is-valid", state === "valid");
        status.classList.toggle("is-invalid", state === "invalid");
        status.classList.toggle("is-loading", state === "loading");
    }

    function setNewBatchStatus(state, message) {
        var input = bySelector("#NewBatchNo");
        var status = bySelector("[data-new-batch-status]");

        if (input) {
            input.classList.toggle("is-valid", state === "valid");
            input.classList.toggle("is-invalid", state === "invalid");
        }

        if (status) {
            status.textContent = message || "";
            status.classList.toggle("is-valid", state === "valid");
            status.classList.toggle("is-invalid", state === "invalid");
        }
    }

    function setSkuReadonly(isReadonly) {
        var select = bySelector("[data-sku-select]");
        var button = select ? select.querySelector("[data-custom-select-button]") : null;
        var menu = select ? select.querySelector("[data-custom-select-menu]") : null;

        if (select) {
            select.classList.toggle("is-readonly", !!isReadonly);
            select.classList.remove("is-open");
        }

        if (button) {
            button.disabled = !!isReadonly;
            button.setAttribute("aria-expanded", "false");
        }

        if (menu) {
            menu.hidden = true;
        }
    }

    function resetCustomSelect(form) {
        var select = form.querySelector("[data-sku-select]");
        var label = select ? select.querySelector("[data-custom-select-label]") : null;
        var menu = select ? select.querySelector("[data-custom-select-menu]") : null;
        var button = select ? select.querySelector("[data-custom-select-button]") : null;

        if (select) {
            select.classList.remove("is-open", "is-readonly");
        }

        if (label) {
            label.textContent = "Select";
        }

        if (menu) {
            menu.hidden = true;
            menu.querySelectorAll("[role='option']").forEach(function (option) {
                option.removeAttribute("aria-selected");
            });
        }

        if (button) {
            button.disabled = false;
            button.setAttribute("aria-expanded", "false");
        }
    }

    function selectSku(skewCode, textColumn, details) {
        var form = getForm();
        var skuSelect = bySelector("[data-sku-select]");
        var menu = skuSelect ? skuSelect.querySelector("[data-custom-select-menu]") : null;
        var label = skuSelect ? skuSelect.querySelector("[data-custom-select-label]") : null;
        var selectedOption = null;

        if (menu) {
            menu.querySelectorAll("[data-sku-option]").forEach(function (option) {
                var isMatch = option.getAttribute("data-value") === skewCode;
                option.toggleAttribute("aria-selected", isMatch);
                if (isMatch) {
                    selectedOption = option;
                }
            });
        }

        if (selectedOption) {
            textColumn = textColumn || selectedOption.getAttribute("data-text-column") || selectedOption.textContent.trim();
            details = details || {
                memberType: selectedOption.getAttribute("data-member-type"),
                memberSize: selectedOption.getAttribute("data-member-size")
            };
        }

        setValue("#Sku", skewCode || "");
        setValue("#SkuText", textColumn || "");
        if (label) {
            label.textContent = textColumn || "Select";
        }

        if (details) {
            storeSkewDetails(details);
        } else {
            clearStoredSkewDetails();
        }

        if (form) {
            setSaveStatus("", "");
        }
    }

    function loadSkewDetails(skewCode) {
        var url = getFormUrl("data-skew-details-url");

        clearStoredSkewDetails();
        if (containsHtmlInjection(skewCode)) {
            setSaveStatus("invalid", "HTML markup is not allowed in SKU code.");
            return Promise.resolve(false);
        }

        if (!url || !skewCode || !window.fetch) {
            return Promise.resolve(false);
        }

        return fetch(url + "?skewCode=" + encodeURIComponent(skewCode), {
            credentials: "same-origin",
            headers: { "Accept": "application/json", "X-Requested-With": "XMLHttpRequest" }
        })
            .then(function (response) {
                return response.ok ? response.json() : null;
            })
            .then(function (result) {
                if (result && result.success) {
                    storeSkewDetails(result);
                    return true;
                }

                clearStoredSkewDetails();
                return false;
            })
            .catch(function () {
                clearStoredSkewDetails();
                return false;
            });
    }

    function validateNewBatchNo() {
        var input = bySelector("#NewBatchNo");
        var url = getFormUrl("data-new-batch-validation-url");
        var batchNo = normalizeScan(input ? input.value : "");

        if (input && input.value !== batchNo) {
            input.value = batchNo;
        }

        if (containsHtmlInjection(input ? input.value : "") || containsHtmlInjection(batchNo)) {
            setNewBatchStatus("invalid", "HTML markup is not allowed in New Batch No.");
            return Promise.resolve(false);
        }

        if (!batchNo) {
            setNewBatchStatus("", "");
            return Promise.resolve(false);
        }

        if (!url || !window.fetch) {
            setNewBatchStatus("invalid", "Unable to validate New Batch No.");
            return Promise.resolve(false);
        }

        setNewBatchStatus("", "Checking New Batch No...");
        return fetch(url + "?batchNo=" + encodeURIComponent(batchNo), {
            credentials: "same-origin",
            headers: { "Accept": "application/json", "X-Requested-With": "XMLHttpRequest" }
        })
            .then(function (response) {
                return response.ok ? response.json() : null;
            })
            .then(function (result) {
                if (result && result.success && result.isValid) {
                    setNewBatchStatus("valid", result.message || "New Batch No available.");
                    return true;
                }

                setNewBatchStatus("invalid", result && result.message ? result.message : "New Batch No is not valid.");
                return false;
            })
            .catch(function () {
                setNewBatchStatus("invalid", "Unable to validate New Batch No.");
                return false;
            });
    }

    function clearExistingQrDetails() {
        setValue("#ExistingRfid", "");
        setValue("#ExistingMemberCode", "");
    }

    function updateModeState() {
        var mode = bySelector("#MemberMode");
        var qrInput = bySelector("#QrCode");
        var qrButton = bySelector('[data-scan-target="#QrCode"]');
        var showButton = bySelector("[data-show-existing-qr]");
        var isExistingWithQr = mode && mode.value === existingWithQrMode;

        [qrInput, qrButton, showButton].forEach(function (element) {
            if (element) {
                element.disabled = !isExistingWithQr;
            }
        });

        setSkuReadonly(!!isExistingWithQr);
        if (!isExistingWithQr) {
            setValue("#QrCode", "");
            clearExistingQrDetails();
        } else {
            selectSku("", "", null);
        }
    }

    function showExistingQr() {
        var input = bySelector("#QrCode");
        var url = getFormUrl("data-existing-qr-url");
        var qrCode = normalizeScan(input ? input.value : "");
        var button = bySelector("[data-show-existing-qr]");

        if (input) {
            input.value = qrCode;
        }

        if (containsHtmlInjection(input ? input.value : "") || containsHtmlInjection(qrCode)) {
            setSaveStatus("invalid", "HTML markup is not allowed in QR Code.");
            return;
        }

        if (!qrCode) {
            setSaveStatus("invalid", "Please scan QR Code.");
            return;
        }

        if (!url || !window.fetch) {
            setSaveStatus("invalid", "Unable to load member details.");
            return;
        }

        clearExistingQrDetails();
        selectSku("", "", null);
        setSaveStatus("loading", "Loading member details...");
        if (button) {
            button.disabled = true;
        }

        fetch(url + "?qrCode=" + encodeURIComponent(qrCode), {
            credentials: "same-origin",
            headers: { "Accept": "application/json", "X-Requested-With": "XMLHttpRequest" }
        })
            .then(function (response) {
                return response.ok ? response.json() : null;
            })
            .then(function (result) {
                if (!result || !result.success) {
                    setSaveStatus("invalid", result && result.message ? result.message : "Member details not found.");
                    return;
                }

                setValue("#ExistingRfid", result.existingRfid);
                setValue("#ExistingMemberCode", result.memberCode);
                selectSku(result.sku, "", {
                    weight: result.weight,
                    memberType: result.memberType,
                    memberSize: result.memberSize
                });
                setSkuReadonly(true);
                setSaveStatus("valid", "Member details loaded.");
            })
            .catch(function () {
                setSaveStatus("invalid", "Unable to load member details.");
            })
            .then(function () {
                if (button) {
                    button.disabled = false;
                }
            });
    }


    function resetMemberModeSelect(form) {
        var select = form.querySelector("[data-member-mode-select]");
        var label = select ? select.querySelector("[data-custom-select-label]") : null;
        var menu = select ? select.querySelector("[data-custom-select-menu]") : null;
        var button = select ? select.querySelector("[data-custom-select-button]") : null;

        if (select) {
            select.classList.remove("is-open");
        }

        if (label) {
            label.textContent = "Select Member Type";
        }

        if (menu) {
            menu.hidden = true;
            menu.querySelectorAll("[role='option']").forEach(function (option) {
                option.removeAttribute("aria-selected");
            });
        }

        if (button) {
            button.setAttribute("aria-expanded", "false");
        }
    }

    function syncProductionLocationFromOption(option) {
        var titleInput = bySelector("#ProductionLocationTitle");
        if (!option) {
            return;
        }

        setValue("#ProductionLocation", option.getAttribute("data-value") || "");
        if (titleInput) {
            titleInput.value = option.getAttribute("data-location-title") || option.textContent.trim();
        }
    }

    function syncProductionLocationTitle() {
        var currentCode = bySelector("#ProductionLocation");
        var selectedOption = bySelector("[data-production-location-option][aria-selected='true']");
        var matchingOption = currentCode ? bySelector("[data-production-location-option][data-value='" + currentCode.value + "']") : null;
        syncProductionLocationFromOption(selectedOption || matchingOption);
    }

    function resetProductionLocationSelect(form) {
        var select = form ? form.querySelector("[data-production-location-select]") : null;
        var defaultOption = select ? select.querySelector("[data-production-location-option][data-value='CLOC00000010']") : null;
        var label = select ? select.querySelector("[data-custom-select-label]") : null;
        var menu = select ? select.querySelector("[data-custom-select-menu]") : null;
        var button = select ? select.querySelector("[data-custom-select-button]") : null;

        if (select) {
            select.classList.remove("is-open");
        }

        if (label) {
            label.textContent = "Sanaswadi";
        }

        if (menu) {
            menu.hidden = true;
            menu.querySelectorAll("[role='option']").forEach(function (option) {
                option.toggleAttribute("aria-selected", option === defaultOption);
            });
        }

        if (button) {
            button.setAttribute("aria-expanded", "false");
        }

        syncProductionLocationFromOption(defaultOption);
    }

    function resetMemberMasterForm(form) {
        form.reset();
        resetCustomSelect(form);
        resetMemberModeSelect(form);
        resetProductionLocationSelect(form);
        clearStoredSkewDetails();
        clearExistingQrDetails();
        setValue("#Sku", "");
        setValue("#SkuText", "");
        setValue("#QrCode", "");
        setValue("#NewBatchNo", "");
        setNewBatchStatus("", "");
        setSaveStatus("", "");
        updateModeState();
    }

    function formHasRequiredDetails(form) {
        var mode = bySelector("#MemberMode");
        if (!mode || !mode.value) {
            setSaveStatus("invalid", "Please select Member Type.");
            return false;
        }

        if (!validatePlainInput("#MemberMode", "Member Type", setSaveStatus)
            || !validatePlainInput("#QrCode", "QR Code", setSaveStatus)
            || !validatePlainInput("#ExistingRfid", "Existing RFID", setSaveStatus)
            || !validatePlainInput("#ExistingMemberCode", "Existing Member Code", setSaveStatus)
            || !validatePlainInput("#Sku", "SKU", setSaveStatus)
            || !validatePlainInput("#SkuText", "SKU", setSaveStatus)
            || !validatePlainInput("#SkuWeight", "Member Weight", setSaveStatus)
            || !validatePlainInput("#SkuMemberTypeCode", "Member Type Code", setSaveStatus)
            || !validatePlainInput("#SkuMemberSize", "Member Size", setSaveStatus)
            || !validatePlainInput("#NewBatchNo", "New Batch No", setSaveStatus)
            || !validatePlainInput("#ProductionLocation", "Production Location", setSaveStatus)) {
            return false;
        }

        if (mode.value === existingWithQrMode) {
            if (!bySelector("#ExistingRfid") || !bySelector("#ExistingRfid").value) {
                setSaveStatus("invalid", "Please press Show after scanning QR Code.");
                return false;
            }
        }

        if (!bySelector("#Sku") || !bySelector("#Sku").value) {
            setSaveStatus("invalid", "Please select SKU.");
            return false;
        }

        if (!bySelector("#NewBatchNo") || !bySelector("#NewBatchNo").value.trim()) {
            setSaveStatus("invalid", "Please scan New Batch No.");
            return false;
        }

        return !!form;
    }

    function saveMemberMaster(form) {
        var url = getFormUrl("data-save-url");
        var submitButton = form ? form.querySelector("[type='submit']") : null;

        if (!url || !form || !window.fetch) {
            setSaveStatus("invalid", "Unable to save member.");
            return;
        }

        syncProductionLocationTitle();

        if (!formHasRequiredDetails(form)) {
            return;
        }

        validateNewBatchNo().then(function (isValidBatch) {
            if (!isValidBatch) {
                return;
            }

            setSaveStatus("loading", "Saving member...");
            if (submitButton) {
                submitButton.disabled = true;
            }

            fetch(url, {
                method: "POST",
                credentials: "same-origin",
                body: new FormData(form),
                headers: { "Accept": "application/json", "X-Requested-With": "XMLHttpRequest" }
            })
                .then(function (response) {
                    return response.ok ? response.json() : null;
                })
                .then(function (result) {
                    if (result && result.success) {
                        setSaveStatus("", "");
                        showPopup(result.message || "Member Saved Successfully");
                        resetMemberMasterForm(form);
                        return;
                    }

                    setSaveStatus("invalid", result && result.message ? result.message : "Unable to save member.");
                })
                .catch(function () {
                    setSaveStatus("invalid", "Unable to save member.");
                })
                .then(function () {
                    if (submitButton) {
                        submitButton.disabled = false;
                    }
                });
        });
    }

    function stopVisionCamera() {
        if (activeVisionStream) {
            activeVisionStream.getTracks().forEach(function (track) {
                track.stop();
            });
            activeVisionStream = null;
        }
    }

    function closeVisionScanner() {
        var modal = bySelector("[data-vision-scanner]");
        stopVisionCamera();
        if (modal) {
            modal.hidden = true;
        }
    }

    function setVisionStatus(message) {
        var status = bySelector("[data-vision-status]");
        if (status) {
            status.textContent = message || "";
        }
    }

    function openVisionScanner() {
        var modal = bySelector("[data-vision-scanner]");
        var video = bySelector("[data-vision-video]");
        var textArea = bySelector("[data-vision-text]");

        if (!modal || !video || !navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
            setNewBatchStatus("invalid", "Camera is not available on this device.");
            return;
        }

        modal.hidden = false;
        if (textArea) {
            textArea.hidden = true;
            textArea.value = "";
        }
        setVisionStatus("Opening camera...");

        navigator.mediaDevices.getUserMedia({ video: { facingMode: { ideal: "environment" } }, audio: false })
            .then(function (stream) {
                activeVisionStream = stream;
                video.srcObject = stream;
                video.play();
                setVisionStatus("Point the camera at the New Batch No.");
            })
            .catch(function () {
                closeVisionScanner();
                setNewBatchStatus("invalid", "Unable to open camera.");
            });
    }

    function captureVisionText() {
        var endpoint = getFormUrl("data-vision-endpoint");
        var video = bySelector("[data-vision-video]");
        var canvas = bySelector("[data-vision-canvas]");
        var textArea = bySelector("[data-vision-text]");
        var captureButton = bySelector("[data-vision-capture]");

        if (!endpoint || !video || !canvas || !window.fetch) {
            setVisionStatus("Unable to read image.");
            return;
        }

        canvas.width = video.videoWidth || 1280;
        canvas.height = video.videoHeight || 720;
        canvas.getContext("2d").drawImage(video, 0, 0, canvas.width, canvas.height);
        setVisionStatus("Reading New Batch No...");
        if (captureButton) {
            captureButton.disabled = true;
        }

        fetch(endpoint, {
            method: "POST",
            credentials: "same-origin",
            headers: {
                "Accept": "application/json",
                "Content-Type": "application/json"
            },
            body: JSON.stringify({ imageData: canvas.toDataURL("image/jpeg", 0.88) })
        })
            .then(function (response) {
                return response.ok ? response.json() : null;
            })
            .then(function (result) {
                if (!result || !result.success) {
                    setVisionStatus(result && result.message ? result.message : "Unable to read New Batch No.");
                    return;
                }

                if (textArea) {
                    textArea.hidden = false;
                    textArea.value = result.text || "";
                }

                if (result.batchNo) {
                    setValue("#NewBatchNo", result.batchNo);
                    closeVisionScanner();
                    validateNewBatchNo();
                    return;
                }

                setVisionStatus("New Batch No not found in image.");
            })
            .catch(function () {
                setVisionStatus("Unable to read New Batch No.");
            })
            .then(function () {
                if (captureButton) {
                    captureButton.disabled = false;
                }
            });
    }

    document.addEventListener("click", function (event) {
        var skuOption = event.target.closest("[data-sku-option]");
        if (skuOption && (!bySelector("[data-sku-select]") || !bySelector("[data-sku-select]").classList.contains("is-readonly"))) {
            var skewCode = skuOption.getAttribute("data-value");
            selectSku(skewCode, skuOption.getAttribute("data-text-column"), {
                memberType: skuOption.getAttribute("data-member-type"),
                memberSize: skuOption.getAttribute("data-member-size")
            });
            loadSkewDetails(skewCode);
            return;
        }

        var productionLocationOption = event.target.closest("[data-production-location-option]");
        if (productionLocationOption) {
            syncProductionLocationFromOption(productionLocationOption);
            return;
        }

        if (event.target.closest("[data-show-existing-qr]")) {
            showExistingQr();
            return;
        }

        if (event.target.closest("[data-member-cancel]")) {
            var form = getForm();
            if (form) {
                resetMemberMasterForm(form);
            }
            return;
        }

        if (event.target.closest("[data-vision-open]")) {
            openVisionScanner();
            return;
        }

        if (event.target.closest("[data-vision-close]")) {
            closeVisionScanner();
            return;
        }

        if (event.target.closest("[data-vision-capture]")) {
            captureVisionText();
        }
    });

    document.addEventListener("change", function (event) {
        if (event.target && event.target.matches("#MemberMode")) {
            updateModeState();
        }

        if (event.target && event.target.matches("#NewBatchNo")) {
            validateNewBatchNo();
        }


    });

    document.addEventListener("input", function (event) {
        if (event.target && (event.target.matches("#QrCode") || event.target.matches("#NewBatchNo"))) {
            event.target.value = normalizeScan(event.target.value);
            if (containsHtmlInjection(event.target.value)) {
                if (event.target.matches("#NewBatchNo")) {
                    setNewBatchStatus("invalid", "HTML markup is not allowed in New Batch No.");
                } else {
                    setSaveStatus("invalid", "HTML markup is not allowed in QR Code.");
                }
            }
        }
    });

    document.addEventListener("apts:qr-scan", function (event) {
        if (event.target && event.target.matches("#QrCode")) {
            showExistingQr();
        }

        if (event.target && event.target.matches("#NewBatchNo")) {
            validateNewBatchNo();
        }


    });

    document.addEventListener("submit", function (event) {
        var form = event.target && event.target.closest(".apts-member-form");
        if (!form) {
            return;
        }

        event.preventDefault();
        saveMemberMaster(form);
    });

    document.addEventListener("DOMContentLoaded", updateModeState);
}());

