(function () {
    var members = [];
    var pendingBypassMember = null;

    function bySelector(selector) {
        return document.querySelector(selector);
    }
    function escapeHtml(value) {
        return String(value || "")
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#39;");
    }

    function showPopup(message) {
        if (window.AptsProcessPopup) {
            window.AptsProcessPopup.show(message || "Validation failed.");
            return;
        }

        alert(message || "Validation failed.");
    }

    function getSelectedTruckCode() {
        var truckCode = bySelector("[data-collection-truck-code]");
        return truckCode ? truckCode.value : "";
    }

    function getSelectedTruckNo() {
        var truckNo = bySelector("[data-collection-truck-no]");
        return truckNo ? truckNo.value : "";
    }

    function getTransportEntryMode() {
        var selected = document.querySelector("input[name='CollectionTransportEntryMode']:checked");
        return selected ? selected.value : "Master";
    }

    function isTransportMasterMode() {
        return getTransportEntryMode() === "Master";
    }

    function resetMasterTruckSelect() {
        var truckInput = bySelector("[data-collection-truck-code]");
        var field = truckInput ? truckInput.closest(".apts-custom-select-field") : null;
        var label = field ? field.querySelector("[data-custom-select-label]") : null;

        if (truckInput) {
            truckInput.value = "";
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

    function clearTruckDetails() {
        var truckCode = bySelector("[data-collection-truck-code]");
        var truckNo = bySelector("[data-collection-truck-no]");
        var driverName = bySelector("[data-collection-driver-name]");
        var driverContact = bySelector("[data-collection-driver-contact]");
        var transporter = bySelector("[data-collection-transporter]");

        if (truckCode) {
            truckCode.value = "";
        }
        if (truckNo) {
            truckNo.value = "";
        }
        if (driverName) {
            driverName.value = "";
        }
        if (driverContact) {
            driverContact.value = "";
        }
        if (transporter) {
            transporter.value = "";
        }
    }

    function syncManualTruckNo() {
        var manualTruck = bySelector("[data-collection-manual-truck-no]");
        var truckCode = bySelector("[data-collection-truck-code]");
        var truckNo = bySelector("[data-collection-truck-no]");
        var value = manualTruck ? manualTruck.value.trim().toUpperCase() : "";

        if (manualTruck) {
            manualTruck.value = value;
        }
        if (truckCode) {
            truckCode.value = value;
        }
        if (truckNo) {
            truckNo.value = value;
        }
    }

    function updateTransportEntryMode(clearCurrent) {
        var masterMode = isTransportMasterMode();
        var truckField = bySelector("[data-collection-transfer-truck-field]");
        var manualField = bySelector("[data-collection-transfer-manual-truck-field]");
        var manualTruck = bySelector("[data-collection-manual-truck-no]");

        if (truckField) {
            truckField.hidden = !masterMode;
        }
        if (manualField) {
            manualField.hidden = masterMode;
        }

        if (masterMode) {
            if (clearCurrent) {
                if (manualTruck) {
                    manualTruck.value = "";
                }
                clearTruckDetails();
            }
        } else {
            if (manualTruck && clearCurrent) {
                manualTruck.value = "";
            }
            resetMasterTruckSelect();
            clearTruckDetails();
            syncManualTruckNo();
            if (manualTruck && clearCurrent) {
                manualTruck.focus();
            }
        }
    }
    function setStatus(message, state) {
        var status = bySelector("[data-collection-transfer-status]");
        var input = bySelector("[data-collection-transfer-input]");

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

    function closeCustomSelect(select) {
        var menu = select ? select.querySelector("[data-custom-select-menu]") : null;
        var button = select ? select.querySelector("[data-custom-select-button]") : null;
        if (menu) {
            menu.hidden = true;
        }
        if (button) {
            button.setAttribute("aria-expanded", "false");
        }
        if (select) {
            select.classList.remove("is-open");
        }
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

    function getSelectedLocationType() {
        var selected = document.querySelector("input[name='toLocationType']:checked");
        return selected ? selected.value : "";
    }

    function resetToLocationSelect() {
        var valueInput = bySelector("#CollectionToLocation");
        var field = valueInput ? valueInput.closest(".apts-custom-select-field") : null;
        var label = field ? field.querySelector("[data-custom-select-label]") : null;

        if (valueInput) {
            valueInput.value = "";
        }
        if (label) {
            label.textContent = "Select To Location";
        }
        if (field) {
            field.querySelectorAll("[role='option']").forEach(function (item) {
                item.setAttribute("aria-selected", "false");
            });
        }
    }

    function filterToLocationOptions() {
        var field = bySelector("#CollectionToLocation");
        field = field ? field.closest(".apts-custom-select-field") : null;
        if (!field) {
            return;
        }

        var selectedType = getSelectedLocationType();
        var visibleCount = 0;
        var emptyOption = field.querySelector("[data-transfer-empty]");

        field.querySelectorAll("[data-transfer-location-option]").forEach(function (option) {
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

    function applyTruckDetails(option) {
        var truckCode = bySelector("[data-collection-truck-code]");
        var truckNo = bySelector("[data-collection-truck-no]");
        var driverName = bySelector("[data-collection-driver-name]");
        var driverContact = bySelector("[data-collection-driver-contact]");
        var transporter = bySelector("[data-collection-transporter]");

        if (truckCode) {
            truckCode.value = option.getAttribute("data-value") || "";
        }
        if (truckNo) {
            truckNo.value = option.getAttribute("data-truck-no") || option.textContent.trim();
        }
        if (driverName) {
            driverName.value = option.getAttribute("data-driver-name") || "";
        }
        if (driverContact) {
            driverContact.value = option.getAttribute("data-driver-contact") || "";
        }
        if (transporter) {
            transporter.value = option.getAttribute("data-transporter-name") || "";
        }
    }

    function chooseCustomSelectOption(option) {
        if (option.hasAttribute("data-transfer-empty") || option.disabled) {
            return;
        }

        var select = option.closest("[data-custom-select]");
        var field = option.closest(".apts-custom-select-field");
        var valueInput = field ? field.querySelector("[data-custom-select-value]") : null;
        var label = select ? select.querySelector("[data-custom-select-label]") : null;
        var value = option.getAttribute("data-value") || option.textContent.trim();
        var text = option.textContent.trim();

        if (valueInput) {
            valueInput.value = value;
            valueInput.dispatchEvent(new Event("change", { bubbles: true }));
        }
        if (label) {
            label.textContent = text;
        }
        if (valueInput && valueInput.id === "CollectionTruckNo") {
            applyTruckDetails(option);
        }
        if (select) {
            select.querySelectorAll("[role='option']").forEach(function (item) {
                item.setAttribute("aria-selected", item === option ? "true" : "false");
            });
            closeCustomSelect(select);
        }
    }

    function updateCheckedMemberCount() {
        var count = bySelector("[data-collection-transfer-count]");
        if (count) {
            count.textContent = members.length;
        }
    }

    function getMemberSize(member) {
        return member.memberSize || member.memberDimension || "-";
    }

    function getCollectionTransferGroups() {
        var groups = [];

        members.forEach(function (member) {
            var size = getMemberSize(member);
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

    function addReturnedMembers(result, tagValue) {
        var returnedMembers = Array.isArray(result.members) && result.members.length
            ? result.members
            : [result];
        var addedCount = 0;

        returnedMembers.forEach(function (item) {
            var itemRfid = item.rfid || item.Rfid || "";
            if (!itemRfid) {
                return;
            }

            if (members.some(function (member) { return String(member.rfid).toUpperCase() === String(itemRfid).toUpperCase(); })) {
                return;
            }

            members.push({
                tagRfid: item.tagRfid || item.TagRfid || tagValue,
                rfid: itemRfid,
                memberCode: item.memberCode || item.MemberCode || "",
                memberType: item.memberType || item.MemberType || "",
                memberSize: item.memberSize || item.MemberSize || "",
                memberWeight: item.memberWeight || item.MemberWeight || "",
                memberDimension: item.memberDimension || item.MemberDimension || ""
            });
            addedCount += 1;
        });

        return addedCount;
    }

    function renderMembers() {
        var list = bySelector("[data-collection-transfer-list]");
        if (!list) {
            return;
        }

        if (!members.length) {
            list.innerHTML = "<p>No members scanned yet.</p>";
            updateCheckedMemberCount();
            return;
        }

        var groups = getCollectionTransferGroups();
        var rows = groups.map(function (group, index) {
            return [
                '<tr class="apts-receipt-group-row" tabindex="0" role="button" data-collection-transfer-group-index="' + index + '">',
                "<td>" + escapeHtml(group.size || "-") + "</td>",
                "<td>" + escapeHtml(group.type || "-") + "</td>",
                "<td>" + escapeHtml(group.members.length) + "</td>",
                "</tr>"
            ].join("");
        }).join("");
        var hiddenInputs = members.map(function (member, index) {
            return [
                '<input type="hidden" name="CheckedMembers" value="' + escapeHtml(member.rfid) + '" />',
                '<input type="hidden" name="Members[' + index + '].Rfid" value="' + escapeHtml(member.rfid) + '" />',
                '<input type="hidden" name="Members[' + index + '].TagRfid" value="' + escapeHtml(member.tagRfid) + '" />',
                '<input type="hidden" name="Members[' + index + '].MemberCode" value="' + escapeHtml(member.memberCode) + '" />',
                '<input type="hidden" name="Members[' + index + '].MemberType" value="' + escapeHtml(member.memberType) + '" />',
                '<input type="hidden" name="Members[' + index + '].MemberSize" value="' + escapeHtml(member.memberSize) + '" />',
                '<input type="hidden" name="Members[' + index + '].MemberWeight" value="' + escapeHtml(member.memberWeight) + '" />',
                '<input type="hidden" name="Members[' + index + '].MemberDimension" value="' + escapeHtml(member.memberDimension) + '" />'
            ].join("");
        }).join("");

        list.innerHTML = [
            '<div class="apts-sku-table-wrap apts-transfer-member-table-wrap">',
            '<table class="apts-sku-table apts-transfer-member-table apts-receipt-summary-table">',
            "<thead><tr><th>Size</th><th>Type</th><th>Count</th></tr></thead>",
            "<tbody>",
            rows,
            "</tbody>",
            "</table>",
            "</div>",
            '<div hidden data-collection-transfer-hidden-inputs>',
            hiddenInputs,
            "</div>"
        ].join("");

        updateCheckedMemberCount();
    }

    function closeCollectionTransferGroupPopup() {
        var modal = bySelector("[data-collection-transfer-group-modal]");
        if (modal) {
            modal.hidden = true;
        }
    }

    function openCollectionTransferGroupPopup(groupIndex) {
        var modal = bySelector("[data-collection-transfer-group-modal]");
        var sizeNode = bySelector("[data-collection-transfer-group-size]");
        var countNode = bySelector("[data-collection-transfer-group-count]");
        var body = bySelector("[data-collection-transfer-group-body]");
        var closeButton = bySelector("[data-collection-transfer-group-close]");
        var group = getCollectionTransferGroups()[groupIndex];

        if (!modal || !body || !group) {
            return;
        }

        if (sizeNode) {
            sizeNode.textContent = "Members Size: " + (group.size || "-");
        }
        if (countNode) {
            countNode.textContent = "Count: " + group.members.length;
        }

        body.innerHTML = group.members.map(function (member, index) {
            return [
                "<tr>",
                "<td>" + escapeHtml(index + 1) + "</td>",
                "<td>" + escapeHtml(member.memberType || group.type || "-") + "</td>",
                "<td>" + escapeHtml(String(member.rfid || "-").toUpperCase()) + "</td>",
                "</tr>"
            ].join("");
        }).join("");

        modal.hidden = false;
        if (closeButton && typeof closeButton.focus === "function") {
            closeButton.focus();
        }
    }
    async function validateCollectionMember(tagRfid) {
        var form = bySelector("[data-collection-transfer-form]");
        var endpoint = form ? form.getAttribute("data-collection-transfer-validation-url") : "";
        var body = new URLSearchParams();
        body.append("tagRfid", tagRfid);
        body.append("selectedTruckCode", getSelectedTruckCode());
        body.append("selectedTruckNo", getSelectedTruckNo());

        if (!endpoint || !window.fetch) {
            return { success: false, message: "Unable to validate member." };
        }

        try {
            var response = await fetch(endpoint, {
                method: "POST",
                credentials: "same-origin",
                headers: {
                    "Accept": "application/json",
                    "Content-Type": "application/x-www-form-urlencoded; charset=UTF-8"
                },
                body: body.toString()
            });

            return response.ok ? await response.json() : { success: false, message: "Unable to validate member." };
        } catch (error) {
            return { success: false, message: "Unable to validate member." };
        }
    }

    async function continueCollectionBypass() {
        var form = bySelector("[data-collection-transfer-form]");
        var endpoint = form ? form.getAttribute("data-collection-transfer-bypass-url") : "";
        var member = pendingBypassMember;

        if (!endpoint || !member || !window.fetch) {
            closeBypassModal();
            showPopup("Member bypass service is not available.");
            return;
        }

        var body = new URLSearchParams();
        body.append("rfid", member.rfid || "");
        body.append("selectedTruckCode", getSelectedTruckCode());
        body.append("selectedTruckNo", getSelectedTruckNo());

        setStatus("Updating member bypass...", "loading");

        try {
            var response = await fetch(endpoint, {
                method: "POST",
                credentials: "same-origin",
                headers: {
                    "Accept": "application/json",
                    "Content-Type": "application/x-www-form-urlencoded; charset=UTF-8"
                },
                body: body.toString()
            });
            var result = response.ok ? await response.json() : null;

            closeBypassModal();
            if (!result || !result.success) {
                setStatus(result && result.message ? result.message : "Unable to continue member bypass.", "invalid");
                showPopup(result && result.message ? result.message : "Unable to continue member bypass.");
                return;
            }

            var addedCount = addReturnedMembers(result, member.tagRfid || member.rfid);
            if (!addedCount) {
                setStatus("Member already added.", "invalid");
                showPopup("Member already added.");
                return;
            }

            var input = bySelector("[data-collection-transfer-input]");
            if (input) {
                input.value = "";
                input.focus();
            }
            setStatus("Member bypass completed and added to the table.", "valid");
            renderMembers();
        } catch (error) {
            closeBypassModal();
            setStatus("Unable to continue member bypass.", "invalid");
            showPopup("Unable to continue member bypass.");
        }
    }

    function openBypassModal(result, tagValue) {
        pendingBypassMember = {
            rfid: result.rfid || "",
            tagRfid: tagValue || result.rfid || ""
        };

        var message = bySelector("[data-collection-transfer-bypass-message]");
        var modal = bySelector("[data-collection-transfer-bypass]");
        if (message) {
            message.textContent = result.message || "The member is not in Member Collection status. Do you want to continue?";
        }
        if (modal) {
            modal.hidden = false;
        }
    }

    function closeBypassModal() {
        pendingBypassMember = null;
        var modal = bySelector("[data-collection-transfer-bypass]");
        if (modal) {
            modal.hidden = true;
        }
    }

    async function addMember() {
        var input = bySelector("[data-collection-transfer-input]");
        var value = input ? input.value.trim().toUpperCase() : "";
        var button = bySelector("[data-collection-transfer-add]");

        if (!value) {
            showPopup("Please scan member.");
            if (input) {
                input.focus();
            }
            return;
        }

        if (!isTransportMasterMode()) {
            syncManualTruckNo();
        }

        if (!getSelectedTruckCode() && !getSelectedTruckNo()) {
            showPopup(isTransportMasterMode() ? "Please select truck." : "Please enter Truck Number.");
            return;
        }

        setStatus("Validating member...", "loading");
        if (button) {
            button.disabled = true;
        }

        var result = await validateCollectionMember(value);

        if (button) {
            button.disabled = false;
        }

        if (result && result.bypassRequired && result.rfid) {
            setStatus(result.message || "Member bypass required.", "invalid");
            openBypassModal(result, value);
            return;
        }

        if (!result || !result.success || !result.rfid) {
            setStatus(result && result.message ? result.message : "Member status should be Member Collection.", "invalid");
            showPopup(result && result.message ? result.message : "Member status should be Member Collection.");
            return;
        }

        var addedCount = addReturnedMembers(result, value);

        if (!addedCount) {
            setStatus("Member already added.", "invalid");
            showPopup("Member already added.");
            return;
        }

        if (input) {
            input.value = "";
            input.focus();
        }
        setStatus(addedCount + " member(s) loaded successfully.", "valid");
        renderMembers();
    }

    function prepareCheckedMembersForSubmit() {
        document.querySelectorAll("[data-collection-transfer-hidden-inputs] input").forEach(function (input) {
            input.disabled = false;
        });
    }

    function validateSubmit() {
        var toLocation = bySelector("#CollectionToLocation");
        var checkedRows = members;

        if (!isTransportMasterMode()) {
            syncManualTruckNo();
        }

        if (!getSelectedTruckCode() && !getSelectedTruckNo()) {
            showPopup(isTransportMasterMode() ? "Please select truck." : "Please enter Truck Number.");
            return false;
        }
        if (!toLocation || !toLocation.value) {
            showPopup("Please select To location.");
            return false;
        }
        if (!checkedRows.length) {
            showPopup("Please select member.");
            return false;
        }

        prepareCheckedMembersForSubmit();
        return true;
    }

    document.addEventListener("click", function (event) {
        var selectButton = event.target.closest("[data-custom-select-button]");
        if (selectButton) {
            toggleCustomSelect(selectButton);
            return;
        }

        var option = event.target.closest("[data-custom-select-menu] [role='option']");
        if (option) {
            chooseCustomSelectOption(option);
            return;
        }

        var groupRow = event.target.closest("[data-collection-transfer-group-index]");
        if (groupRow) {
            openCollectionTransferGroupPopup(parseInt(groupRow.getAttribute("data-collection-transfer-group-index"), 10));
            return;
        }

        if (event.target.closest("[data-collection-transfer-group-close]")) {
            closeCollectionTransferGroupPopup();
            return;
        }

        if (event.target.closest("[data-collection-transfer-add]")) {
            addMember();
            return;
        }

        if (event.target.closest("[data-collection-transfer-clear]")) {
            members = [];
            closeBypassModal();
            renderMembers();
            setStatus("", "");
            var input = bySelector("[data-collection-transfer-input]");
            if (input) {
                input.value = "";
            }
            resetToLocationSelect();
            filterToLocationOptions();
            updateTransportEntryMode(true);
            return;
        }

        if (event.target.closest("[data-collection-transfer-bypass-cancel]")) {
            closeBypassModal();
            return;
        }

        if (event.target.closest("[data-collection-transfer-bypass-continue]")) {
            continueCollectionBypass();
            return;
        }

        if (!event.target.closest("[data-custom-select]")) {
            closeAllCustomSelects();
        }
    });

    document.addEventListener("change", function (event) {
        if (event.target.matches("input[name='toLocationType']")) {
            resetToLocationSelect();
            filterToLocationOptions();
        }
        if (event.target.matches("input[name='CollectionTransportEntryMode']")) {
            updateTransportEntryMode(true);
        }
        if (event.target.matches("[data-collection-transfer-list] input[name='CheckedMembers']")) {
            updateCheckedMemberCount();
        }
    });

    document.addEventListener("input", function (event) {
        if (event.target.matches("[data-collection-transfer-input]")) {
            event.target.value = event.target.value.toUpperCase();
            setStatus("", "");
        }
        if (event.target.matches("[data-collection-manual-truck-no]")) {
            syncManualTruckNo();
            setStatus("", "");
        }
    });

    document.addEventListener("keydown", function (event) {
        if (event.key === "Enter" && event.target.matches("[data-collection-transfer-input]")) {
            event.preventDefault();
            addMember();
        }
        if (event.key === "Escape") {
            closeAllCustomSelects();
        }
    });

    document.addEventListener("submit", function (event) {
        var form = event.target && event.target.closest("[data-collection-transfer-form]");
        if (!form) {
            return;
        }

        if (!validateSubmit()) {
            event.preventDefault();
        }
    });

    filterToLocationOptions();
    updateTransportEntryMode(false);
    renderMembers();
}());
