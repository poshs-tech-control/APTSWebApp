(function () {
    var members = [];
    var transferDraftKey = "apts.internal-transfer.draft.v1";
    var validationInProgress = false;

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

    function setTransferMemberStatus(message, state) {
        var status = bySelector("[data-transfer-member-status]");
        var input = bySelector("[data-transfer-member-input]");

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

    function showValidationMessage(message) {
        setTransferMemberStatus("", "invalid");

        if (window.AptsProcessPopup) {
            window.AptsProcessPopup.show(message || "Validation failed.");
            return;
        }

        setTransferMemberStatus(message || "Validation failed.", "invalid");
    }

    function getFormUrl(attributeName) {
        var form = bySelector("[data-internal-transfer-form]");
        return form ? form.getAttribute(attributeName) : "";
    }

    function uppercaseInputValue(input) {
        if (input && input.value) {
            input.value = input.value.toUpperCase();
        }
    }

    function getSelectedFromLocationTitle() {
        var titleInput = bySelector("#UserLocationTitle");
        if (titleInput && titleInput.value) {
            return titleInput.value.trim();
        }

        var valueInput = bySelector("#UserLocation");
        var field = valueInput ? valueInput.closest(".apts-custom-select-field") : null;
        var label = field ? field.querySelector("[data-custom-select-label]") : null;

        if (!valueInput || !valueInput.value || !label) {
            return "";
        }

        return label.textContent.trim();
    }

    function getSelectedTransferLocation(inputSelector) {
        var valueInput = bySelector(inputSelector);
        var field = valueInput ? valueInput.closest(".apts-custom-select-field") : null;
        var label = field ? field.querySelector("[data-custom-select-label]") : null;

        return {
            value: valueInput ? valueInput.value : "",
            title: label ? label.textContent.trim() : ""
        };
    }

    function getSelectedLocationType(radioName) {
        var selected = document.querySelector("input[name='" + radioName + "']:checked");
        if (selected) {
            return selected.value;
        }

        var hidden = document.querySelector("input[type='hidden'][name='" + radioName + "']");
        return hidden ? hidden.value : "";
    }

    function getSelectDefaultLabel(field) {
        var valueInput = field ? field.querySelector("[data-custom-select-value]") : null;
        if (valueInput && valueInput.id === "ToLocation") {
            return "Select To Location";
        }

        return "Select Location";
    }

    function getSelectRadioName(field) {
        var valueInput = field ? field.querySelector("[data-custom-select-value]") : null;
        if (valueInput && valueInput.id === "ToLocation") {
            return "toLocationType";
        }

        return "userLocationType";
    }

    function resetCustomSelectField(field) {
        var valueInput = field ? field.querySelector("[data-custom-select-value]") : null;
        var label = field ? field.querySelector("[data-custom-select-label]") : null;

        if (valueInput) {
            valueInput.value = "";
        }
        if (label) {
            label.textContent = getSelectDefaultLabel(field);
        }
        if (field) {
            field.querySelectorAll("[role='option']").forEach(function (item) {
                item.setAttribute("aria-selected", "false");
            });
        }
    }

    function filterCustomSelectField(field) {
        var radioName = getSelectRadioName(field);
        var selectedType = getSelectedLocationType(radioName);
        var visibleCount = 0;
        var emptyOption = field ? field.querySelector("[data-transfer-empty]") : null;

        if (!field) {
            return;
        }

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

    function refreshLocationSelects(radioName) {
        document.querySelectorAll(".apts-custom-select-field").forEach(function (field) {
            if (!radioName || getSelectRadioName(field) === radioName) {
                resetCustomSelectField(field);
                filterCustomSelectField(field);
            }
        });
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
        if (option.hasAttribute("data-transfer-empty")) {
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
        if (select) {
            select.querySelectorAll("[role='option']").forEach(function (item) {
                item.setAttribute("aria-selected", item === option ? "true" : "false");
            });
            closeCustomSelect(select);
        }
    }

    function clearTransferDraft() {
        try {
            window.sessionStorage.removeItem(transferDraftKey);
        } catch (error) {
        }
    }

    function saveTransferDraft() {
        var checkedRfids = {};
        document.querySelectorAll("[data-transfer-list] input[name='CheckedMembers']:checked").forEach(function (checkbox) {
            checkedRfids[String(checkbox.value || "").toUpperCase()] = true;
        });

        var draft = {
            userLocationType: getSelectedLocationType("userLocationType"),
            userLocation: getSelectedTransferLocation("#UserLocation"),
            toLocationType: getSelectedLocationType("toLocationType"),
            toLocation: getSelectedTransferLocation("#ToLocation"),
            members: members.filter(function (member) {
                return checkedRfids[String(member.rfid || "").toUpperCase()];
            })
        };

        try {
            window.sessionStorage.setItem(transferDraftKey, JSON.stringify(draft));
        } catch (error) {
        }
    }

    function restoreCustomSelectValue(selector, selection) {
        var input = bySelector(selector);
        var field = input ? input.closest(".apts-custom-select-field") : null;
        var label = field ? field.querySelector("[data-custom-select-label]") : null;
        var selectedOption = null;

        if (!input || !field || !selection || !selection.value) {
            return;
        }

        field.querySelectorAll("[role='option']").forEach(function (option) {
            var isSelected = (option.getAttribute("data-value") || "") === selection.value;
            option.setAttribute("aria-selected", isSelected ? "true" : "false");
            if (isSelected) {
                selectedOption = option;
            }
        });

        input.value = selection.value;
        if (label) {
            label.textContent = selectedOption ? selectedOption.textContent.trim() : (selection.title || "");
        }
    }

    function restoreTransferDraft() {
        var shouldRestore = false;
        var draft;

        try {
            shouldRestore = String(new URLSearchParams(window.location.search).get("restoreDraft") || "").toLowerCase() === "true";
            draft = shouldRestore ? JSON.parse(window.sessionStorage.getItem(transferDraftKey) || "null") : null;
        } catch (error) {
            draft = null;
        }

        if (!draft) {
            refreshLocationSelects();
            return;
        }

        ["userLocationType", "toLocationType"].forEach(function (radioName) {
            var value = draft[radioName];
            document.querySelectorAll("input[name='" + radioName + "']").forEach(function (radio) {
                radio.checked = radio.value === value;
            });
        });

        refreshLocationSelects();
        restoreCustomSelectValue("#UserLocation", draft.userLocation);
        restoreCustomSelectValue("#ToLocation", draft.toLocation);
        members = Array.isArray(draft.members) ? draft.members : [];
    }
    function getTransferGroups() {
        var groups = [];
        members.forEach(function (member, memberIndex) {
            var size = member.memberSize || member.memberDimension || "-";
            var type = member.memberType || "-";
            var key = String(size).toUpperCase() + "|" + String(type).toUpperCase();
            var group = groups.filter(function (item) { return item.key === key; })[0];
            if (!group) { group = { key: key, size: size, type: type, members: [] }; groups.push(group); }
            group.members.push({ member: member, index: memberIndex });
        });
        return groups;
    }

    function renderMembers() {
        var list = bySelector("[data-transfer-list]");
        var groups = getTransferGroups();
        if (!list) { return; }
        var rows = groups.length ? groups.map(function (group, groupIndex) {
            var inputs = group.members.map(function (item) {
                var member = item.member, index = item.index;
                return '<input type="checkbox" name="CheckedMembers" value="' + escapeHtml(member.rfid) + '" checked hidden />' +
                    '<input type="hidden" name="Members[' + index + '].Rfid" value="' + escapeHtml(member.rfid) + '" />' +
                    '<input type="hidden" name="Members[' + index + '].TagRfid" value="' + escapeHtml(member.tagRfid) + '" />' +
                    '<input type="hidden" name="Members[' + index + '].MemberCode" value="' + escapeHtml(member.memberCode) + '" />' +
                    '<input type="hidden" name="Members[' + index + '].MemberType" value="' + escapeHtml(member.memberType) + '" />' +
                    '<input type="hidden" name="Members[' + index + '].MemberSize" value="' + escapeHtml(member.memberSize) + '" />' +
                    '<input type="hidden" name="Members[' + index + '].MemberWeight" value="' + escapeHtml(member.memberWeight) + '" />' +
                    '<input type="hidden" name="Members[' + index + '].MemberDimension" value="' + escapeHtml(member.memberDimension) + '" />';
            }).join("");
            return '<tr class="apts-receipt-row apts-receipt-group-row" tabindex="0" role="button" data-transfer-group-index="' + groupIndex + '"><td>' + escapeHtml(group.size) + inputs + '</td><td>' + escapeHtml(group.type) + '</td><td>' + group.members.length + '</td></tr>';
        }).join("") : '<tr><td class="apts-receipt-empty-cell" colspan="3">No members checked yet.</td></tr>';
        list.innerHTML = '<div class="apts-sku-table-wrap apts-receipt-table-wrap"><table class="apts-sku-table apts-receipt-table apts-receipt-summary-table"><thead><tr><th>Size</th><th>Type</th><th>Count</th></tr></thead><tbody>' + rows + '</tbody></table></div>';
        updateCheckedMemberCount();
    }
    function updateCheckedMemberCount() {
        var count = bySelector("[data-transfer-count]");
        var checkedRows = document.querySelectorAll("[data-transfer-list] input[name='CheckedMembers']:checked");

        if (count) {
            count.textContent = checkedRows.length;
        }
    }

    function showGroupDetails(groupIndex) {
        var group = getTransferGroups()[groupIndex];
        var popup = bySelector("[data-transfer-member-popup]");
        var details = bySelector("[data-transfer-member-popup-details]");
        var closeButton = bySelector("[data-transfer-member-popup-close]");
        var sizeNode = bySelector("[data-transfer-group-size]");
        var countNode = bySelector("[data-transfer-group-count]");
        if (!group || !popup || !details) { return; }
        if (sizeNode) { sizeNode.textContent = "Members Size: " + (group.size || "-"); }
        if (countNode) { countNode.textContent = "Count: " + group.members.length; }
        details.innerHTML = group.members.map(function (item, index) {
            return "<tr><td>" + escapeHtml(index + 1) + "</td><td>" +
                escapeHtml(item.member.memberType || group.type || "-") + "</td><td>" +
                escapeHtml(String(item.member.rfid || "-").toUpperCase()) + "</td></tr>";
        }).join("");
        popup.hidden = false;
        document.body.classList.add("apts-process-popup-open");
        if (closeButton) { closeButton.focus(); }
    }
    function findMemberByRfid(rfid) {
        var key = String(rfid || "").toUpperCase();
        return members.filter(function (member) {
            return String(member.rfid || "").toUpperCase() === key;
        })[0] || null;
    }

    function buildMemberDetailsMarkup(member) {
        var rows = [
            ["Member", String(member.rfid || "-").toUpperCase()],
            ["Member Code", member.memberCode || "-"],
            ["Type", member.memberType || "-"],
            ["Size", member.memberSize || "-"],
            ["Dimension", member.memberDimension || "-"],
            ["Weight", member.memberWeight || "-"],
            ["Qty", member.qty || "1"],
            ["Active", member.active || "-"]
        ];

        return rows.map(function (row) {
            return [
                '<div class="apts-transfer-detail-row">',
                "<span>" + escapeHtml(row[0]) + "</span>",
                "<strong>" + escapeHtml(row[1]) + "</strong>",
                "</div>"
            ].join("");
        }).join("");
    }

    function showMemberDetails(member) {
        var popup = bySelector("[data-transfer-member-popup]");
        var details = bySelector("[data-transfer-member-popup-details]");
        var closeButton = bySelector("[data-transfer-member-popup-close]");

        if (!popup || !details || !member) {
            return;
        }

        details.innerHTML = buildMemberDetailsMarkup(member);
        popup.hidden = false;
        document.body.classList.add("apts-process-popup-open");

        if (closeButton) {
            closeButton.focus();
        }
    }

    function closeMemberDetails() {
        var popup = bySelector("[data-transfer-member-popup]");

        if (!popup) {
            return;
        }

        popup.hidden = true;
        document.body.classList.remove("apts-process-popup-open");
    }

    function prepareCheckedMembersForSubmit() {
        document.querySelectorAll("[data-transfer-member-row]").forEach(function (row) {
            var checkbox = row.querySelector("input[name='CheckedMembers']");
            var isChecked = checkbox && checkbox.checked;

            row.querySelectorAll("input").forEach(function (input) {
                input.disabled = !isChecked;
            });
        });
    }

    function validateTransferFormSubmit() {
        var fromLocation = getSelectedTransferLocation("#UserLocation");
        var toLocation = getSelectedTransferLocation("#ToLocation");
        var checkedRows = document.querySelectorAll("[data-transfer-list] input[name='CheckedMembers']:checked");

        if (!fromLocation.value) {
            showValidationMessage("Please select From location.");
            return false;
        }

        if (!toLocation.value) {
            showValidationMessage("Please select To location.");
            return false;
        }

        if (fromLocation.value.toUpperCase() === toLocation.value.toUpperCase()) {
            showValidationMessage("From and To location cannot be same.");
            return false;
        }

        if (!checkedRows.length) {
            showValidationMessage("Please select member.");
            return false;
        }

        saveTransferDraft();
        prepareCheckedMembersForSubmit();
        return true;
    }

    async function validateTransferMember(tagRfid, currentLocation) {
        var endpoint = getFormUrl("data-transfer-member-validation-url");
        var body = new URLSearchParams();
        body.append("tagRfid", tagRfid);
        body.append("currentLocation", currentLocation);

        if (!endpoint || !window.fetch) {
            return {
                success: false,
                message: "Unable to validate member."
            };
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

            return response.ok ? await response.json() : {
                success: false,
                message: "Unable to validate member."
            };
        } catch (error) {
            return {
                success: false,
                message: "Unable to validate member."
            };
        }
    }

    async function addSummaryMember() {
        var input = bySelector("[data-transfer-member-input]");
        uppercaseInputValue(input);

        var value = input ? input.value.trim() : "";
        var currentLocation = getSelectedFromLocationTitle();
        var summaryButton = bySelector("[data-transfer-summary]");

        if (validationInProgress) {
            return;
        }

        if (!value) {
            showValidationMessage("Please scan member.");
            if (input) {
                input.focus();
            }
            return;
        }

        if (!currentLocation) {
            showValidationMessage("Please select location.");
            return;
        }

        validationInProgress = true;
        setTransferMemberStatus("", "loading");
        if (summaryButton) {
            summaryButton.disabled = true;
        }

        var result = await validateTransferMember(value, currentLocation);
        validationInProgress = false;

        if (summaryButton) {
            summaryButton.disabled = false;
        }

        if (!result || !result.success || !result.rfid) {
            showValidationMessage(result && result.message ? result.message : "Member status should be Manufacture or Received.");
            return;
        }

        if (members.some(function (member) { return member.rfid.toUpperCase() === result.rfid.toUpperCase(); })) {
            showValidationMessage("Member already added.");
            return;
        }

        members.push({
            tagRfid: value,
            rfid: result.rfid,
            memberCode: result.memberCode || "",
            memberType: result.memberType || "",
            memberSize: result.memberSize || "",
            memberWeight: result.memberWeight || "",
            memberDimension: result.memberDimension || "",
            active: result.active || "",
            qty: "1"
        });

        if (input) {
            input.value = "";
            input.focus();
        }
        setTransferMemberStatus(result.message || "Member validated successfully.", "valid");
        renderMembers();
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

        if (event.target.closest("[data-transfer-summary]")) {
            addSummaryMember();
            return;
        }

        if (event.target.closest("[data-transfer-member-popup-close]")) {
            closeMemberDetails();
            return;
        }

        if (event.target.matches("[data-transfer-member-popup]")) {
            closeMemberDetails();
            return;
        }

        var memberRow = event.target.closest("[data-transfer-group-index]");
        if (memberRow && !event.target.closest("input,button,label,a")) {
            showGroupDetails(parseInt(memberRow.getAttribute("data-transfer-group-index"), 10));
            return;
        }

        if (event.target.closest("[data-transfer-clear]")) {
            clearTransferDraft();
            members = [];
            renderMembers();
            refreshLocationSelects();
            setTransferMemberStatus("", "");
            var memberInput = bySelector("[data-transfer-member-input]");
            if (memberInput) {
                memberInput.value = "";
            }
            return;
        }

        if (!event.target.closest("[data-custom-select]")) {
            closeAllCustomSelects();
        }
    });

    document.addEventListener("keydown", function (event) {
        if (event.key === "Enter" && event.target.matches("[data-transfer-member-input]")) {
            event.preventDefault();
            addSummaryMember();
        }

        if ((event.key === "Enter" || event.key === " ") && event.target.matches("[data-transfer-group-index]")) {
            event.preventDefault();
            showGroupDetails(parseInt(event.target.getAttribute("data-transfer-group-index"), 10));
        }

        if (event.key === "Escape") {
            closeMemberDetails();
            closeAllCustomSelects();
        }
    });

    document.addEventListener("change", function (event) {
        if (event.target.matches("input[name='userLocationType']")) {
            refreshLocationSelects("userLocationType");
            setTransferMemberStatus("", "");
        }

        if (event.target.matches("input[name='toLocationType']")) {
            refreshLocationSelects("toLocationType");
        }

        if (event.target.matches("[data-transfer-member-input]")) {
            uppercaseInputValue(event.target);
            setTransferMemberStatus("", "");
        }

        if (event.target.matches("[data-transfer-list] input[name='CheckedMembers']")) {
            updateCheckedMemberCount();
        }
    });

    document.addEventListener("input", function (event) {
        if (event.target.matches("[data-transfer-member-input]")) {
            uppercaseInputValue(event.target);
        }
    });

    document.addEventListener("submit", function (event) {
        var form = event.target && event.target.closest("[data-internal-transfer-form]");

        if (!form) {
            return;
        }

        if (!validateTransferFormSubmit()) {
            event.preventDefault();
        }
    });

    restoreTransferDraft();
    renderMembers();
}());
