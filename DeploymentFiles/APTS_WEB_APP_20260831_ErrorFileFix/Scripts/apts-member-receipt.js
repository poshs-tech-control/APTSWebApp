(function () {
    var members = [];
    var activeReceiptGroupIndex = null;

    function bySelector(selector) {
        return document.querySelector(selector);
    }

    function htmlEncode(value) {
        return String(value || "")
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#39;");
    }

    function getFormUrl(attributeName) {
        var form = bySelector("[data-member-receipt-form]");
        return form ? form.getAttribute(attributeName) : "";
    }

    function setReceiptSaveStatus(state, message) {
        var status = bySelector("[data-receipt-save-status]");

        if (!status) {
            return;
        }

        status.textContent = message || "";
        status.classList.toggle("is-valid", state === "valid");
        status.classList.toggle("is-invalid", state === "invalid");
        status.classList.toggle("is-loading", state === "loading");
    }

    function uppercaseInputValue(input) {
        if (input && input.value) {
            input.value = input.value.toUpperCase();
        }
    }

    function showReceiptPopup(message) {
        var text = typeof message === "string" ? message : "Unable to validate member.";

        if (window.AptsProcessPopup) {
            window.AptsProcessPopup.show(text);
            return;
        }

        alert(text);
    }

    function getReceiptSourceType() {
        var selected = document.querySelector("input[name='receiptFrom']:checked");
        return selected ? selected.value : "";
    }

    function resetReceiptLocationSelect() {
        var valueInput = bySelector("#ReceiptLocation");
        var nameInput = bySelector("#ReceiptLocationName");
        var field = valueInput ? valueInput.closest(".apts-custom-select-field") : null;
        var label = field ? field.querySelector("[data-custom-select-label]") : null;

        if (valueInput) {
            valueInput.value = "";
        }
        if (nameInput) {
            nameInput.value = "";
        }
        if (label) {
            label.textContent = "Select Location";
        }
        if (field) {
            field.querySelectorAll("[role='option']").forEach(function (item) {
                item.setAttribute("aria-selected", "false");
            });
        }
    }

    function filterReceiptLocationOptions() {
        var field = bySelector("#ReceiptLocation") ? bySelector("#ReceiptLocation").closest(".apts-custom-select-field") : null;
        var selectedType = getReceiptSourceType();
        var visibleCount = 0;
        var emptyOption = field ? field.querySelector("[data-receipt-empty]") : null;

        if (!field) {
            return;
        }

        field.querySelectorAll("[data-receipt-location-option]").forEach(function (option) {
            var isVisible = option.getAttribute("data-group") === selectedType;
            option.hidden = !isVisible;
            if (isVisible) {
                visibleCount += 1;
            }
        });

        if (emptyOption) {
            emptyOption.hidden = visibleCount !== 0;
        }
    }

    function refreshReceiptLocationSelect() {
        resetReceiptLocationSelect();
        filterReceiptLocationOptions();
    }

    function getReceiptGroups() {
        var groups = [];

        members.forEach(function (member) {
            var size = member.memberSize || "-";
            var type = member.memberType || "-";
            var key = String(size).toUpperCase() + "|" + String(type).toUpperCase();
            var group = groups.filter(function (item) {
                return item.key === key;
            })[0];

            if (!group) {
                group = {
                    key: key,
                    size: size,
                    type: type,
                    members: []
                };
                groups.push(group);
            }

            group.members.push(member);
        });

        return groups;
    }

    function renderMembers() {
        var list = bySelector("[data-receipt-list]");
        var count = bySelector("[data-receipt-count]");
        var groups = getReceiptGroups();

        if (count) {
            count.textContent = members.length;
        }

        if (!list) {
            return;
        }

        var rows = groups.length
            ? groups.map(function (group, index) {
                return [
                    '<tr class="apts-receipt-row apts-receipt-group-row" tabindex="0" role="button" data-receipt-group-index="' + index + '">',
                    "<td>" + htmlEncode(group.size || "-") + "</td>",
                    "<td>" + htmlEncode(group.type || "-") + "</td>",
                    "<td>" + htmlEncode(group.members.length) + "</td>",
                    "</tr>"
                ].join("");
            }).join("")
            : '<tr><td class="apts-receipt-empty-cell" colspan="3">No members scanned yet.</td></tr>';

        list.innerHTML = [
            '<div class="apts-sku-table-wrap apts-receipt-table-wrap">',
            '<table class="apts-sku-table apts-receipt-table apts-receipt-summary-table">',
            "<thead>",
            "<tr>",
            "<th>Size</th>",
            "<th>Type</th>",
            "<th>Count</th>",
            "</tr>",
            "</thead>",
            "<tbody>",
            rows,
            "</tbody>",
            "</table>",
            "</div>"
        ].join("");
    }

    function hasReceiptDiscrepancyAction(member) {
        return member && (member.actionType === "Damage" || member.actionType === "Loss");
    }

    function isReceiptDiscrepancyMember(member) {
        return member && member.actionType && member.actionType !== "Y";
    }

    function findReceiptMemberByRfid(rfid) {
        var key = String(rfid || "").toUpperCase();
        return members.filter(function (member) {
            return String(member.rfid || "").toUpperCase() === key;
        })[0] || null;
    }

    function updateReceiptSelectAllState() {
        var modal = bySelector("[data-receipt-group-modal]");
        var selectAll = modal ? modal.querySelector("[data-receipt-discrepancy-select-all]") : null;
        var checks = modal ? Array.prototype.slice.call(modal.querySelectorAll("[data-receipt-discrepancy-member]")) : [];

        if (!selectAll || !checks.length) {
            return;
        }

        var checkedCount = checks.filter(function (checkbox) { return checkbox.checked; }).length;
        selectAll.checked = checkedCount === checks.length;
        selectAll.indeterminate = checkedCount > 0 && checkedCount < checks.length;
    }

    function getSelectedReceiptDiscrepancyMembers() {
        return members.filter(isReceiptDiscrepancyMember);
    }

    function getEditableReceiptDiscrepancyMembers() {
        var selectedMembers = getSelectedReceiptDiscrepancyMembers();
        var pendingMembers = selectedMembers.filter(function (member) {
            return !hasReceiptDiscrepancyAction(member);
        });

        return pendingMembers.length ? pendingMembers : selectedMembers;
    }

    function buildReceiptSelectedSizeText(selectedMembers) {
        var counts = {};
        (selectedMembers || []).forEach(function (member) {
            var size = member.memberSize || "-";
            counts[size] = (counts[size] || 0) + 1;
        });

        return Object.keys(counts).map(function (size) {
            return counts[size] > 1 ? size + " (" + counts[size] + ")" : size;
        }).join(", ") || "-";
    }

    function closeReceiptDiscrepancyPopup() {
        var popup = bySelector("[data-receipt-discrepancy-popup]");
        if (popup) {
            popup.hidden = true;
            document.body.classList.remove("apts-process-popup-open");
        }
    }

    function ensureReceiptDiscrepancyPopup() {
        var popup = bySelector("[data-receipt-discrepancy-popup]");

        if (popup) {
            return popup;
        }

        popup = document.createElement("section");
        popup.className = "apts-dismantle-popup";
        popup.setAttribute("data-receipt-discrepancy-popup", "");
        popup.setAttribute("role", "dialog");
        popup.setAttribute("aria-modal", "true");
        popup.hidden = true;
        popup.innerHTML = [
            '<div class="apts-dismantle-popup__panel">',
            '<div class="apts-dismantle-popup__head">',
            '<h2 data-receipt-discrepancy-title></h2>',
            '<button type="button" data-receipt-discrepancy-close aria-label="Close popup">',
            '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M6 6l12 12M18 6 6 18" /></svg>',
            '</button>',
            '</div>',
            '<div class="apts-dismantle-popup__body" data-receipt-discrepancy-body></div>',
            '</div>'
        ].join("");

        document.body.appendChild(popup);
        return popup;
    }

    function showReceiptDiscrepancyPopup() {
        var selectedMembers = getSelectedReceiptDiscrepancyMembers();
        var editableMembers = getEditableReceiptDiscrepancyMembers();
        var firstAction = editableMembers.length ? editableMembers[0].actionType || "" : "";
        var firstComment = editableMembers.length ? editableMembers[0].comment || "" : "";
        var checkedAction = firstAction === "Damage" || firstAction === "Loss" ? firstAction : "";
        var popup;
        var title;
        var body;

        if (!members.length) {
            showReceiptPopup("Please scan member.");
            return;
        }

        if (!selectedMembers.length) {
            showReceiptPopup("Please select discrepancy member.");
            return;
        }

        popup = ensureReceiptDiscrepancyPopup();
        title = popup.querySelector("[data-receipt-discrepancy-title]");
        body = popup.querySelector("[data-receipt-discrepancy-body]");

        if (title) {
            title.textContent = "Pallet Discrepancy";
        }

        if (body) {
            body.innerHTML = [
                '<div class="apts-discrepancy-popup-form">',
                '<div class="apts-discrepancy-popup-summary">',
                '<span>Checked Member List</span>',
                '<strong>' + editableMembers.length + '</strong>',
                '<span>Size :</span>',
                '<strong>' + htmlEncode(buildReceiptSelectedSizeText(editableMembers)) + '</strong>',
                '</div>',
                '<fieldset class="apts-discrepancy-popup-actions">',
                '<legend>Select Action -</legend>',
                '<label><input type="radio" name="ReceiptDiscrepancyAction" value="Loss"' + (checkedAction === "Loss" ? " checked" : "") + ' /><span>Loss</span></label>',
                '<label><input type="radio" name="ReceiptDiscrepancyAction" value="Damage"' + (checkedAction === "Damage" ? " checked" : "") + ' /><span>Damage</span></label>',
                '</fieldset>',
                '<label class="apts-discrepancy-popup-comment">',
                '<span>Comment</span>',
                '<textarea rows="2" maxlength="250" data-receipt-discrepancy-comment>' + htmlEncode(firstComment) + '</textarea>',
                '</label>',
                '<div class="apts-discrepancy-popup-buttons">',
                '<button type="button" data-receipt-discrepancy-back>BACK</button>',
                '<button type="button" data-receipt-discrepancy-submit>SUBMIT</button>',
                '</div>',
                '</div>'
            ].join("");
        }

        popup.hidden = false;
        document.body.classList.add("apts-process-popup-open");
    }

    function submitReceiptDiscrepancyPopup() {
        var popup = bySelector("[data-receipt-discrepancy-popup]");
        var selectedAction = popup ? popup.querySelector("input[name='ReceiptDiscrepancyAction']:checked") : null;
        var comment = popup ? popup.querySelector("[data-receipt-discrepancy-comment]") : null;
        var selectedMembers = getEditableReceiptDiscrepancyMembers();

        if (!selectedAction) {
            showReceiptPopup("Please select discrepancy action.");
            return;
        }

        selectedMembers.forEach(function (member) {
            member.actionType = selectedAction.value;
            member.comment = comment ? comment.value.trim() : "";
        });

        closeReceiptDiscrepancyPopup();
        if (activeReceiptGroupIndex !== null) {
            openReceiptGroupPopup(activeReceiptGroupIndex);
        } else {
            updateReceiptSelectAllState();
        }
        setReceiptSaveStatus("valid", "Discrepancy details updated.");
    }

    function closeReceiptGroupPopup() {
        var modal = bySelector("[data-receipt-group-modal]");
        if (modal) {
            modal.hidden = true;
        }
        activeReceiptGroupIndex = null;
    }

    function openReceiptGroupPopup(groupIndex) {
        var modal = bySelector("[data-receipt-group-modal]");
        var sizeNode = bySelector("[data-receipt-group-size]");
        var countNode = bySelector("[data-receipt-group-count]");
        var body = bySelector("[data-receipt-group-body]");
        var closeButton = bySelector("[data-receipt-group-close]");
        var group = getReceiptGroups()[groupIndex];

        if (!modal || !body || !group) {
            return;
        }

        activeReceiptGroupIndex = groupIndex;

        if (sizeNode) {
            sizeNode.textContent = "Members Size: " + (group.size || "-");
        }
        if (countNode) {
            countNode.textContent = "Count: " + group.members.length;
        }

        body.innerHTML = group.members.map(function (member) {
            var isDiscrepancy = isReceiptDiscrepancyMember(member);
            var discrepancyText = hasReceiptDiscrepancyAction(member) ? member.actionType : "Null";
            return [
                "<tr>",
                "<td>" + htmlEncode(member.memberType || group.type || "-") + "</td>",
                "<td>" + htmlEncode(String(member.rfid || "-").toUpperCase()) + "</td>",
                '<td class="apts-sku-select"><input type="checkbox" data-receipt-discrepancy-member value="' + htmlEncode(member.rfid || "") + '" aria-label="Mark discrepancy"' + (isDiscrepancy ? ' checked' : '') + ' /></td>',
                '<td data-receipt-discrepancy-cell>' + htmlEncode(discrepancyText) + "</td>",
                "</tr>"
            ].join("");
        }).join("");
        updateReceiptSelectAllState();

        modal.hidden = false;
        if (closeButton && typeof closeButton.focus === "function") {
            closeButton.focus();
        }
    }
    function normalizeReceiptMember(item) {
        item = item || {};

        return {
            rfid: item.Rfid || item.rfid || "",
            memberCode: item.MemberCode || item.memberCode || "",
            memberType: item.MemberType || item.memberType || "",
            memberSize: item.MemberSize || item.memberSize || "",
            matDispCode: item.MatDispCode || item.matDispCode || "",
            truckNo: item.TruckNo || item.truckNo || "",
            actionType: item.ActionType || item.actionType || "Y",
            comment: item.Comment || item.comment || ""
        };
    }

    function mergeReceiptMembers(items) {
        (items || []).forEach(function (item) {
            var member = normalizeReceiptMember(item);
            var key = String(member.rfid || "").toUpperCase();

            if (!key) {
                return;
            }

            var alreadyAdded = members.some(function (existing) {
                return String(existing.rfid || "").toUpperCase() === key;
            });

            if (!alreadyAdded) {
                members.push(member);
            }
        });
    }

    function getCheckedReceiptMembers() {
        return members.slice();
    }

    function buildReceiptSaveFormData(form, checkedMembers) {
        var data = new FormData();
        var receiptFrom = form.querySelector("input[name='ReceiptFrom']");
        var receiptLocation = form.querySelector("input[name='ReceiptLocation']");
        var receiptLocationName = form.querySelector("input[name='ReceiptLocationName']");

        data.append("ReceiptFrom", receiptFrom ? receiptFrom.value : "");
        data.append("ReceiptLocation", receiptLocation ? receiptLocation.value : "");
        data.append("ReceiptLocationName", receiptLocationName ? receiptLocationName.value : "");

        checkedMembers.forEach(function (member, index) {
            data.append("Members[" + index + "].Rfid", member.rfid || "");
            data.append("Members[" + index + "].MemberCode", member.memberCode || "");
            data.append("Members[" + index + "].MemberType", member.memberType || "");
            data.append("Members[" + index + "].MemberSize", member.memberSize || "");
            data.append("Members[" + index + "].MatDispCode", member.matDispCode || "");
            data.append("Members[" + index + "].TruckNo", member.truckNo || "");
            data.append("Members[" + index + "].ActionType", member.actionType || "Y");
            data.append("Members[" + index + "].Comment", member.comment || "");
        });

        return data;
    }

    function resetReceiptScanForm() {
        members = [];
        renderMembers();
        setReceiptSaveStatus("", "");

        var input = bySelector("[data-receipt-member-input]");
        if (input) {
            input.value = "";
        }
    }

    async function saveReceipt(form) {
        var endpoint = form.getAttribute("data-receipt-save-url") || "";
        var checkedMembers = getCheckedReceiptMembers();
        var submitButton = form.querySelector("[type='submit']");

        if (!checkedMembers.length) {
            showReceiptPopup("Please select member.");
            return;
        }

        if (!endpoint || !window.fetch) {
            showReceiptPopup("Unable to save Member Receipt.");
            return;
        }

        setReceiptSaveStatus("loading", "");
        if (submitButton) {
            submitButton.disabled = true;
        }

        try {
            var response = await fetch(endpoint, {
                method: "POST",
                credentials: "same-origin",
                body: buildReceiptSaveFormData(form, checkedMembers),
                headers: { "Accept": "application/json" }
            });
            var result = response.ok ? await response.json() : null;

            setReceiptSaveStatus("", "");

            if (result && result.success) {
                if (window.AptsProcessPopup) {
                    window.AptsProcessPopup.show(result.message || "Member Receipt Saved Successfully", function () {
                        window.location.href = "/MemberReceipt/MemberReceipt";
                    });
                } else {
                    alert(result.message || "Member Receipt Saved Successfully");
                    window.location.href = "/MemberReceipt/MemberReceipt";
                }
                return;
            }

            showReceiptPopup(result && result.message ? result.message : "Unable to save Member Receipt.");
        } catch (error) {
            setReceiptSaveStatus("", "");
            showReceiptPopup("Unable to save Member Receipt.");
        } finally {
            if (submitButton) {
                submitButton.disabled = false;
            }
        }
    }

    async function addMember() {
        var input = bySelector("[data-receipt-member-input]");
        var value = input ? input.value.trim() : "";
        var endpoint = getFormUrl("data-receipt-validation-url");
        var addButton = bySelector("[data-receipt-add]");

        if (!value) {
            if (input) {
                input.focus();
            }
            return;
        }

        if (!endpoint || !window.fetch) {
            showReceiptPopup("Unable to validate member.");
            return;
        }

        setReceiptSaveStatus("loading", "");
        if (addButton) {
            addButton.disabled = true;
        }

        try {
            var body = new URLSearchParams();
            body.append("tagRfid", value);

            var response = await fetch(endpoint, {
                method: "POST",
                credentials: "same-origin",
                headers: {
                    "Content-Type": "application/x-www-form-urlencoded; charset=UTF-8"
                },
                body: body.toString()
            });
            var result = await response.json();

            if (!response.ok || !result || !result.success) {
                setReceiptSaveStatus("", "");
                showReceiptPopup(result && result.message ? result.message : "Unable to validate member.");
                return;
            }

            mergeReceiptMembers(result.members || []);
        } catch (error) {
            setReceiptSaveStatus("", "");
            showReceiptPopup("Unable to validate member.");
            return;
        } finally {
            if (addButton) {
                addButton.disabled = false;
            }
        }

        setReceiptSaveStatus("", "");
        if (input) {
            input.value = "";
            input.focus();
        }

        renderMembers();
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

    function closeAllCustomSelects(exceptSelect) {
        document.querySelectorAll("[data-custom-select]").forEach(function (select) {
            if (select !== exceptSelect) {
                closeCustomSelect(select);
            }
        });
    }

    function toggleCustomSelect(button) {
        var select = button.closest("[data-custom-select]");
        var menu = select ? select.querySelector("[data-custom-select-menu]") : null;

        if (!select || !menu) {
            return;
        }

        var shouldOpen = menu.hidden;
        closeAllCustomSelects(select);
        menu.hidden = !shouldOpen;
        button.setAttribute("aria-expanded", shouldOpen ? "true" : "false");
        select.classList.toggle("is-open", shouldOpen);
    }

    function chooseCustomSelectOption(option) {
        if (option.hasAttribute("data-receipt-empty")) {
            return;
        }

        var select = option.closest("[data-custom-select]");
        var field = option.closest(".apts-custom-select-field");
        var valueInput = field ? field.querySelector("[data-custom-select-value]") : null;
        var nameInput = field ? field.querySelector("[data-receipt-location-name]") : null;
        var label = select ? select.querySelector("[data-custom-select-label]") : null;
        var value = option.getAttribute("data-value") || option.textContent.trim();
        var text = option.textContent.trim();

        if (valueInput) {
            valueInput.value = value;
            valueInput.dispatchEvent(new Event("change", { bubbles: true }));
        }
        if (nameInput) {
            nameInput.value = text;
        }
        if (label) {
            label.textContent = text;
        }

        if (select) {
            select.querySelectorAll("[role='option']").forEach(function (item) {
                item.setAttribute("aria-selected", item === option ? "true" : "false");
            });
            closeCustomSelect(select);
        }
    }

    document.addEventListener("click", function (event) {
        var selectButton = event.target.closest("[data-custom-select-button]");
        if (selectButton) {
            toggleCustomSelect(selectButton);
            return;
        }

        var selectOption = event.target.closest("[data-custom-select-menu] [role='option']");
        if (selectOption) {
            chooseCustomSelectOption(selectOption);
            return;
        }

        var groupRow = event.target.closest("[data-receipt-group-index]");
        if (groupRow) {
            openReceiptGroupPopup(parseInt(groupRow.getAttribute("data-receipt-group-index"), 10));
            return;
        }

        if (event.target.closest("[data-receipt-group-close]")) {
            closeReceiptGroupPopup();
            return;
        }

        if (event.target.closest("[data-receipt-add]")) {
            addMember();
            return;
        }

        if (event.target.closest("[data-receipt-clear]")) {
            members = [];
            renderMembers();
            var input = bySelector("[data-receipt-member-input]");
            if (input) {
                input.value = "";
                input.focus();
            }
            return;
        }

        if (event.target.closest("[data-receipt-discrepancy]")) {
            showReceiptDiscrepancyPopup();
            return;
        }

        if (event.target.closest("[data-receipt-discrepancy-close]") || event.target.closest("[data-receipt-discrepancy-back]") || event.target.matches("[data-receipt-discrepancy-popup]")) {
            closeReceiptDiscrepancyPopup();
            return;
        }

        if (event.target.closest("[data-receipt-discrepancy-submit]")) {
            submitReceiptDiscrepancyPopup();
            return;
        }

        if (!event.target.closest("[data-custom-select]")) {
            closeAllCustomSelects();
        }
    });

    document.addEventListener("change", function (event) {
        if (event.target.matches("input[name='receiptFrom']")) {
            refreshReceiptLocationSelect();
        }

        if (event.target.matches("[data-receipt-select-all]")) {
            document.querySelectorAll("[data-receipt-list] input[name='CheckedMembers']").forEach(function (checkbox) {
                checkbox.checked = event.target.checked;
            });
            updateReceiptSelectAllState();
        }

        if (event.target.matches("[data-receipt-list] input[name='CheckedMembers']")) {
            updateReceiptSelectAllState();
        }

        if (event.target.matches("[data-receipt-discrepancy-select-all]")) {
            document.querySelectorAll("[data-receipt-discrepancy-member]").forEach(function (checkbox) {
                checkbox.checked = event.target.checked;
                checkbox.dispatchEvent(new Event("change", { bubbles: true }));
            });
            updateReceiptSelectAllState();
        }

        if (event.target.matches("[data-receipt-discrepancy-member]")) {
            var member = findReceiptMemberByRfid(event.target.value);
            var row = event.target.closest("tr");
            var cell = row ? row.querySelector("[data-receipt-discrepancy-cell]") : null;
            if (member && event.target.checked) {
                member.actionType = isReceiptDiscrepancyMember(member) ? member.actionType : "Discrepancy";
            } else if (member) {
                member.actionType = "Y";
                member.comment = "";
            }
            if (cell) {
                cell.textContent = hasReceiptDiscrepancyAction(member) ? member.actionType : "Null";
            }
            updateReceiptSelectAllState();
        }
    });

    document.addEventListener("input", function (event) {
        if (event.target.matches("[data-receipt-member-input]")) {
            uppercaseInputValue(event.target);
        }
    });

    document.addEventListener("submit", function (event) {
        var saveForm = event.target && event.target.closest("[data-member-receipt-form]");
        if (saveForm) {
            event.preventDefault();
            saveReceipt(saveForm);
            return;
        }

        var form = event.target && event.target.closest("[data-member-receipt-setup-form]");
        var locationInput = bySelector("#ReceiptLocation");

        if (!form) {
            return;
        }

        if (!locationInput || !locationInput.value) {
            event.preventDefault();
            showReceiptPopup("Please select Location.");
        }
    });

    document.addEventListener("keydown", function (event) {
        if (event.key === "Enter" && event.target.matches("[data-receipt-member-input]")) {
            event.preventDefault();
            addMember();
        }

        if (event.key === "Escape") {
            closeAllCustomSelects();
        }
    });

    filterReceiptLocationOptions();
    renderMembers();
}());
