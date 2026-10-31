(function () {
    var results = document.querySelector("[data-sku-results]");
    var orderGrid = document.querySelector("[data-order-grid]");
    var skuPopup = document.querySelector("[data-sku-popup]");
    var skuPopupSummary = document.querySelector("[data-sku-popup-summary]");
    var orderLines = [];

    function bySelector(selector) {
        return document.querySelector(selector);
    }

    function getForm() {
        return bySelector("[data-pallet-order-form]");
    }

    function getFormUrl(attributeName) {
        var form = getForm();
        return form ? form.getAttribute(attributeName) : "";
    }

    function setStatus(state, message) {
        var status = bySelector("[data-pallet-order-status]");
        if (!status) {
            return;
        }

        status.textContent = message || "";
        status.classList.toggle("is-valid", state === "valid");
        status.classList.toggle("is-invalid", state === "invalid");
        status.classList.toggle("is-loading", state === "loading");
    }

    function htmlEncode(value) {
        return String(value || "")
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#39;");
    }

    function getValue(selector) {
        var element = bySelector(selector);
        return element ? element.value.trim() : "";
    }

    function getShapeText() {
        var shapeInput = document.querySelector("#PalletShape");
        var field = shapeInput ? shapeInput.closest(".apts-custom-select-field") : null;
        var label = field ? field.querySelector("[data-custom-select-label]") : null;
        return label ? label.textContent.trim() : "";
    }

    function showProcessMessage(message) {
        if (window.AptsProcessPopup) {
            window.AptsProcessPopup.show(message);
            return;
        }

        setStatus("invalid", message);
    }

    function normalizeNumberValue(value, fallbackValue) {
        value = String(value || "").trim();
        if (!value) {
            return fallbackValue;
        }

        return value.split(".")[0] || fallbackValue;
    }

    function getLineInput() {
        return {
            shapeCode: getValue("[data-sku-shape]"),
            shapeText: getShapeText(),
            width: getValue("[data-sku-width]"),
            length1: getValue("[data-sku-length1]"),
            length2: getValue("[data-sku-length2]"),
            qty: getValue("[data-line-qty]"),
            instruction: getValue("[data-line-instruction]")
        };
    }

    function buildLineInput() {
        var line = getLineInput();
        line.width = normalizeNumberValue(line.width, "0");
        line.length1 = normalizeNumberValue(line.length1, "0");
        line.length2 = normalizeNumberValue(line.length2, "0");
        line.qty = normalizeNumberValue(line.qty, "0");
        return line;
    }

    function setResultsMessage(message) {
        if (results) {
            results.innerHTML = "<p>" + htmlEncode(message) + "</p>";
        }
    }

    function openSkuPopup() {
        if (skuPopup) {
            skuPopup.hidden = false;
        }
    }

    function closeSkuPopup() {
        if (skuPopup) {
            skuPopup.hidden = true;
        }
    }

    function renderSkuSummary(line) {
        if (!skuPopupSummary) {
            return;
        }
        skuPopupSummary.innerHTML = [
            '<table class="apts-sku-table apts-sku-popup-summary-table">',
            '<thead><tr><th>Shape</th><th>Width</th><th>Length 1</th><th>Length 2</th></tr></thead>',
            '<tbody><tr>',
            '<td>' + htmlEncode(line.shapeText || line.shapeCode) + '</td>',
            '<td>' + htmlEncode(line.width) + '</td>',
            '<td>' + htmlEncode(line.length1) + '</td>',
            '<td>' + htmlEncode(line.length2 || '0') + '</td>',
            '</tr></tbody>',
            '</table>'
        ].join("");
    }

    function renderSkuResults(matches) {
        if (!results) {
            return;
        }

        if (!matches || !matches.length) {
            setResultsMessage("No matching SKU found for the selected details.");
            return;
        }

        var rows = matches.map(function (item, index) {
            var code = item.PalletSkewCode || item.palletSkewCode || "";
            var width = item.SheetWidth || item.sheetWidth || "";
            var length1 = item.SheetLength1 || item.sheetLength1 || "";
            var length2 = item.SheetLength2 || item.sheetLength2 || "";
            var cmSize = item.CMSize || item.cmSize || "";
            var lm1Size = item.LM1Size || item.lm1Size || "";
            var lm2Size = item.LM2Size || item.lm2Size || "";
            var cmQty = item.CMSizeQty || item.cmSizeQty || "";
            var lm1Qty = item.LM1SizeQty || item.lm1SizeQty || "";
            var lm2Qty = item.LM2SizeQty || item.lm2SizeQty || "";
            var checkboxId = "SkuMatch" + index;

            return [
                '<tr data-sku-result-row data-pallet-skew-code="' + htmlEncode(code) + '">',
                '<td class="apts-sku-select"><input id="' + checkboxId + '" type="checkbox" value="' + htmlEncode(code) + '" data-sku-row-check /></td>',
                '<td class="apts-sku-size">' + htmlEncode(width) + '</td>',
                '<td class="apts-sku-size">' + htmlEncode(length1) + '</td>',
                '<td class="apts-sku-size">' + htmlEncode(length2) + '</td>',
                '<td class="apts-sku-size">' + htmlEncode(cmSize) + '</td>',
                '<td class="apts-sku-size">' + htmlEncode(lm1Size) + '</td>',
                '<td class="apts-sku-size">' + htmlEncode(lm2Size) + '</td>',
                '<td class="apts-sku-size">' + htmlEncode(cmQty) + '</td>',
                '<td class="apts-sku-size">' + htmlEncode(lm1Qty) + '</td>',
                '<td class="apts-sku-size">' + htmlEncode(lm2Qty) + '</td>',
                '</tr>'
            ].join("");
        }).join("");

        results.innerHTML = [
            '<div class="apts-sku-table-wrap">',
            '<table class="apts-sku-table apts-sku-popup-table">',
            '<thead><tr>',
            '<th>Check</th>',
            '<th>SheetWidth</th>',
            '<th>SheetLength1</th>',
            '<th>SheetLength2</th>',
            '<th>CMSize</th>',
            '<th>LM1Size</th>',
            '<th>LM2Size</th>',
            '<th>CMQty</th>',
            '<th>LM1Qty</th>',
            '<th>LM2Qty</th>',
            '</tr></thead>',
            '<tbody>',
            rows,
            '</tbody>',
            '</table>',
            '</div>'
        ].join("");
    }

    function renderOrderGrid() {
        if (!orderGrid) {
            return;
        }

        if (!orderLines.length) {
            orderGrid.innerHTML = "<p>No line items added yet.</p>";
            return;
        }

        var rows = orderLines.map(function (line, index) {
            return [
                '<tr data-order-line-index="' + index + '">',
                '<td>' + htmlEncode(line.shapeText || line.shapeCode) + '<input type="hidden" name="OrderShape" value="' + htmlEncode(line.shapeCode) + '" /><input type="hidden" name="OrderShapeText" value="' + htmlEncode(line.shapeText) + '" /><input type="hidden" name="OrderSkuCode" value="' + htmlEncode(line.skuCode) + '" /><input type="hidden" name="OrderInstruction" value="' + htmlEncode(line.instruction) + '" /></td>',
                '<td>' + htmlEncode(line.width) + '<input type="hidden" name="OrderWidth" value="' + htmlEncode(line.width) + '" /></td>',
                '<td>' + htmlEncode(line.length1) + '<input type="hidden" name="OrderLength1" value="' + htmlEncode(line.length1) + '" /></td>',
                '<td>' + htmlEncode(line.length2 || '0') + '<input type="hidden" name="OrderLength2" value="' + htmlEncode(line.length2 || '0') + '" /></td>',
                '<td>' + htmlEncode(line.qty) + '<input type="hidden" name="OrderQty" value="' + htmlEncode(line.qty) + '" /></td>',
                '<td><button class="apts-grid-icon-button" type="button" data-order-line-remove aria-label="Remove line item">×</button></td>',
                '</tr>'
            ].join("");
        }).join("");

        orderGrid.innerHTML = [
            '<div class="apts-sku-table-wrap apts-order-grid-wrap">',
            '<table class="apts-sku-table apts-order-grid-table">',
            '<thead><tr><th>Shape</th><th>Width</th><th>Length 1</th><th>Length 2</th><th>Qty</th><th></th></tr></thead>',
            '<tbody>',
            rows,
            '</tbody></table></div>'
        ].join("");
    }

    function resetCustomSelect(field, placeholder) {
        var valueInput = field ? field.querySelector("[data-custom-select-value]") : null;
        var label = field ? field.querySelector("[data-custom-select-label]") : null;
        var select = field ? field.querySelector("[data-custom-select]") : null;
        var menu = field ? field.querySelector("[data-custom-select-menu]") : null;
        var button = field ? field.querySelector("[data-custom-select-button]") : null;

        if (valueInput) {
            valueInput.value = "";
            valueInput.dispatchEvent(new Event("change", { bubbles: true }));
        }

        if (label) {
            label.textContent = placeholder;
        }

        if (select) {
            select.classList.remove("is-open");
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

    function resetEntryFields() {
        ["[data-sku-width]", "[data-sku-length1]", "[data-sku-length2]", "[data-line-qty]", "[data-line-instruction]"].forEach(function (selector) {
            var input = bySelector(selector);
            if (input) {
                input.value = "";
                input.classList.remove("is-invalid");
            }
        });
    }

    function resetPalletOrderForm(form) {
        if (form) {
            form.reset();
        }

        resetCustomSelect(document.getElementById("PalletShape") ? document.getElementById("PalletShape").closest(".apts-custom-select-field") : null, "Select Shape");
        orderLines = [];
        renderOrderGrid();
        setResultsMessage("Enter pallet shape, width and length 1 to view matching SKUs.");
        setStatus("", "");
        closeSkuPopup();
    }

    function findSkuMatches() {
        var line = buildLineInput();
        var url = getFormUrl("data-sku-url");


        if (!url || !window.fetch) {
            setStatus("invalid", "Unable to load matching SKUs.");
            return;
        }

        setStatus("loading", "Loading matching SKUs...");
        renderSkuSummary(line);
        openSkuPopup();
        setResultsMessage("Loading matching SKUs...");

        fetch(url + "?shape=" + encodeURIComponent(line.shapeCode) + "&width=" + encodeURIComponent(line.width) + "&length=" + encodeURIComponent(line.length1), {
            credentials: "same-origin",
            headers: { "Accept": "application/json" }
        })
            .then(function (response) {
                return response.ok ? response.json() : null;
            })
            .then(function (result) {
                setStatus("", "");
                if (result && result.success) {
                    renderSkuResults(result.skus || []);
                    return;
                }

                setResultsMessage(result && result.message ? result.message : "Unable to load matching SKUs.");
            })
            .catch(function () {
                setStatus("invalid", "Unable to load matching SKUs.");
                setResultsMessage("Unable to load matching SKUs.");
            });
    }

    function addSelectedSkus() {
        var line = buildLineInput();
        var selected = document.querySelectorAll("[data-sku-row-check]:checked");


        if (!selected.length) {
            setStatus("invalid", "Please select at least one SKU.");
            return;
        }

        selected.forEach(function (checkbox) {
            orderLines.push({
                skuCode: checkbox.value,
                shapeCode: line.shapeCode,
                shapeText: line.shapeText,
                width: line.width,
                length1: line.length1,
                length2: line.length2 || "0",
                qty: line.qty,
                instruction: line.instruction
            });
        });

        renderOrderGrid();
        resetEntryFields();
        setStatus("", "");
        closeSkuPopup();
    }

    function hasOrderLines() {
        return orderLines.length > 0;
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

    function queueChallanAfterSave(form, palletOrderCode) {
        var endpoint = form ? form.getAttribute("data-pallet-order-challan-url") : "";
        if (!endpoint || !palletOrderCode || !window.sessionStorage) {
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
                paramName: "palletOrderCode",
                code: palletOrderCode,
                attempts: 0
            });
            window.sessionStorage.setItem(storageKey, JSON.stringify(pendingItems));
            return true;
        } catch (error) {
            return false;
        }
    }

    function submitPalletOrder(form) {
        var url = getFormUrl("data-submit-url");
        var submitButton = form ? form.querySelector("[type='submit']") : null;

        if (!hasOrderLines()) {
            showProcessMessage("Please add at least one line item.");
            return;
        }

        if (!url || !form || !window.fetch) {
            setStatus("invalid", "Unable to submit pallet order.");
            return;
        }

        setStatus("loading", "");
        if (submitButton) {
            submitButton.disabled = true;
        }

        fetch(url, {
            method: "POST",
            credentials: "same-origin",
            body: new FormData(form),
            headers: { "Accept": "application/json" }
        })
            .then(function (response) {
                return response.ok ? response.json() : null;
            })
            .then(function (result) {
                if (result && result.success) {
                    setStatus("", "");
                    if (window.AptsProcessPopup) {
                        window.AptsProcessPopup.show(result.message || "Pallet Order Submitted Successfully");
                    } else {
                        setStatus("valid", result.message || "Pallet Order Submitted Successfully");
                    }
                    if (result.challanPending && result.palletOrderCode) {
                        queueChallanAfterSave(form, result.palletOrderCode);
                    }
                    resetPalletOrderForm(form);
                    return;
                }

                setStatus("invalid", result && result.message ? result.message : "Unable to submit pallet order.");
            })
            .catch(function () {
                setStatus("invalid", "Unable to submit pallet order.");
            })
            .then(function () {
                if (submitButton) {
                    submitButton.disabled = false;
                }
            });
    }

    document.addEventListener("click", function (event) {
        if (event.target.closest("[data-sku-search]")) {
            findSkuMatches();
            return;
        }

        if (event.target.closest("[data-sku-popup-close]")) {
            closeSkuPopup();
            return;
        }

        if (event.target.closest("[data-sku-add]")) {
            addSelectedSkus();
            return;
        }

        var resultRow = event.target.closest("[data-sku-result-row]");
        if (resultRow && !event.target.matches("input")) {
            var checkbox = resultRow.querySelector("[data-sku-row-check]");
            if (checkbox) {
                checkbox.checked = !checkbox.checked;
                resultRow.classList.toggle("is-selected", checkbox.checked);
            }
            return;
        }

        var removeButton = event.target.closest("[data-order-line-remove]");
        if (removeButton) {
            var row = removeButton.closest("[data-order-line-index]");
            var index = row ? parseInt(row.getAttribute("data-order-line-index"), 10) : -1;
            if (index > -1) {
                orderLines.splice(index, 1);
                renderOrderGrid();
            }
            return;
        }
    });

    document.addEventListener("change", function (event) {
        if (event.target.matches("[data-sku-row-check]")) {
            var row = event.target.closest("[data-sku-result-row]");
            if (row) {
                row.classList.toggle("is-selected", event.target.checked);
            }
        }
    });

    document.addEventListener("submit", function (event) {
        var form = event.target && event.target.closest("[data-pallet-order-form]");
        if (!form) {
            return;
        }

        event.preventDefault();
        submitPalletOrder(form);
    });

    document.addEventListener("input", function (event) {
        if (event.target.matches("[data-sku-width], [data-sku-length1], [data-sku-length2], [data-line-qty]")) {
            event.target.classList.remove("is-invalid");
        }
    });

    document.addEventListener("keydown", function (event) {
        if (event.key === "Escape") {
            closeSkuPopup();
        }
    });
}());