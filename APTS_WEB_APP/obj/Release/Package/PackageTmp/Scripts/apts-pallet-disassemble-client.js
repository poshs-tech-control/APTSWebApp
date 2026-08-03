(function () {
    var members = [];
    var palletCode = "";

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
        var form = bySelector("[data-disassemble-form]");
        return form ? form.getAttribute(attributeName) : "";
    }

    function uppercaseInputValue(input) {
        if (input && input.value) {
            input.value = input.value.toUpperCase();
        }
    }

    function setStatus(state, message) {
        var status = bySelector("[data-disassemble-status]");
        var input = bySelector("[data-disassemble-member-input]");

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

    function showPopup(message, onClose) {
        setStatus("", "");

        if (window.AptsProcessPopup) {
            window.AptsProcessPopup.show(message || "Validation failed.", onClose);
            return;
        }

        alert(message || "Validation failed.");
        if (typeof onClose === "function") {
            onClose();
        }
    }

    function normalizeMember(item) {
        item = item || {};

        return {
            rfid: item.Rfid || item.rfid || item.MemberId || item.memberId || "",
            memberCode: item.MemberCode || item.memberCode || "",
            memberType: item.MemberType || item.memberType || "",
            memberSize: item.MemberSize || item.memberSize || "",
            batchNo: item.BatchNo || item.batchNo || "",
            palletCode: item.PalletCode || item.palletCode || "",
            sheetWidth: item.SheetWidth || item.sheetWidth || "",
            sheetLength1: item.SheetLength1 || item.sheetLength1 || "",
            sheetLength2: item.SheetLength2 || item.sheetLength2 || ""
        };
    }

    function updateCount() {
        var count = bySelector("[data-disassemble-count]");
        if (count) {
            count.textContent = members.length;
        }
    }

    function renderMembers() {
        var list = bySelector("[data-disassemble-list]");

        updateCount();

        if (!list) {
            return;
        }

        var rows = members.length
            ? members.map(function (member, index) {
                return [
                    '<tr class="apts-disassemble-member-row">',
                    "<td>" + htmlEncode(String(member.rfid || "").toUpperCase()) + "</td>",
                    "<td>" + htmlEncode(member.memberType || "-") + "</td>",
                    "<td>" + htmlEncode(member.memberSize || "-"),
                    "<input type=\"hidden\" name=\"Members[" + index + "].Rfid\" value=\"" + htmlEncode(member.rfid) + "\" />",
                    "<input type=\"hidden\" name=\"Members[" + index + "].MemberCode\" value=\"" + htmlEncode(member.memberCode) + "\" />",
                    "<input type=\"hidden\" name=\"Members[" + index + "].MemberType\" value=\"" + htmlEncode(member.memberType) + "\" />",
                    "<input type=\"hidden\" name=\"Members[" + index + "].MemberSize\" value=\"" + htmlEncode(member.memberSize) + "\" />",
                    "<input type=\"hidden\" name=\"Members[" + index + "].PalletCode\" value=\"" + htmlEncode(member.palletCode) + "\" />",
                    "</td>",
                    "</tr>"
                ].join("");
            }).join("")
            : '<tr><td class="apts-receipt-empty-cell" colspan="3">No members scanned yet.</td></tr>';

        list.innerHTML = [
            '<div class="apts-sku-table-wrap apts-disassemble-table-wrap">',
            '<table class="apts-sku-table apts-disassemble-member-table">',
            "<thead>",
            "<tr>",
            "<th>Member Id</th>",
            "<th>Type</th>",
            "<th>Size</th>",
            "</tr>",
            "</thead>",
            "<tbody>",
            rows,
            "</tbody>",
            "</table>",
            "</div>"
        ].join("");
    }

    function mergeMembers(items) {
        var added = 0;
        var duplicate = 0;
        var skipped = 0;

        (items || []).forEach(function (item) {
            var member = normalizeMember(item);
            var key = String(member.rfid || "").toUpperCase();

            if (!key || !member.memberCode) {
                skipped += 1;
                return;
            }

            if (!palletCode && member.palletCode) {
                palletCode = member.palletCode;
            }

            var alreadyAdded = members.some(function (existing) {
                return String(existing.rfid || "").toUpperCase() === key;
            });

            if (!alreadyAdded) {
                members.push(member);
                added += 1;
            } else {
                duplicate += 1;
            }
        });

        return {
            added: added,
            duplicate: duplicate,
            skipped: skipped,
            total: (items || []).length
        };
    }

    function buildSaveFormData() {
        var data = new FormData();
        data.append("PalletCode", palletCode || (members[0] ? members[0].palletCode : ""));

        members.forEach(function (member, index) {
            data.append("Members[" + index + "].Rfid", member.rfid || "");
            data.append("Members[" + index + "].MemberCode", member.memberCode || "");
            data.append("Members[" + index + "].MemberType", member.memberType || "");
            data.append("Members[" + index + "].MemberSize", member.memberSize || "");
            data.append("Members[" + index + "].PalletCode", member.palletCode || palletCode || "");
        });

        return data;
    }

    async function validateMember(value) {
        var endpoint = getFormUrl("data-disassemble-validation-url");
        var body = new URLSearchParams();
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
        var input = bySelector("[data-disassemble-member-input]");
        var addButton = bySelector("[data-disassemble-add]");

        uppercaseInputValue(input);

        var value = input ? input.value.trim() : "";
        if (!value) {
            if (input) {
                input.focus();
            }
            return;
        }

        setStatus("loading", "");
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

        var mergeResult = mergeMembers(result.members || []);
        if (result.palletCode) {
            palletCode = result.palletCode;
        }

        if (mergeResult.added === 0) {
            renderMembers();

            if (mergeResult.duplicate > 0) {
                showPopup("Member " + String(result.rfid || value).toUpperCase() + " already added.");
                return;
            }

            showPopup("Pallet member details not found.");
            return;
        }

        if (input) {
            input.value = "";
            input.focus();
        }

        setStatus("valid", result.message || "Member validated successfully.");
        renderMembers();
    }

    async function saveDisassemble(form) {
        var endpoint = getFormUrl("data-disassemble-save-url");
        var submitButton = form ? form.querySelector("[type='submit']") : null;

        if (!members.length) {
            showPopup("Please select member.");
            return;
        }

        if (!endpoint || !window.fetch) {
            showPopup("Unable to save Pallet Disassemble.");
            return;
        }

        setStatus("loading", "");
        if (submitButton) {
            submitButton.disabled = true;
        }

        try {
            var response = await fetch(endpoint, {
                method: "POST",
                credentials: "same-origin",
                body: buildSaveFormData(),
                headers: { "Accept": "application/json" }
            });
            var result = response.ok ? await response.json() : null;

            setStatus("", "");

            if (result && result.success) {
                showPopup(result.message || "Pallet Disassemble Saved Successfully", function () {
                    members = [];
                    palletCode = "";
                    renderMembers();
                    var input = bySelector("[data-disassemble-member-input]");
                    if (input) {
                        input.value = "";
                    }
                });
                return;
            }

            showPopup(result && result.message ? result.message : "Unable to save Pallet Disassemble.");
        } catch (error) {
            setStatus("", "");
            showPopup("Unable to save Pallet Disassemble.");
        } finally {
            if (submitButton) {
                submitButton.disabled = false;
            }
        }
    }

    document.addEventListener("click", function (event) {
        if (event.target.closest("[data-disassemble-add]")) {
            addMember();
            return;
        }

        if (event.target.closest("[data-disassemble-clear]")) {
            members = [];
            palletCode = "";
            setStatus("", "");
            renderMembers();
            var input = bySelector("[data-disassemble-member-input]");
            if (input) {
                input.value = "";
                input.focus();
            }
        }
    });

    document.addEventListener("input", function (event) {
        if (event.target.matches("[data-disassemble-member-input]")) {
            uppercaseInputValue(event.target);
            setStatus("", "");
        }
    });

    document.addEventListener("keydown", function (event) {
        if (event.key === "Enter" && event.target.matches("[data-disassemble-member-input]")) {
            event.preventDefault();
            addMember();
        }
    });

    document.addEventListener("submit", function (event) {
        var form = event.target && event.target.closest("[data-disassemble-form]");
        if (!form) {
            return;
        }

        event.preventDefault();
        saveDisassemble(form);
    });

    renderMembers();
}());
