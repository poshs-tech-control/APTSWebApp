(function () {
    var modal = document.querySelector("[data-instruction-modal]");
    var instructionText = document.querySelector("[data-instruction-text]");
    var instructionInput = document.getElementById("Instructions");
    var instructionPreview = document.getElementById("InstructionsPreview");
    var results = document.querySelector("[data-sku-results]");
    var skuDetailModal = document.querySelector("[data-sku-detail-modal]");
    var skuDetailRows = document.querySelector("[data-sku-detail-rows]");
    var skuDetailQty = document.querySelector("[data-sku-detail-qty]");
    var activeSkuRow = null;

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

    function closeInstructionModal() {
        if (modal) {
            modal.hidden = true;
        }
    }

    function openInstructionModal() {
        if (!modal || !instructionText || !instructionInput) {
            return;
        }

        instructionText.value = instructionInput.value;
        modal.hidden = false;
        instructionText.focus();
    }

    function saveInstructions() {
        var value = instructionText ? instructionText.value.trim() : "";
        if (instructionInput) {
            instructionInput.value = value;
        }
        if (instructionPreview) {
            instructionPreview.textContent = value || "Add pallet instructions";
        }
        closeInstructionModal();
    }

    function renderSkuResults(matches) {
        if (!results) {
            return;
        }

        if (!matches || !matches.length) {
            results.innerHTML = "<p>No matching SKU found for the selected details.</p>";
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
                '<tr data-sku-result-row data-pallet-skew-code="' + htmlEncode(code) + '" data-sheet-width="' + htmlEncode(width) + '" data-sheet-length1="' + htmlEncode(length1) + '" data-sheet-length2="' + htmlEncode(length2) + '" data-cm-size="' + htmlEncode(cmSize) + '" data-lm1-size="' + htmlEncode(lm1Size) + '" data-lm2-size="' + htmlEncode(lm2Size) + '" data-cm-qty="' + htmlEncode(cmQty) + '" data-lm1-qty="' + htmlEncode(lm1Qty) + '" data-lm2-qty="' + htmlEncode(lm2Qty) + '">',
                '<td class="apts-sku-size">' + htmlEncode(width) + "</td>",
                '<td class="apts-sku-size">' + htmlEncode(length1) + "</td>",
                '<td class="apts-sku-size">' + htmlEncode(length2) + "</td>",
                '<td class="apts-sku-size">' + htmlEncode(cmSize) + "</td>",
                '<td class="apts-sku-size">' + htmlEncode(lm1Size) + "</td>",
                '<td class="apts-sku-size">' + htmlEncode(lm2Size) + "</td>",
                '<td class="apts-sku-size">' + htmlEncode(cmQty) + "</td>",
                '<td class="apts-sku-size">' + htmlEncode(lm1Qty) + "</td>",
                '<td class="apts-sku-size">' + htmlEncode(lm2Qty) + "</td>",
                '<td class="apts-sku-select"><input id="' + checkboxId + '" type="checkbox" name="SelectedSkus" value="' + htmlEncode(code) + '" data-sku-row-check /></td>',
                '<td class="apts-sku-qty-cell"><input class="apts-sku-qty" type="number" name="SkuQty_' + htmlEncode(code) + '" min="0" value="0" readonly data-sku-row-qty /></td>',
                "</tr>"
            ].join("");
        }).join("");

        results.innerHTML = [
            '<div class="apts-sku-table-wrap">',
            '<table class="apts-sku-table">',
            "<thead>",
            "<tr>",
            "<th>SheetWidth</th>",
            "<th>SheetLength1</th>",
            "<th>SheetLength2</th>",
            "<th>CMSize</th>",
            "<th>LM1Size</th>",
            "<th>LM2Size</th>",
            "<th>CMQty</th>",
            "<th>LM1Qty</th>",
            "<th>LM2Qty</th>",
            "<th>Check</th>",
            "<th>Qty</th>",
            "</tr>",
            "</thead>",
            "<tbody>",
            rows,
            "</tbody>",
            "</table>",
            "</div>"
        ].join("");
    }

    function setResultsMessage(message) {
        if (results) {
            results.innerHTML = "<p>" + htmlEncode(message) + "</p>";
        }
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

    function resetPalletOrderForm(form) {
        if (form) {
            form.reset();
        }

        resetCustomSelect(document.getElementById("PalletShape") ? document.getElementById("PalletShape").closest(".apts-custom-select-field") : null, "Select Shape");

        if (instructionInput) {
            instructionInput.value = "";
        }

        if (instructionText) {
            instructionText.value = "";
        }

        if (instructionPreview) {
            instructionPreview.textContent = "Add pallet instructions";
        }

        setResultsMessage("Enter pallet shape, width and length to view matching SKUs.");
        setStatus("", "");
        closeInstructionModal();
        closeSkuDetailModal();
    }

    function updateSelectedCustomerCode(option) {
        var locationField = document.getElementById("Location");
        var customerCodeInput = document.getElementById("CustomerCode");

        if (!locationField || !customerCodeInput || !option) {
            return;
        }

        var field = option.closest(".apts-custom-select-field");
        if (!field || !field.contains(locationField)) {
            return;
        }

        customerCodeInput.value = option.getAttribute("data-customer-code") || "";
    }

    function findSkuMatches() {
        var url = getFormUrl("data-sku-url");
        var shapeField = bySelector("[data-sku-shape]");
        var widthField = bySelector("[data-sku-width]");
        var lengthField = bySelector("[data-sku-length]");
        var shape = shapeField ? shapeField.value.trim() : "";
        var width = widthField ? widthField.value.trim() : "";
        var length = lengthField ? lengthField.value.trim() : "";

        setStatus("", "");

        if (!shape || !width || !length) {
            setResultsMessage("Enter pallet shape, width and length to view matching SKUs.");
            return;
        }

        if (!url || !window.fetch) {
            setResultsMessage("Unable to load matching SKUs.");
            return;
        }

        setResultsMessage("Loading matching SKUs...");

        fetch(url + "?shape=" + encodeURIComponent(shape) + "&width=" + encodeURIComponent(width) + "&length=" + encodeURIComponent(length), {
            credentials: "same-origin",
            headers: { "Accept": "application/json" }
        })
            .then(function (response) {
                return response.ok ? response.json() : null;
            })
            .then(function (result) {
                if (result && result.success) {
                    renderSkuResults(result.skus || []);
                    return;
                }

                setResultsMessage(result && result.message ? result.message : "Unable to load matching SKUs.");
            })
            .catch(function () {
                setResultsMessage("Unable to load matching SKUs.");
            });
    }

    function updateRowQtyState(checkbox) {
        var row = checkbox ? checkbox.closest("tr") : null;
        var qtyInput = row ? row.querySelector("[data-sku-row-qty]") : null;
        if (row) {
            row.classList.toggle("is-selected", checkbox && checkbox.checked);
        }
        if (qtyInput && checkbox && !checkbox.checked) {
            qtyInput.value = "0";
        }
    }

    function setAllRows(checked) {
        document.querySelectorAll("[data-sku-row-check]").forEach(function (checkbox) {
            checkbox.checked = checked;
            updateRowQtyState(checkbox);
        });
    }

    function hasSelectedSku() {
        return !!bySelector("[data-sku-row-check]:checked");
    }

    function hasSelectedSkuWithQty() {
        var invalid = false;
        document.querySelectorAll("[data-sku-row-check]:checked").forEach(function (checkbox) {
            var row = checkbox.closest("tr");
            var qtyInput = row ? row.querySelector("[data-sku-row-qty]") : null;
            var qty = qtyInput ? parseInt(qtyInput.value, 10) : 0;
            if (!qty || qty < 1) {
                invalid = true;
            }
        });

        return !invalid;
    }

    function getSkuDetailPairs(row) {
        return [
            ["Pallet SKU", row.getAttribute("data-pallet-skew-code") || "-"],
            ["Sheet Width", row.getAttribute("data-sheet-width") || "-"],
            ["Sheet Length 1", row.getAttribute("data-sheet-length1") || "-"],
            ["Sheet Length 2", row.getAttribute("data-sheet-length2") || "-"],
            ["CM Size", row.getAttribute("data-cm-size") || "-"],
            ["LM1 Size", row.getAttribute("data-lm1-size") || "-"],
            ["LM2 Size", row.getAttribute("data-lm2-size") || "-"],
            ["CM Qty", row.getAttribute("data-cm-qty") || "-"],
            ["LM1 Qty", row.getAttribute("data-lm1-qty") || "-"],
            ["LM2 Qty", row.getAttribute("data-lm2-qty") || "-"]
        ];
    }

    function openSkuDetailModal(row) {
        if (!skuDetailModal || !skuDetailRows || !skuDetailQty || !row) {
            return;
        }

        activeSkuRow = row;
        skuDetailRows.innerHTML = getSkuDetailPairs(row).map(function (pair) {
            return "<dt>" + htmlEncode(pair[0]) + "</dt><dd>" + htmlEncode(pair[1]) + "</dd>";
        }).join("");

        var qtyInput = row.querySelector("[data-sku-row-qty]");
        var currentQty = qtyInput && qtyInput.value !== "0" ? qtyInput.value : "";
        skuDetailQty.value = currentQty;
        skuDetailModal.hidden = false;
        skuDetailQty.focus();
        skuDetailQty.select();
    }

    function closeSkuDetailModal() {
        if (skuDetailModal) {
            skuDetailModal.hidden = true;
        }
        activeSkuRow = null;
    }

    function applySkuDetailQty() {
        if (!activeSkuRow || !skuDetailQty) {
            return;
        }

        var qty = parseInt(skuDetailQty.value, 10);
        if (!qty || qty < 1) {
            skuDetailQty.classList.add("is-invalid");
            skuDetailQty.focus();
            return;
        }

        var checkbox = activeSkuRow.querySelector("[data-sku-row-check]");
        var qtyInput = activeSkuRow.querySelector("[data-sku-row-qty]");
        if (checkbox) {
            checkbox.checked = true;
        }
        if (qtyInput) {
            qtyInput.value = String(qty);
        }
        skuDetailQty.classList.remove("is-invalid");
        updateRowQtyState(checkbox);
        closeSkuDetailModal();
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

    async function generateChallanInBackground(form, palletOrderCode) {
        var endpoint = form ? form.getAttribute("data-pallet-order-challan-url") : "";
        var body = new URLSearchParams();
        body.append("palletOrderCode", palletOrderCode || "");

        if (!endpoint || !palletOrderCode || !window.fetch) {
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
    function submitPalletOrder(form) {
        var url = getFormUrl("data-submit-url");
        var submitButton = form ? form.querySelector("[type='submit']") : null;

        if (!hasSelectedSku()) {
            setStatus("invalid", "Please select at least one SKU.");
            return;
        }

        if (!hasSelectedSkuWithQty()) {
            setStatus("invalid", "Please enter Qty for selected SKU.");
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
                        generateChallanInBackground(form, result.palletOrderCode);
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
        var selectedOption = event.target.closest("[data-custom-select-menu] [role='option']");
        if (selectedOption) {
            updateSelectedCustomerCode(selectedOption);
        }

        var row = event.target.closest("[data-sku-result-row]");
        if (row && !event.target.matches("input")) {
            openSkuDetailModal(row);
            return;
        }

        if (event.target.closest("[data-sku-detail-close]")) {
            closeSkuDetailModal();
            return;
        }

        if (event.target.closest("[data-sku-detail-ok]")) {
            applySkuDetailQty();
            return;
        }

        if (event.target.closest("[data-instruction-open]")) {
            openInstructionModal();
        }
        if (event.target.closest("[data-instruction-close]")) {
            closeInstructionModal();
        }
        if (event.target.closest("[data-instruction-save]")) {
            saveInstructions();
        }
        if (event.target.closest("[data-sku-search]")) {
            findSkuMatches();
        }
    });

    document.addEventListener("change", function (event) {
        if (event.target.matches("[data-sku-select-all]")) {
            setAllRows(event.target.checked);
        }

        if (event.target.matches("[data-sku-row-check]")) {
            updateRowQtyState(event.target);
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
        if (event.target.matches("[data-sku-detail-qty]")) {
            event.target.classList.remove("is-invalid");
        }
    });

    document.addEventListener("keydown", function (event) {
        if (event.key === "Enter" && event.target.matches("[data-sku-detail-qty]")) {
            event.preventDefault();
            applySkuDetailQty();
        }

        if (event.key === "Escape") {
            closeInstructionModal();
            closeSkuDetailModal();
        }
    });
}());


