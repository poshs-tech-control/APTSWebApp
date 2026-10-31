(function () {
    var activeStream = null;
    var captureCanvas = document.createElement("canvas");
    var dispatchItems = [];

    function bySelector(selector) {
        return document.querySelector(selector);
    }
    function closeCustomSelect(select) {
        var menu = select.querySelector("[data-custom-select-menu]");
        var button = select.querySelector("[data-custom-select-button]");
        if (menu) {
            menu.hidden = true;
        }
        if (button) {
            button.setAttribute("aria-expanded", "false");
        }
        select.classList.remove("is-open");
    }

    function setVisionStatus(text) {
        var status = bySelector("[data-vision-status]");
        if (status) {
            status.textContent = text;
        }
    }

    function setDispatchValidationStatus(message, state) {
        var status = bySelector("[data-dispatch-validation-status]");
        var input = bySelector("#DispatchMemberId");

        if (status) {
            status.textContent = message || "";
            status.classList.toggle("is-valid", state === "valid");
            status.classList.toggle("is-invalid", state === "invalid");
            status.classList.toggle("is-loading", state === "loading");
        }

        if (input) {
            input.classList.toggle("is-invalid", state === "invalid");
        }
    }

    function getSelectedScanType() {
        var selectedMode = bySelector("input[name='scanType']:checked");
        return selectedMode ? selectedMode.value : "ClientBatchNo";
    }

    function normalizeBatchList(values) {
        var seen = {};
        var batches = [];

        (values || []).forEach(function (value) {
            String(value || "").split(/[\r\n,|]+/).forEach(function (part) {
                var batch = part.trim();
                var key = batch.toUpperCase();
                if (batch && !seen[key]) {
                    seen[key] = true;
                    batches.push(batch);
                }
            });
        });

        return batches;
    }

    function updateScanModeText() {
        var selectedMode = bySelector("input[name='scanType']:checked");
        var label = bySelector("[data-dispatch-scan-label]");
        var input = bySelector("#DispatchMemberId");
        var isClientBatch = !selectedMode || selectedMode.value === "ClientBatchNo";
        var text = isClientBatch ? "Client Batch No" : "Scan Member";

        if (label) {
            label.textContent = text;
        }
        if (input) {
            input.placeholder = text;
        }
    }

    async function validateDispatchScan(scanType, scanValue) {
        var modal = bySelector("[data-vision-scanner]");
        var endpoint = modal ? modal.getAttribute("data-dispatch-validation-endpoint") : "";

        if (!endpoint) {
            return {
                success: false,
                message: "Members Not in PreDispatch."
            };
        }

        var body = new URLSearchParams();
        body.append("scanType", scanType);
        body.append("scanValue", scanValue);

        try {
            var response = await fetch(endpoint, {
                method: "POST",
                headers: { "Content-Type": "application/x-www-form-urlencoded; charset=UTF-8" },
                body: body.toString()
            });

            return await response.json();
        } catch (error) {
            return {
                success: false,
                message: "Members Not in PreDispatch."
            };
        }
    }

    async function loadClientBatchItems(batchNos) {
        var modal = bySelector("[data-vision-scanner]");
        var endpoint = modal ? modal.getAttribute("data-dispatch-batch-endpoint") : "";
        var batches = normalizeBatchList(batchNos);

        if (!endpoint || !batches.length) {
            return {
                success: false,
                message: "Client batch No not found.",
                items: []
            };
        }

        var body = new URLSearchParams();
        body.append("clientBatchNos", JSON.stringify(batches));

        try {
            var response = await fetch(endpoint, {
                method: "POST",
                headers: { "Content-Type": "application/x-www-form-urlencoded; charset=UTF-8" },
                body: body.toString()
            });

            return await response.json();
        } catch (error) {
            return {
                success: false,
                message: "Client batch No not found.",
                items: []
            };
        }
    }

    async function addValidatedDispatchInput() {
        var input = bySelector("#DispatchMemberId");
        var scanType = getSelectedScanType();
        var scanValue = input ? input.value.trim() : "";

        if (!scanValue) {
            setDispatchValidationStatus(scanType === "MemberId"
                ? "Members Not in PreDispatch."
                : "Enter Client Batch No.", "invalid");
            return;
        }

        setDispatchValidationStatus("", "loading");

        if (scanType !== "MemberId") {
            var batchResult = await addClientBatchRows([scanValue]);
            if (batchResult > 0) {
                setDispatchValidationStatus(batchResult + " batch(es) loaded.", "valid");
                if (input) {
                    input.value = "";
                }
            }
            return;
        }

        var result = await validateDispatchScan(scanType, scanValue);

        if (!result.success) {
            setDispatchValidationStatus(result.message || "Members Not in PreDispatch.", "invalid");
            return;
        }

        if (addDispatchItem({
            preDispatchCode: result.item ? result.item.PreDispatchCode || result.item.preDispatchCode || result.item.PalletPreDispatchCode || result.item.palletPreDispatchCode || "-" : "-",
            palletCode: result.item ? result.item.PalletCode || result.item.palletCode || "-" : "-",
            clientBatchNo: result.item ? result.item.ClientBatchNo || result.item.clientBatchNo || scanValue : scanValue,
            sheetSize: result.item ? formatSheetSize(result.item) : "-"
        })) {
            renderDispatchItems();
            setDispatchValidationStatus(result.message || "Validated successfully.", "valid");
            if (input) {
                input.value = "";
            }
        } else {
            setDispatchValidationStatus("Already added.", "invalid");
        }
    }

    function stopCamera() {
        if (activeStream) {
            activeStream.getTracks().forEach(function (track) {
                track.stop();
            });
            activeStream = null;
        }

        var video = bySelector("[data-vision-video]");
        if (video) {
            video.srcObject = null;
        }

        var modal = bySelector("[data-vision-scanner]");
        if (modal) {
            modal.hidden = true;
        }
    }

    async function openCamera() {
        var modal = bySelector("[data-vision-scanner]");
        var video = bySelector("[data-vision-video]");
        if (!modal || !video) {
            return;
        }

        modal.hidden = false;

        if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
            setVisionStatus("Camera is not available in this browser. Please use HTTPS or localhost.");
            return;
        }

        try {
            setVisionStatus("Opening camera...");
            activeStream = await navigator.mediaDevices.getUserMedia({
                video: { facingMode: { ideal: "environment" } },
                audio: false
            });
            video.srcObject = activeStream;
            await video.play();
            setVisionStatus("Position the dispatch text clearly, then capture.");
        } catch (error) {
            setVisionStatus("Camera permission is required to read text.");
        }
    }

    function captureImage() {
        var video = bySelector("[data-vision-video]");
        if (!video || !video.videoWidth || !video.videoHeight) {
            return "";
        }

        var maxSize = 1280;
        var scale = Math.min(1, maxSize / Math.max(video.videoWidth, video.videoHeight));
        captureCanvas.width = Math.round(video.videoWidth * scale);
        captureCanvas.height = Math.round(video.videoHeight * scale);
        captureCanvas.getContext("2d").drawImage(video, 0, 0, captureCanvas.width, captureCanvas.height);

        return captureCanvas.toDataURL("image/jpeg", 0.85);
    }

    async function readTextFromCamera() {
        var imageData = captureImage();

        if (!imageData) {
            setVisionStatus("Camera image could not be captured.");
            return;
        }

        await readTextFromImage(imageData);
    }

    async function readTextFromImage(imageData) {
        var modal = bySelector("[data-vision-scanner]");
        var endpoint = modal ? modal.getAttribute("data-vision-endpoint") : "";

        if (!endpoint) {
            setVisionStatus("Google Vision endpoint is not available.");
            return;
        }

        try {
            setVisionStatus("Reading text with Google Vision...");
            var response = await fetch(endpoint, {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ imageData: imageData })
            });
            var result = await response.json();

            if (!result.success) {
                setVisionStatus(result.message || "Google Vision could not read this image.");
                return;
            }

            var addedCount = await applyVisionResult(result);
            setVisionStatus(addedCount > 0
                ? addedCount + " batch(es) added from loading slip."
                : "Text captured, but no Client Batch No / Product No batches were found.");
            setTimeout(stopCamera, 900);
        } catch (error) {
            setVisionStatus("Unable to call Google Vision. Check network and API key.");
        }
    }

    async function applyVisionResult(result) {
        var fields = result.fields || {};
        var textArea = bySelector("[data-vision-text]");

        if (textArea) {
            textArea.hidden = false;
            textArea.value = result.text || "";
        }

        var addedCount = 0;
        var ocrItems = Array.isArray(fields.dispatchItems) ? fields.dispatchItems : [];
        var batchNos = [];

        if (ocrItems.length) {
            batchNos = normalizeBatchList(ocrItems.map(function (item) {
                return item.clientBatchNo;
            }));
        } else {
            batchNos = normalizeBatchList([fields.clientBatchNo || fields.productNo]);
        }

        setValue("#DispatchMemberId", batchNos.length ? batchNos.join(", ") : fields.memberId || fields.preDispatchCode);

        if (batchNos.length) {
            addedCount = await addClientBatchRows(batchNos);
        } else if (fields.preDispatchCode || fields.sheetSize) {
            addedCount = addDispatchItem({
                preDispatchCode: fields.preDispatchCode || "-",
                clientBatchNo: "-",
                sheetSize: fields.sheetSize || "-"
            }) ? 1 : 0;
        }

        renderDispatchItems();
        return addedCount;
    }

    async function addClientBatchRows(batchNos) {
        var result = await loadClientBatchItems(batchNos);
        var items = Array.isArray(result.items) ? result.items : [];
        var requestedBatches = normalizeBatchList(batchNos);
        var foundKeys = {};
        var missingBatches = [];
        var addedCount = 0;

        items.forEach(function (item) {
            var returnedBatch = item.ClientBatchNo || item.clientBatchNo || "";
            if (returnedBatch) {
                foundKeys[String(returnedBatch).trim().toUpperCase()] = true;
            }

            if (addDispatchItem({
                preDispatchCode: item.PreDispatchCode || item.preDispatchCode || item.PalletPreDispatchCode || item.palletPreDispatchCode || "-",
                palletCode: item.PalletCode || item.palletCode || "-",
                clientBatchNo: returnedBatch || "-",
                sheetSize: formatSheetSize(item)
            })) {
                addedCount += 1;
            }
        });

        requestedBatches.forEach(function (batch) {
            if (!foundKeys[batch.toUpperCase()]) {
                missingBatches.push(batch);
            }
        });

        renderDispatchItems();

        if (missingBatches.length) {
            setDispatchValidationStatus("Some Members are not In PreDispatch.", "invalid");
            return addedCount;
        }

        setDispatchValidationStatus(addedCount > 0 ? addedCount + " batch(es) loaded." : "Client Batch No Already Exist.", addedCount > 0 ? "valid" : "invalid");
        return addedCount;
    }

    function formatSheetSize(item) {
        var sheetLength = item.SheetLength || item.sheetLength || "";
        var width = item.Width || item.width || "";
        var sheetSize = item.SheetSize || item.sheetSize || "";

        if (sheetSize) {
            return sheetSize;
        }

        if (sheetLength && width) {
            return sheetLength + " x " + width;
        }

        return sheetLength || width || "-";
    }

    function setValue(selector, value) {
        var input = bySelector(selector);
        if (!input || !value) {
            return;
        }

        input.value = value;
        input.dispatchEvent(new Event("input", { bubbles: true }));
        input.dispatchEvent(new Event("change", { bubbles: true }));
    }

    function renderDispatchItems() {
        var list = bySelector("[data-dispatch-list]");
        if (!list) {
            return;
        }

        if (!dispatchItems.length) {
            list.innerHTML = "<p>No dispatch items added yet.</p>";
            return;
        }

        list.innerHTML = dispatchItems.map(function (item, index) {
            var preDispatchCode = escapeHtml(item.preDispatchCode);
            var clientBatchNo = escapeHtml(item.clientBatchNo);
            var sheetSize = escapeHtml(item.sheetSize);
            var palletCode = escapeHtml(item.palletCode);
            return [
                "<div class=\"apts-dispatch-row\">",
                "<span>" + preDispatchCode + "</span>",
                "<strong>" + clientBatchNo + "</strong>",
                "<span>" + sheetSize + "</span>",                "<input type=\"hidden\" name=\"DispatchItems[" + index + "].PreDispatchCode\" value=\"" + preDispatchCode + "\" />",
                "<input type=\"hidden\" name=\"DispatchItems[" + index + "].PalletCode\" value=\"" + palletCode + "\" />",
                "<input type=\"hidden\" name=\"DispatchItems[" + index + "].ClientBatchNo\" value=\"" + clientBatchNo + "\" />",
                "<input type=\"hidden\" name=\"DispatchItems[" + index + "].SheetSize\" value=\"" + sheetSize + "\" />",
                "</div>"
            ].join("");
        }).join("");
    }

    function addDispatchItem(item) {
        var clientBatchNo = item && item.clientBatchNo ? String(item.clientBatchNo).trim() : "";
        if (!clientBatchNo || clientBatchNo === "-") {
            return false;
        }

        var exists = dispatchItems.some(function (existing) {
            return String(existing.clientBatchNo).toUpperCase() === clientBatchNo.toUpperCase();
        });
        if (exists) {
            return false;
        }

        dispatchItems.push({
            preDispatchCode: item.preDispatchCode || "-",
            palletCode: item.palletCode || "-",
            clientBatchNo: clientBatchNo,
            sheetSize: item.sheetSize || "-"
        });
        return true;
    }

    function readImageFile(file) {
        if (!file) {
            return;
        }

        var reader = new FileReader();
        reader.onload = function () {
            readTextFromImage(reader.result);
        };
        reader.onerror = function () {
            setVisionStatus("Selected image could not be read.");
        };
        setVisionStatus("Reading selected loading slip image...");
        reader.readAsDataURL(file);
    }

    function escapeHtml(value) {
        return String(value || "").replace(/[&<>"']/g, function (character) {
            return {
                "&": "&amp;",
                "<": "&lt;",
                ">": "&gt;",
                "\"": "&quot;",
                "'": "&#39;"
            }[character];
        });
    }

    function closeAllCustomSelects(exceptSelect) {
        document.querySelectorAll("[data-custom-select]").forEach(function (select) {
            if (select !== exceptSelect) {
                closeCustomSelect(select);
            }
        });
    }

