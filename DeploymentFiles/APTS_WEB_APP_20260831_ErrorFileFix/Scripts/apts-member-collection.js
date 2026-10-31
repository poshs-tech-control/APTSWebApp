(function () {
    var members = [];

    function bySelector(selector, root) {
        return (root || document).querySelector(selector);
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

    function setStatus(state, message) {
        var status = bySelector("[data-member-collection-status]");
        if (!status) {
            return;
        }
        status.textContent = message || "";
        status.classList.toggle("is-valid", state === "valid");
        status.classList.toggle("is-invalid", state === "invalid");
        status.classList.toggle("is-loading", state === "loading");
    }

    function closeSelect(select) {
        if (!select) {
            return;
        }
        var menu = bySelector("[data-custom-select-menu]", select);
        var button = bySelector("[data-custom-select-button]", select);
        if (menu) {
            menu.hidden = true;
        }
        if (button) {
            button.setAttribute("aria-expanded", "false");
        }
        select.classList.remove("is-open");
    }

    function closeAllSelects(except) {
        document.querySelectorAll("[data-custom-select]").forEach(function (select) {
            if (select !== except) {
                closeSelect(select);
            }
        });
    }

    function normalizeMember(item) {
        item = item || {};
        return {
            custDismentleCode: item.CustDismentleCode || item.custDismentleCode || "",
            custCode: item.CustCode || item.custCode || "",
            rfidNo: String(item.RFIDNo || item.rfidNo || "").toUpperCase(),
            memberType: item.MemberType || item.memberType || "",
            memberSize: item.MemberSize || item.memberSize || "",
            actionType: item.ActionType || item.actionType || ""
        };
    }

    function getMemberGroups() {
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

    function renderHiddenMemberFields() {
        var hiddenFields = bySelector("[data-member-collection-hidden-fields]");
        if (!hiddenFields) {
            return;
        }

        hiddenFields.innerHTML = members.map(function (member, index) {
            return [
                "<input type=\"hidden\" name=\"Members[" + index + "].RFIDNo\" value=\"" + escapeHtml(member.rfidNo) + "\" />",
                "<input type=\"hidden\" name=\"Members[" + index + "].CustDismentleCode\" value=\"" + escapeHtml(member.custDismentleCode) + "\" />",
                "<input type=\"hidden\" name=\"Members[" + index + "].CustCode\" value=\"" + escapeHtml(member.custCode) + "\" />",
                "<input type=\"hidden\" name=\"Members[" + index + "].MemberType\" value=\"" + escapeHtml(member.memberType) + "\" />",
                "<input type=\"hidden\" name=\"Members[" + index + "].MemberSize\" value=\"" + escapeHtml(member.memberSize) + "\" />",
                "<input type=\"hidden\" name=\"Members[" + index + "].ActionType\" value=\"" + escapeHtml(member.actionType) + "\" />"
            ].join("");
        }).join("");
    }

    function renderMembers() {
        var body = bySelector("[data-member-collection-list]");
        var count = bySelector("[data-member-collection-count]");
        var groups = getMemberGroups();

        if (count) {
            count.textContent = members.length;
        }
        renderHiddenMemberFields();

        if (!body) {
            return;
        }
        if (!groups.length) {
            body.innerHTML = '<tr><td class="apts-member-collection-empty" colspan="3">No members scanned yet.</td></tr>';
            return;
        }

        body.innerHTML = groups.map(function (group, index) {
            return [
                '<tr class="apts-receipt-row apts-receipt-group-row" tabindex="0" role="button" data-member-collection-group-index="' + index + '">',
                "<td>" + escapeHtml(group.size || "-") + "</td>",
                "<td>" + escapeHtml(group.type || "-") + "</td>",
                "<td>" + escapeHtml(group.members.length) + "</td>",
                "</tr>"
            ].join("");
        }).join("");
    }

    function closeMemberGroupPopup() {
        var modal = bySelector("[data-member-collection-group-modal]");
        if (modal) {
            modal.hidden = true;
        }
    }

    function openMemberGroupPopup(groupIndex) {
        var modal = bySelector("[data-member-collection-group-modal]");
        var sizeNode = bySelector("[data-member-collection-group-size]");
        var countNode = bySelector("[data-member-collection-group-count]");
        var body = bySelector("[data-member-collection-group-body]");
        var closeButton = bySelector("[data-member-collection-group-close]");
        var group = getMemberGroups()[groupIndex];

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
                "<td>" + escapeHtml(String(member.rfidNo || "-").toUpperCase()) + "</td>",
                "</tr>"
            ].join("");
        }).join("");

        modal.hidden = false;
        if (closeButton && typeof closeButton.focus === "function") {
            closeButton.focus();
        }
    }
    function mergeMembers(items) {
        var added = 0;
        (items || []).forEach(function (item) {
            var member = normalizeMember(item);
            if (!member.rfidNo) {
                return;
            }
            var exists = members.some(function (current) {
                return current.rfidNo === member.rfidNo;
            });
            if (!exists) {
                members.push(member);
                added += 1;
            }
        });
        renderMembers();
        return added;
    }

    async function validateAndAddMember() {
        var form = bySelector("[data-member-collection-form]");
        var input = bySelector("[data-member-collection-input]");
        var customer = bySelector("#CollectionCustomer");
        var addButton = bySelector("[data-member-collection-add]");
        var endpoint = form ? form.getAttribute("data-member-collection-validation-url") : "";
        var value = input ? input.value.trim().toUpperCase() : "";

        if (!customer || !customer.value) {
            showPopup("Please select Disassemble from.");
            return;
        }
        if (!value) {
            showPopup("Please scan or enter a member.");
            if (input) {
                input.focus();
            }
            return;
        }
        if (!endpoint || !window.fetch) {
            showPopup("Unable to validate member.");
            return;
        }

        input.value = value;
        setStatus("loading", "");
        if (addButton) {
            addButton.disabled = true;
        }

        try {
            var data = new URLSearchParams();
            data.append("tagRfid", value);
            data.append("customerCode", customer.value);
            var response = await fetch(endpoint, {
                method: "POST",
                credentials: "same-origin",
                headers: {
                    "Accept": "application/json",
                    "Content-Type": "application/x-www-form-urlencoded; charset=UTF-8"
                },
                body: data.toString()
            });
            var result = response.ok ? await response.json() : null;
            if (!result || !result.success) {
                setStatus("", "");
                showPopup(result && result.message ? result.message : "Unable to validate member.");
                return;
            }

            if (mergeMembers(result.members || []) === 0) {
                setStatus("", "");
                showPopup("Member already added.");
                return;
            }

            setStatus("valid", result.message || "Member validated successfully and added to the table.");
            input.value = "";
            input.focus();
        } catch (error) {
            setStatus("", "");
            showPopup("Unable to validate member.");
        } finally {
            if (addButton) {
                addButton.disabled = false;
            }
        }
    }

    function clearMembers() {
        members = [];
        renderMembers();
        setStatus("", "");
        var input = bySelector("[data-member-collection-input]");
        if (input) {
            input.value = "";
        }
    }

    document.addEventListener("click", function (event) {
        var selectButton = event.target.closest("[data-custom-select-button]");
        if (selectButton) {
            var select = selectButton.closest("[data-custom-select]");
            var menu = bySelector("[data-custom-select-menu]", select);
            var open = menu && menu.hidden;
            closeAllSelects(select);
            if (menu) {
                menu.hidden = !open;
            }
            selectButton.setAttribute("aria-expanded", open ? "true" : "false");
            select.classList.toggle("is-open", open);
            if (open) {
                var search = bySelector("[data-custom-select-search]", menu);
                if (search) {
                    search.value = "";
                    menu.querySelectorAll("[role='option']").forEach(function (option) { option.hidden = false; });
                    search.focus();
                }
            }
            return;
        }

        var option = event.target.closest("[data-custom-select-menu] [role='option']");
        if (option) {
            var field = option.closest(".apts-custom-select-field");
            var select = option.closest("[data-custom-select]");
            var valueInput = bySelector("[data-custom-select-value]", field);
            var label = bySelector("[data-custom-select-label]", select);
            var oldValue = valueInput ? valueInput.value : "";
            var value = option.getAttribute("data-value") || "";
            if (valueInput) {
                valueInput.value = value;
                valueInput.dispatchEvent(new Event("change", { bubbles: true }));
            }
            if (label) {
                label.textContent = option.textContent.trim();
            }
            closeSelect(select);
            if (valueInput && valueInput.id === "CollectionCustomer" && oldValue !== value) {
                clearMembers();
            }
            return;
        }

        var groupRow = event.target.closest("[data-member-collection-group-index]");
        if (groupRow) {
            openMemberGroupPopup(parseInt(groupRow.getAttribute("data-member-collection-group-index"), 10));
            return;
        }

        if (event.target.closest("[data-member-collection-group-close]")) {
            closeMemberGroupPopup();
            return;
        }

        if (event.target.closest("[data-member-collection-add]")) {
            validateAndAddMember();
            return;
        }
        if (event.target.closest("[data-member-collection-clear]")) {
            clearMembers();
            return;
        }
        if (!event.target.closest("[data-custom-select]")) {
            closeAllSelects();
        }
    });

    document.addEventListener("input", function (event) {
        if (event.target.matches("[data-member-collection-input]")) {
            event.target.value = event.target.value.toUpperCase();
            return;
        }
        var search = event.target.closest("[data-custom-select-search]");
        if (search) {
            var menu = search.closest("[data-custom-select-menu]");
            var text = search.value.trim().toUpperCase();
            menu.querySelectorAll("[role='option']").forEach(function (option) {
                option.hidden = text && option.textContent.toUpperCase().indexOf(text) === -1;
            });
        }
    });

    document.addEventListener("keydown", function (event) {
        if (event.key === "Enter" && event.target.matches("[data-member-collection-input]")) {
            event.preventDefault();
            validateAndAddMember();
        }
        if (event.key === "Enter" && event.target.matches("[data-member-collection-group-index]")) {
            event.preventDefault();
            openMemberGroupPopup(parseInt(event.target.getAttribute("data-member-collection-group-index"), 10));
        }
        if (event.key === "Escape") {
            closeAllSelects();
            closeMemberGroupPopup();
        }
    });

    document.addEventListener("apts:qr-scan", function (event) {
        if (event.target && event.target.matches("[data-member-collection-input]")) {
            validateAndAddMember();
        }
    });

    document.addEventListener("submit", function (event) {
        var form = event.target && event.target.closest("[data-member-collection-form]");
        if (!form) {
            return;
        }
        if (!bySelector("#CollectionCustomer").value) {
            event.preventDefault();
            showPopup("Please select Disassemble from.");
            return;
        }
        if (!members.length) {
            event.preventDefault();
            showPopup("Please add at least one member.");
        }
    });

    renderMembers();
}());