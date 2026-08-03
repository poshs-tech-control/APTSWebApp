(function () {
    var members = [];
    var disassembledPallets = [];
    var palletCode = "";

    function bySelector(selector) {
        return document.querySelector(selector);
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

    function getFormUrl(attributeName) {
        var form = bySelector("[data-disassemble-customer-form]");
        return form ? form.getAttribute(attributeName) : "";
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

    function setStatus(message, state) {
        var status = bySelector("[data-disassemble-customer-status]");
        var input = bySelector("[data-disassemble-customer-input]");

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

    function showPopup(message) {
        setStatus("", "");

        if (window.AptsProcessPopup) {
            window.AptsProcessPopup.show(message || "Validation failed.");
            return;
        }

        alert(message || "Validation failed.");
    }

    function showToast(message) {
        if (!message) {
            return;
        }

        var toast = document.createElement("div");
        toast.className = "apts-background-toast";
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

    async function generateChallanInBackground(custDismentleCode) {
        var endpoint = getFormUrl("data-disassemble-customer-challan-url");
        var body = new URLSearchParams();
        body.append("custDismentleCode", custDismentleCode || "");

        if (!endpoint || !custDismentleCode || !window.fetch) {
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

    function uppercaseInput(input) {
        if (input && input.value) {
            input.value = input.value.toUpperCase();
        }
    }

    function getCurrentLocationMode() {
        var selected = bySelector("input[name='currentLocation']:checked");
        return selected ? selected.value : "PalletDispatch";
    }

    function updateSummary() {
        var count = bySelector("[data-disassemble-customer-count]");
        var pallet = bySelector("[data-customer-pallet-code]");

        if (count) {
            count.textContent = disassembledPallets.length;
        }
        if (pallet) {
            pallet.textContent = palletCode || "-";
        }
    }

    function isFinalDiscrepancyAction(actionType) {
        return actionType === "Loss" || actionType === "Damage";
    }

    function getSaveActionType(member) {
        return isFinalDiscrepancyAction(member && member.actionType) ? member.actionType : "Y";
    }

    function hasPendingDiscrepancyMembers(selectedMembers) {
        return (selectedMembers || []).some(function (member) {
            return member && member.actionType && member.actionType !== "Y" && !isFinalDiscrepancyAction(member.actionType);
        });
    }
    function getCheckedMembers() {
        return members.map(function (member) {
            var checkbox = document.querySelector("[data-disassemble-customer-list] input[name='CheckedMembers'][value='" + cssEscape(String(member.rfid || "")) + "']");
            var hasDiscrepancyAction = member.actionType && member.actionType !== "Y";
            if ((checkbox && checkbox.checked) || hasDiscrepancyAction) {
                member.actionType = hasDiscrepancyAction ? member.actionType : "Discrepancy";
                member.discrepancy = member.actionType;
            } else {
                member.actionType = "Y";
                member.comment = "";
                member.discrepancy = "No";
            }
            return member;
        });
    }

    function cssEscape(value) {
        if (window.CSS && typeof window.CSS.escape === "function") {
            return window.CSS.escape(value);
        }

        return String(value || "").replace(/["\\]/g, "\\$&");
    }

    function buildTempSaveFormData(code, selectedMembers) {
        var data = new FormData();
        data.append("CustDismentleCode", code || "");

        (selectedMembers || []).forEach(function (member, index) {
            data.append("Members[" + index + "].Rfid", member.rfid || "");
            data.append("Members[" + index + "].MemberCode", member.memberCode || "");
            data.append("Members[" + index + "].MemberSize", member.memberSize || "");
            data.append("Members[" + index + "].PalletCode", member.palletCode || "");
        });

        return data;
    }

    async function saveTempDisassembleMembers(code, selectedMembers) {
        var endpoint = getFormUrl("data-disassemble-customer-temp-save-url");

        if (!endpoint || !window.fetch) {
            return {
                success: false,
                message: "Unable to save Pallet Dismantle Customer."
            };
        }

        try {
            var response = await fetch(endpoint, {
                method: "POST",
                credentials: "same-origin",
                body: buildTempSaveFormData(code, selectedMembers),
                headers: { "Accept": "application/json" }
            });

            return response.ok ? await response.json() : {
                success: false,
                message: "Unable to save Pallet Dismantle Customer."
            };
        } catch (error) {
            return {
                success: false,
                message: "Unable to save Pallet Dismantle Customer."
            };
        }
    }

    async function clearTempDisassembleMembers() {
        var endpoint = getFormUrl("data-disassemble-customer-temp-clear-url");

        if (!endpoint || !window.fetch) {
            return false;
        }

        try {
            var response = await fetch(endpoint, {
                method: "POST",
                credentials: "same-origin",
                headers: { "Accept": "application/json" }
            });
            var result = response.ok ? await response.json() : null;
            return !!(result && result.success);
        } catch (error) {
            return false;
        }
    }

    function isDuplicateTempMessage(message) {
        return /already\s+exist/i.test(String(message || ""));
    }

    function buildTempDisassembleCode() {
        var now = new Date();
        var pad = function (value, size) {
            return String(value).padStart(size || 2, "0");
        };

        return "Dismantle" +
            now.getFullYear() +
            pad(now.getMonth() + 1) +
            pad(now.getDate()) +
            pad(now.getHours()) +
            pad(now.getMinutes()) +
            pad(now.getSeconds()) +
            pad(now.getMilliseconds(), 3) +
            pad(disassembledPallets.length + 1);
    }
    async function loadTempDisassembleDetails(code) {
        var endpoint = getFormUrl("data-disassemble-customer-temp-details-url");
        var body = new URLSearchParams();
        body.append("custDismentleCode", code || "");

        if (!endpoint || !window.fetch) {
            return {
                success: false,
                message: "Unable to fetch details.",
                items: []
            };
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

            return response.ok ? await response.json() : {
                success: false,
                message: "Unable to fetch details.",
                items: []
            };
        } catch (error) {
            return {
                success: false,
                message: "Unable to fetch details.",
                items: []
            };
        }
    }

    function buildFinalSubmitFormData() {
        var data = new FormData();
        var customer = bySelector("#DisassembleCustomer");

        data.append("CustomerCode", customer ? customer.value : "");

        disassembledPallets.forEach(function (group, groupIndex) {
            data.append("Groups[" + groupIndex + "].CustDismentleCode", group.code || "");
            data.append("Groups[" + groupIndex + "].PalletCode", group.palletCode || "");

            (group.members || []).forEach(function (member, memberIndex) {
                data.append("Groups[" + groupIndex + "].Members[" + memberIndex + "].Rfid", member.rfid || "");
                data.append("Groups[" + groupIndex + "].Members[" + memberIndex + "].MemberCode", member.memberCode || "");
                data.append("Groups[" + groupIndex + "].Members[" + memberIndex + "].PalletCode", member.palletCode || group.palletCode || "");
                data.append("Groups[" + groupIndex + "].Members[" + memberIndex + "].ClientBatchNo", member.clientBatchNo || "");
                data.append("Groups[" + groupIndex + "].Members[" + memberIndex + "].ActionType", getSaveActionType(member));
                data.append("Groups[" + groupIndex + "].Members[" + memberIndex + "].Comment", member.comment || "");
            });
        });

        return data;
    }

    async function submitFinalDisassemble() {
        var endpoint = getFormUrl("data-disassemble-customer-submit-url");
        var submitButton = bySelector("[data-disassemble-customer-submit]");

        if (!endpoint || !window.fetch) {
            showPopup("Unable to save Pallet Dismantle Customer.");
            return;
        }

        if (submitButton) {
            submitButton.disabled = true;
        }
        setStatus("", "loading");

        try {
            var response = await fetch(endpoint, {
                method: "POST",
                credentials: "same-origin",
                body: buildFinalSubmitFormData(),
                headers: { "Accept": "application/json" }
            });
            var result = response.ok ? await response.json() : null;

            setStatus("", "");

            if (result && result.success) {
                var savedCode = result.custDismentleCode || "";
                members = [];
                disassembledPallets = [];
                palletCode = "";
                resetCustomerSelection();
                renderMembers();
                showPopup(result.message || "Pallet Dismantle Customer Saved Successfully");

                if (result.challanPending && savedCode) {
                    generateChallanInBackground(savedCode);
                }
                return;
            }

            showPopup(result && result.message ? result.message : "Unable to save Pallet Dismantle Customer.");
        } catch (error) {
            setStatus("", "");
            showPopup("Unable to save Pallet Dismantle Customer.");
        } finally {
            if (submitButton) {
                submitButton.disabled = false;
            }
        }
    }

    function ensurePopupShell() {
        var popup = bySelector("[data-disassemble-customer-popup]");

        if (popup) {
            return popup;
        }

        popup = document.createElement("section");
        popup.className = "apts-dismantle-popup";
        popup.setAttribute("data-disassemble-customer-popup", "");
        popup.setAttribute("role", "dialog");
        popup.setAttribute("aria-modal", "true");
        popup.hidden = true;
        popup.innerHTML = [
            '<div class="apts-dismantle-popup__panel">',
            '<div class="apts-dismantle-popup__head">',
            '<h2 data-disassemble-customer-popup-title></h2>',
            '<button type="button" data-disassemble-customer-popup-close aria-label="Close popup">',
            '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M6 6l12 12M18 6 6 18" /></svg>',
            "</button>",
            "</div>",
            '<div class="apts-dismantle-popup__body" data-disassemble-customer-popup-body></div>',
            "</div>"
        ].join("");

        document.body.appendChild(popup);
        return popup;
    }

    function closeDismantlePopup() {
        var popup = bySelector("[data-disassemble-customer-popup]");
        if (popup) {
            popup.hidden = true;
            document.body.classList.remove("apts-process-popup-open");
        }
    }

    function showDismantlePopup(title, bodyHtml) {
        var popup = ensurePopupShell();
        var titleElement = popup.querySelector("[data-disassemble-customer-popup-title]");
        var bodyElement = popup.querySelector("[data-disassemble-customer-popup-body]");

        if (titleElement) {
            titleElement.textContent = title || "";
        }
        if (bodyElement) {
            bodyElement.innerHTML = bodyHtml || "";
        }

        popup.hidden = false;
        document.body.classList.add("apts-process-popup-open");
    }

    function buildAllPalletsRows() {
        if (!disassembledPallets.length) {
            return '<tr><td class="apts-receipt-empty-cell" colspan="2">No pallets disassembled yet.</td></tr>';
        }

        return disassembledPallets.map(function (item, index) {
            return [
                '<tr data-disassemble-group-index="' + index + '" tabindex="0">',
                "<td>" + escapeHtml(item.code) + "</td>",
                "<td>" + escapeHtml(item.members.length) + "</td>",
                "</tr>"
            ].join("");
        }).join("");
    }

    function hasFinalDiscrepancyAction(member) {
        return member && (member.actionType === "Damage" || member.actionType === "Loss");
    }

    function getSelectedDiscrepancyMembers() {
        return members.filter(function (member) {
            var checkbox = document.querySelector("[data-disassemble-customer-list] input[name='CheckedMembers'][value='" + cssEscape(String(member.rfid || "")) + "']");
            return !!(checkbox && checkbox.checked);
        });
    }

    function getEditableDiscrepancyMembers() {
        var selectedMembers = getSelectedDiscrepancyMembers();
        var pendingMembers = selectedMembers.filter(function (member) {
            return !hasFinalDiscrepancyAction(member);
        });

        return pendingMembers.length ? pendingMembers : selectedMembers;
    }

    function buildSelectedSizeText(selectedMembers) {
        var counts = {};
        (selectedMembers || []).forEach(function (member) {
            var size = member.memberSize || "-";
            counts[size] = (counts[size] || 0) + 1;
        });

        return Object.keys(counts).map(function (size) {
            return counts[size] > 1 ? size + " (" + counts[size] + ")" : size;
        }).join(", ") || "-";
    }

    function showDiscrepancyPopup() {
        var selectedMembers = getSelectedDiscrepancyMembers();
        var editableMembers = getEditableDiscrepancyMembers();
        var firstAction = editableMembers.length ? editableMembers[0].actionType || "" : "";
        var firstComment = editableMembers.length ? editableMembers[0].comment || "" : "";
        var checkedAction = firstAction === "Damage" || firstAction === "Loss" ? firstAction : "";

        if (!members.length) {
            showPopup("Please scan member.");
            return;
        }

        if (!selectedMembers.length) {
            showPopup("Please select discrepancy member.");
            return;
        }

        showDismantlePopup("Pallet Discrepancy", [
            '<div class="apts-discrepancy-popup-form">',
            '<div class="apts-discrepancy-popup-summary">',
            '<span>Checked Member List</span>',
            '<strong>' + editableMembers.length + '</strong>',
            '<span>Size :</span>',
            '<strong>' + escapeHtml(buildSelectedSizeText(editableMembers)) + '</strong>',
            '</div>',
            '<fieldset class="apts-discrepancy-popup-actions">',
            '<legend>Select Action -</legend>',
            '<label><input type="radio" name="DisassembleDiscrepancyAction" value="Loss"' + (checkedAction === "Loss" ? " checked" : "") + ' /><span>Loss</span></label>',
            '<label><input type="radio" name="DisassembleDiscrepancyAction" value="Damage"' + (checkedAction === "Damage" ? " checked" : "") + ' /><span>Damage</span></label>',
            '</fieldset>',
            '<label class="apts-discrepancy-popup-comment">',
            '<span>Comment</span>',
            '<textarea rows="2" maxlength="250" data-discrepancy-comment>' + escapeHtml(firstComment) + '</textarea>',
            '</label>',
            '<div class="apts-discrepancy-popup-buttons">',
            '<button type="button" data-discrepancy-back>BACK</button>',
            '<button type="button" data-discrepancy-submit>SUBMIT</button>',
            '</div>',
            '</div>'
        ].join(""));
    }

    function submitDiscrepancyPopup() {
        var popup = bySelector("[data-disassemble-customer-popup]");
        var selectedAction = popup ? popup.querySelector("input[name='DisassembleDiscrepancyAction']:checked") : null;
        var comment = popup ? popup.querySelector("[data-discrepancy-comment]") : null;
        var selectedMembers = getEditableDiscrepancyMembers();

        if (!selectedAction) {
            showPopup("Please select discrepancy action.");
            return;
        }

        selectedMembers.forEach(function (member) {
            member.actionType = selectedAction.value;
            member.comment = comment ? comment.value.trim() : "";
            member.discrepancy = selectedAction.value;
        });

        renderMembers();
        closeDismantlePopup();
        setStatus("Discrepancy details updated.", "valid");
    }    function showAllPalletsPopup() {
        showDismantlePopup("All Pallets", [
            '<div class="apts-dismantle-popup-table-wrap">',
            '<table class="apts-dismantle-popup-table">',
            "<thead><tr><th>Disassemble Code</th><th>Count</th></tr></thead>",
            "<tbody>",
            buildAllPalletsRows(),
            "</tbody>",
            "</table>",
            "</div>"
        ].join(""));
    }

    async function showDisassembleMembersPopup(index) {
        var group = disassembledPallets[index];

        if (!group) {
            return;
        }

        showDismantlePopup(group.code + " Members", '<p class="apts-dismantle-popup-loading">Please wait</p>');

        var result = await loadTempDisassembleDetails(group.code);
        var detailItems = result && result.success && Array.isArray(result.items) ? result.items : [];

        if (!detailItems.length) {
            showDismantlePopup(group.code + " Members", [
                '<div class="apts-dismantle-popup-table-wrap">',
                '<table class="apts-dismantle-popup-table">',
                "<thead><tr><th>Member</th><th>Size</th></tr></thead>",
                "<tbody>",
                '<tr><td class="apts-receipt-empty-cell" colspan="2">' + escapeHtml(result && result.message ? result.message : "Details not found.") + "</td></tr>",
                "</tbody>",
                "</table>",
                "</div>"
            ].join(""));
            return;
        }

        var rows = detailItems.map(function (member) {
            var rfid = String(member.Rfid || member.rfid || "").toUpperCase();
            var savedMember = group.members.filter(function (item) {
                return String(item.rfid || "").toUpperCase() === rfid;
            })[0] || {};

            return [
                "<tr>",
                "<td>" + escapeHtml(rfid) + "</td>",
                "<td>" + escapeHtml(savedMember.memberSize || "-") + "</td>",
                "</tr>"
            ].join("");
        }).join("");

        showDismantlePopup(group.code + " Members", [
            '<div class="apts-dismantle-popup-table-wrap">',
            '<table class="apts-dismantle-popup-table">',
            "<thead><tr><th>Member</th><th>Size</th></tr></thead>",
            "<tbody>",
            rows || '<tr><td class="apts-receipt-empty-cell" colspan="2">No members found.</td></tr>',
            "</tbody>",
            "</table>",
            "</div>"
        ].join(""));
    }

    function clearCurrentMemberGrid() {
        var input = bySelector("[data-disassemble-customer-input]");

        members = [];
        palletCode = "";
        renderMembers();
        setStatus("", "");

        if (input) {
            input.value = "";
            input.focus();
        }
    }

    function renderMembers() {
        var list = bySelector("[data-disassemble-customer-list]");
        updateSummary();

        if (!list) {
            return;
        }

        var rows = members.length
            ? members.map(function (member, index) {
                var discrepancyAction = member.actionType && member.actionType !== "Y" ? member.actionType : "";
                var isDiscrepancy = !!discrepancyAction;
                return [
                    '<tr data-disassemble-customer-row="' + escapeHtml(member.rfid) + '">',
                    "<td>" + escapeHtml(String(member.rfid || "").toUpperCase()) + "</td>",
                    "<td>" + escapeHtml(member.memberSize || "-") + "</td>",
                    '<td class="apts-sku-select"><input type="checkbox" name="CheckedMembers" value="' + escapeHtml(member.rfid) + '" aria-label="Mark discrepancy"' + (isDiscrepancy ? ' checked' : '') + ' />',
                    "<input type=\"hidden\" name=\"Members[" + index + "].Rfid\" value=\"" + escapeHtml(member.rfid) + "\" />",
                    "<input type=\"hidden\" name=\"Members[" + index + "].MemberCode\" value=\"" + escapeHtml(member.memberCode) + "\" />",
                    "<input type=\"hidden\" name=\"Members[" + index + "].MemberType\" value=\"" + escapeHtml(member.memberType) + "\" />",
                    "<input type=\"hidden\" name=\"Members[" + index + "].MemberSize\" value=\"" + escapeHtml(member.memberSize) + "\" />",
                    "<input type=\"hidden\" name=\"Members[" + index + "].PalletDispDTCode\" value=\"" + escapeHtml(member.palletDispDTCode) + "\" />",
                    "<input type=\"hidden\" name=\"Members[" + index + "].PalletCode\" value=\"" + escapeHtml(member.palletCode) + "\" />",
                    "<input type=\"hidden\" name=\"Members[" + index + "].ClientBatchNo\" value=\"" + escapeHtml(member.clientBatchNo) + "\" />",
                    "</td>",
                    '<td data-discrepancy-cell>' + escapeHtml(discrepancyAction || "No") + "</td>",
                    "</tr>"
                ].join("");
            }).join("")
            : '<tr><td class="apts-receipt-empty-cell" colspan="4">No members scanned yet.</td></tr>';

        list.innerHTML = [
            '<div class="apts-sku-table-wrap apts-disassemble-customer-table-wrap">',
            '<table class="apts-sku-table apts-disassemble-customer-table">',
            "<thead>",
            "<tr>",
            "<th>Member</th>",
            "<th>Size</th>",
            '<th class="apts-sku-select"><input type="checkbox" data-disassemble-customer-select-all aria-label="Mark all discrepancy" /></th>',
            "<th>Discrepancy</th>",
            "</tr>",
            "</thead>",
            "<tbody>",
            rows,
            "</tbody>",
            "</table>",
            "</div>"
        ].join("");
    }

    function normalizeMember(item) {
        item = item || {};
        return {
            palletDispDTCode: item.PalletDispDTCode || item.palletDispDTCode || "",
            memberCode: item.MemberCode || item.memberCode || "",
            memberType: item.MemberType || item.memberType || "",
            palletCode: item.PalletCode || item.palletCode || "",
            rfid: item.Rfid || item.rfid || item.RFID || item.MemberId || item.memberId || "",
            clientBatchNo: item.ClientBatchNo || item.clientBatchNo || "",
            memberSize: item.MemberSize || item.memberSize || "",
            sheetWidth: item.SheetWidth || item.sheetWidth || "",
            sheetLength1: item.SheetLength1 || item.sheetLength1 || "",
            sheetLength2: item.SheetLength2 || item.sheetLength2 || "",
            discrepancy: item.Discrepancy || item.discrepancy || "No"
        };
    }

    function mergeMembers(items) {
        var added = 0;
        var duplicate = 0;
        var skipped = 0;

        (items || []).forEach(function (item) {
            var member = normalizeMember(item);
            var key = String(member.rfid || "").toUpperCase();

            if (!key) {
                skipped += 1;
                return;
            }

            if (!palletCode && member.palletCode) {
                palletCode = member.palletCode;
            }

            var exists = members.some(function (existing) {
                return String(existing.rfid || "").toUpperCase() === key;
            });

            if (exists) {
                duplicate += 1;
                return;
            }

            members.push(member);
            added += 1;
        });

        return {
            added: added,
            duplicate: duplicate,
            skipped: skipped,
            total: (items || []).length
        };
    }

    async function validateMember(value) {
        var endpoint = getFormUrl("data-disassemble-customer-validation-url");
        var body = new URLSearchParams();
        body.append("currentLocation", getCurrentLocationMode());
        body.append("tagRfid", value);

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
                keepalive: true,
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

    async function addMember() {
        var input = bySelector("[data-disassemble-customer-input]");
        var addButton = bySelector("[data-disassemble-customer-add]");

        uppercaseInput(input);

        var value = input ? input.value.trim() : "";
        if (!value) {
            if (input) {
                input.focus();
            }
            return;
        }

        setStatus("", "loading");
        if (addButton) {
            addButton.disabled = true;
        }

        var result = await validateMember(value);

        if (addButton) {
            addButton.disabled = false;
        }

        if (!result || !result.success) {
            showPopup(result && result.message ? result.message : "Unable to validate member.");
            return;
        }

        var incomingMembers = result.members || [];
        var firstIncomingMember = incomingMembers.length ? normalizeMember(incomingMembers[0]) : null;
        var incomingPalletCode = String(result.palletCode || (firstIncomingMember ? firstIncomingMember.palletCode : "") || "").trim();
        var activePalletCode = String(palletCode || (members[0] ? members[0].palletCode : "") || "").trim();

        if (members.length > 0 && activePalletCode && incomingPalletCode && activePalletCode.toUpperCase() !== incomingPalletCode.toUpperCase()) {
            showPopup("Pallet " + activePalletCode.toUpperCase() + " is already loaded. Please disassemble this pallet before scanning members from another pallet.");
            clearCurrentMemberGrid();
            return;
        }

        if (!palletCode && incomingPalletCode) {
            palletCode = incomingPalletCode;
        }

        var mergeResult = mergeMembers(incomingMembers);
        renderMembers();

        if (mergeResult.added === 0) {
            if (mergeResult.duplicate > 0) {
                showPopup("Member " + String(result.rfid || value).toUpperCase() + " already added.");
                clearCurrentMemberGrid();
                return;
            }

            showPopup("Pallet dispatch details not found.");
            return;
        }

        if (input) {
            input.value = "";
            input.focus();
        }

        setStatus(result.message || "Member validated successfully.", "valid");
    }

    function chooseCustomSelectOption(option) {
        if (!option || !option.getAttribute("data-value")) {
            return;
        }

        var field = option.closest(".apts-custom-select-field");
        var selectRoot = option.closest("[data-custom-select]");
        var valueInput = field ? field.querySelector("[data-custom-select-value]") : null;
        var label = selectRoot ? selectRoot.querySelector("[data-custom-select-label]") : null;
        var addressInput = bySelector("#CustomerAddress");
        var value = option.getAttribute("data-value") || "";
        var displayText = option.textContent.trim();
        var address = option.getAttribute("data-address") || "";

        if (valueInput) {
            valueInput.value = value;
            valueInput.dispatchEvent(new Event("change", { bubbles: true }));
        }
        if (label) {
            label.textContent = displayText;
        }
        if (addressInput) {
            addressInput.value = address;
        }
        if (selectRoot) {
            closeCustomSelect(selectRoot);
        }
    }

    function resetCustomerSelection() {
        var customer = bySelector("#DisassembleCustomer");
        var address = bySelector("#CustomerAddress");
        var field = customer ? customer.closest(".apts-custom-select-field") : null;
        var label = field ? field.querySelector("[data-custom-select-label]") : null;

        if (customer) {
            customer.value = "";
        }
        if (address) {
            address.value = "";
        }
        if (label) {
            label.textContent = "--select--";
        }
    }

    function validateSubmit() {
        var customer = bySelector("#DisassembleCustomer");

        if (!customer || !customer.value) {
            showPopup("Please select To Customer.");
            return false;
        }

        if (!disassembledPallets.length) {
            showPopup("Please disassemble pallet first.");
            return false;
        }

        submitFinalDisassemble();
        return true;
    }

    async function disassembleCheckedMembers() {
        var selectedMembers = getCheckedMembers();
        var customer = bySelector("#DisassembleCustomer");
        var button = bySelector("[data-disassemble-customer-disassemble]");

        if (!customer || !customer.value) {
            showPopup("Please select To Customer.");
            return;
        }

        if (!selectedMembers.length) {
            showPopup("Please scan member.");
            return;
        }

        if (hasPendingDiscrepancyMembers(selectedMembers)) {
            showPopup("Please select Loss or Damage for checked discrepancy member.");
            return;
        }

        var code = buildTempDisassembleCode();
        setStatus("", "loading");
        if (button) {
            button.disabled = true;
        }

        var result = await saveTempDisassembleMembers(code, selectedMembers);

        if ((!result || !result.success) && !disassembledPallets.length && isDuplicateTempMessage(result && result.message)) {
            await clearTempDisassembleMembers();
            code = buildTempDisassembleCode();
            result = await saveTempDisassembleMembers(code, selectedMembers);
        }

        if (button) {
            button.disabled = false;
        }
        setStatus("", "");

        if (!result || !result.success) {
            showPopup(result && result.message ? result.message : "Unable to save Pallet Dismantle Customer.");
            if (isDuplicateTempMessage(result && result.message)) {
                clearCurrentMemberGrid();
            }
            return;
        }

        disassembledPallets.push({
            code: code,
            palletCode: palletCode || (selectedMembers[0] ? selectedMembers[0].palletCode : ""),
            members: selectedMembers.slice()
        });

        members = [];
        palletCode = "";
        renderMembers();
        updateSummary();
        setStatus(result.message || "Members Inserted", "valid");
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
            chooseCustomSelectOption(option);
            return;
        }

        if (event.target.closest("[data-disassemble-customer-add]")) {
            addMember();
            return;
        }

        if (event.target.closest("[data-disassemble-customer-summary]")) {
            showAllPalletsPopup();
            return;
        }

        if (event.target.closest("[data-disassemble-customer-all-pallets]")) {
            showAllPalletsPopup();
            return;
        }

        if (event.target.closest("[data-disassemble-customer-disassemble]")) {
            disassembleCheckedMembers();
            return;
        }

        var groupRow = event.target.closest("[data-disassemble-group-index]");
        if (groupRow) {
            showDisassembleMembersPopup(parseInt(groupRow.getAttribute("data-disassemble-group-index"), 10));
            return;
        }

        if (event.target.closest("[data-disassemble-customer-popup-close]") || event.target.matches("[data-disassemble-customer-popup]")) {
            closeDismantlePopup();
            return;
        }

        if (event.target.closest("[data-disassemble-customer-discrepancy]")) {
            showDiscrepancyPopup();
            return;
        }

        if (event.target.closest("[data-discrepancy-back]")) {
            closeDismantlePopup();
            return;
        }

        if (event.target.closest("[data-discrepancy-submit]")) {
            submitDiscrepancyPopup();
            return;
        }

        if (event.target.closest("[data-disassemble-customer-submit]")) {
            validateSubmit();
            return;
        }

        if (!event.target.closest("[data-custom-select]")) {
            closeAllCustomSelects();
        }
    });

    document.addEventListener("change", function (event) {
        if (event.target.matches("[data-disassemble-customer-select-all]")) {
            document.querySelectorAll("[data-disassemble-customer-list] input[name='CheckedMembers']").forEach(function (checkbox) {
                checkbox.checked = event.target.checked;
                updateDiscrepancyCell(checkbox);
            });
        }

        if (event.target.matches("[data-disassemble-customer-list] input[name='CheckedMembers']")) {
            updateDiscrepancyCell(event.target);
        }

        if (event.target.matches("input[name='currentLocation']")) {
            setStatus("", "");
        }
    });

    function updateDiscrepancyCell(checkbox) {
        var row = checkbox ? checkbox.closest("[data-disassemble-customer-row]") : null;
        var cell = row ? row.querySelector("[data-discrepancy-cell]") : null;
        var rfid = checkbox ? String(checkbox.value || "").toUpperCase() : "";
        var member = members.filter(function (item) {
            return String(item.rfid || "").toUpperCase() === rfid;
        })[0];

        if (member && !checkbox.checked) {
            member.actionType = "Y";
            member.comment = "";
            member.discrepancy = "No";
        }

        if (cell) {
            cell.textContent = checkbox.checked ? (member && member.actionType && member.actionType !== "Y" ? member.actionType : "Discrepancy") : "No";
        }
    }

    document.addEventListener("input", function (event) {
        if (event.target.matches("[data-disassemble-customer-input]")) {
            uppercaseInput(event.target);
            setStatus("", "");
            return;
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

    document.addEventListener("keydown", function (event) {
        if (event.key === "Enter" && event.target.matches("[data-disassemble-customer-input]")) {
            event.preventDefault();
            addMember();
        }

        if (event.key === "Escape") {
            closeDismantlePopup();
            closeAllCustomSelects();
        }

        if ((event.key === "Enter" || event.key === " ") && event.target.matches("[data-disassemble-group-index]")) {
            event.preventDefault();
            showDisassembleMembersPopup(parseInt(event.target.getAttribute("data-disassemble-group-index"), 10));
        }
    });

    document.addEventListener("submit", function (event) {
        if (event.target && event.target.closest("[data-disassemble-customer-form]")) {
            event.preventDefault();
            validateSubmit();
        }
    });

    renderMembers();
}());