function showDispatchFormMessage(message) {
        setDispatchValidationStatus(message || "", message ? "invalid" : "");
    }

    function validateDispatchStartForm(form) {
        var customer = form ? form.querySelector("#DispatchCustomer") : null;
        var consignee = form ? form.querySelector("#DispatchConsignee") : null;

        if (customer && !customer.value) {
            showDispatchFormMessage("Please select To Customer.");
            return false;
        }

        if (consignee && !consignee.value) {
            showDispatchFormMessage("Please select Consignee.");
            return false;
        }

        return true;
    }
    document.addEventListener("click", function (event) {
        var selectButton = event.target.closest("[data-custom-select-button]");
        if (selectButton) {
            var select = selectButton.closest("[data-custom-select]");
            var menu = select.querySelector("[data-custom-select-menu]");
            var shouldOpen = menu.hidden;
            closeAllCustomSelects(select);
            menu.hidden = !shouldOpen;
            selectButton.setAttribute("aria-expanded", shouldOpen ? "true" : "false");
            select.classList.toggle("is-open", shouldOpen);
            return;
        }

        var option = event.target.closest("[data-custom-select-menu] [role='option']");
        if (option) {
            var field = option.closest(".apts-custom-select-field");
            var selectRoot = option.closest("[data-custom-select]");
            var valueInput = field.querySelector("[data-custom-select-value]");
            var label = selectRoot.querySelector("[data-custom-select-label]");
            var value = option.getAttribute("data-value") || option.textContent.trim();
            var displayText = option.textContent.trim();
            var address = option.getAttribute("data-address") || "";
            valueInput.value = value;
            label.textContent = displayText;
            valueInput.dispatchEvent(new Event("change", { bubbles: true }));
            if (field.querySelector("#DispatchConsignee")) {
                var addressInput = bySelector("#CustomerAddress");
                if (addressInput) {
                    addressInput.value = address;
                    addressInput.dispatchEvent(new Event("input", { bubbles: true }));
                    addressInput.dispatchEvent(new Event("change", { bubbles: true }));
                }
            }
            closeCustomSelect(selectRoot);
            return;
        }

        if (event.target.closest("[data-vision-open]")) {
            openCamera();
            return;
        }

        if (event.target.closest("[data-vision-capture]")) {
            readTextFromCamera();
            return;
        }

        if (event.target.closest("[data-vision-close]")) {
            stopCamera();
            return;
        }

        if (event.target.closest("[data-dispatch-add]")) {
            addValidatedDispatchInput();
            return;
        }

        if (!event.target.closest("[data-custom-select]")) {
            closeAllCustomSelects();
        }
    });

    document.addEventListener("change", function (event) {
        var uploadInput = event.target.closest("[data-upload-input]");
        if (uploadInput) {
            var label = document.querySelector("[data-upload-label]");
            if (label) {
                label.textContent = uploadInput.files && uploadInput.files.length
                    ? uploadInput.files[0].name
                    : "UPLOAD";
            }
            return;
        }

        var visionFile = event.target.closest("[data-vision-file]");
        if (visionFile) {
            readImageFile(visionFile.files && visionFile.files[0]);
        }

        if (event.target.closest("input[name='scanType']")) {
            updateScanModeText();
            setDispatchValidationStatus("", "");
        }
    });

    document.addEventListener("input", function (event) {
        if (event.target.closest("#DispatchMemberId")) {
            setDispatchValidationStatus("", "");
        }

        var searchInput = event.target.closest("[data-custom-select-search]");
        if (!searchInput) {
            return;
        }

        var menu = searchInput.closest("[data-custom-select-menu]");
        var searchText = searchInput.value.trim().toUpperCase();
        menu.querySelectorAll("[role='option']").forEach(function (option) {
            option.hidden = searchText.length > 0
                && option.textContent.toUpperCase().indexOf(searchText) === -1;
        });
    });

    document.addEventListener("submit", function (event) {
        var form = event.target && event.target.closest(".apts-pallet-dispatch-form");
        if (!form) {
            return;
        }

        if (!validateDispatchStartForm(form)) {
            event.preventDefault();
        }
    });
    document.addEventListener("keydown", function (event) {
        if (event.key === "Escape") {
            stopCamera();
            closeAllCustomSelects();
        }
    });
    updateScanModeText();
}());
