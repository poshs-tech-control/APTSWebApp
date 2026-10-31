(function () {
    var orderRows = [];
    var selectedOrder = null;
    var form = document.querySelector("[data-assemble-orders-url]");
    var orderList = document.querySelector("[data-assemble-orders]");
    var customerField = document.querySelector("[data-assemble-customer]");
    var detailPanel = document.querySelector("[data-assemble-detail]");
    var memberInput = document.querySelector("[data-assemble-member-input]");
    var memberList = document.querySelector("[data-member-list]");
    var memberCount = document.querySelector("[data-member-count]");
    var memberStatus = document.querySelector("[data-assemble-member-status]");
    var bypassModal = document.querySelector("[data-assemble-bypass]");
    var bypassMessage = document.querySelector("[data-assemble-bypass-message]");
    var bypassContinueButton = document.querySelector("[data-assemble-bypass-continue]");
    var bypassCancelButton = document.querySelector("[data-assemble-bypass-cancel]");
    var messageModal = document.querySelector("[data-assemble-message]");
    var messageText = document.querySelector("[data-assemble-message-text]");
    var messageClose = document.querySelector("[data-assemble-message-close]");
    var qrWrap = document.querySelector("[data-assemble-qr-wrap]");
    var qrImage = document.querySelector("[data-assemble-qr-image]");
    var qrCodeText = document.querySelector("[data-assemble-qr-code]");
    var messageCloseCallback = null;
    var pendingBypassMember = null;
    var scannedMembers = [];
    var isSubmitting = false;

    function showPopup(message, onClose) {
        showMessagePopup(message, onClose, "", "error");
    }

    function showPalletQrPopup(message, palletCode, onClose) {
        showMessagePopup(message, onClose, palletCode || "", "success");
    }

    function showMessagePopup(message, onClose, palletCode, variant) {
        if (detailPanel && !detailPanel.hidden && messageModal && messageText) {
            messageCloseCallback = typeof onClose === "function" ? onClose : null;
            setMessagePopupVariant(variant);
            messageText.textContent = message || "Process Completed Successfully";
            renderPalletQr(palletCode || "");
            messageModal.hidden = false;
            if (messageClose && typeof messageClose.focus === "function") {
                messageClose.focus();
            }
            return;
        }

        if (window.AptsProcessPopup && typeof window.AptsProcessPopup.show === "function") {
            window.AptsProcessPopup.show(message, onClose, variant);
            return;
        }

        alert(message);
        if (typeof onClose === "function") {
            onClose();
        }
    }

    function setMessagePopupVariant(variant) {
        var normalized = variant === "error" || variant === "warning" ? "error" : "success";
        var icon = messageModal ? messageModal.querySelector(".apts-process-popup__icon") : null;

        if (messageModal) {
            messageModal.classList.toggle("is-error", normalized === "error");
            messageModal.classList.toggle("is-success", normalized === "success");
            messageModal.setAttribute("aria-label", normalized === "error" ? "Pallet assemble message" : "Pallet assemble completed");
        }

        if (icon) {
            icon.innerHTML = normalized === "error"
                ? '<svg viewBox="0 0 24 24"><path d="M12 8v5M12 17h.01M10.29 3.86 1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z" /></svg>'
                : '<svg viewBox="0 0 24 24"><path d="m5 12 4 4L19 6" /></svg>';
        }
    }

    function closeMessagePopup() {
        var callback = messageCloseCallback;
        messageCloseCallback = null;
        if (messageModal) {
            messageModal.hidden = true;
        }
        if (memberInput && typeof memberInput.focus === "function" && detailPanel && !detailPanel.hidden) {
            memberInput.focus();
        }
        if (typeof callback === "function") {
            callback();
        }
    }

    function setMemberStatus(message, state) {
        if (!memberStatus) {
            return;
        }

        memberStatus.textContent = message || "";
        memberStatus.classList.remove("is-valid", "is-invalid", "is-loading");
        if (state) {
            memberStatus.classList.add("is-" + state);
        }
    }

    function htmlEncode(value) {
        return String(value == null ? "" : value)
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#39;");
    }
    function renderPalletQr(palletCode) {
        palletCode = String(palletCode || "").trim();
        if (qrCodeText) {
            qrCodeText.textContent = palletCode;
        }

        if (!qrWrap || !qrImage) {
            return;
        }

        if (!palletCode) {
            qrWrap.hidden = true;
            qrImage.removeAttribute("src");
            return;
        }

        var qrUrl = form ? form.getAttribute("data-assemble-qr-url") : "";
        qrWrap.hidden = false;
        qrImage.src = qrUrl ? qrUrl + "?palletCode=" + encodeURIComponent(palletCode) : "";
    }

    function getValue(row, key) {
        return row && row[key] != null ? row[key] : "";
    }

    function getNumber(row, key) {
        var value = parseInt(String(getValue(row, key)).replace(/[^\d-]/g, ""), 10);
        return isNaN(value) ? 0 : value;
    }

    function normalizeCompareValue(value) {
        return String(value == null ? "" : value).trim().toUpperCase();
    }

    function normalizeMemberIdentity(value) {
        return normalizeCompareValue(value).replace(/[^A-Z0-9]/g, "");
    }

    function getMemberIdentityValues(member) {
        member = member || {};
        return [member.id, member.rfid, member.memberCode]
            .map(normalizeMemberIdentity)
            .filter(function (value) { return value !== ""; });
    }

    function hasScannedMember(member) {
        var identities = getMemberIdentityValues(member);
        if (!identities.length) {
            return false;
        }

        return scannedMembers.some(function (existing) {
            var existingIdentities = getMemberIdentityValues(existing);
            return identities.some(function (identity) {
                return existingIdentities.indexOf(identity) >= 0;
            });
        });
    }

    function showDuplicateMemberMessage() {
        setMemberStatus("This member is already added in the scanned members list.", "invalid");
        showPopup("This member is already added in the scanned members list.");
    }

    function getAllowedMemberSizes() {
        return ["CMSize", "LM1Size", "LM2Size"].map(function (key) {
            return normalizeCompareValue(getValue(selectedOrder, key));
        }).filter(function (value) {
            return value !== "";
        });
    }

    function isAllowedMemberSize(memberSize) {
        var normalizedSize = normalizeCompareValue(memberSize);
        return normalizedSize !== "" && getAllowedMemberSizes().indexOf(normalizedSize) >= 0;
    }

    function getAllowedMemberSizesText() {
        return getAllowedMemberSizes().join(", ");
    }

    function getSizeRules() {
        return [
            { label: "CM", sizeKey: "CMSize", qtyKey: "CMSizeQty" },
            { label: "LM1", sizeKey: "LM1Size", qtyKey: "LM1SizeQty" },
            { label: "LM2", sizeKey: "LM2Size", qtyKey: "LM2SizeQty" }
        ].map(function (rule) {
            return {
                label: rule.label,
                size: normalizeCompareValue(getValue(selectedOrder, rule.sizeKey)),
                qty: getNumber(selectedOrder, rule.qtyKey)
            };
        }).filter(function (rule) {
            return rule.size !== "" && rule.qty > 0;
        });
    }

    function getMatchingSizeRules(memberSize) {
        var normalizedSize = normalizeCompareValue(memberSize);
        return getSizeRules().filter(function (rule) {
            return rule.size === normalizedSize;
        });
    }

    function getPreferredSizeRuleLabels(memberType) {
        var normalizedType = normalizeCompareValue(memberType);
        if (normalizedType.indexOf("CROSS") >= 0 || normalizedType === "CM") {
            return ["CM"];
        }

        if (normalizedType.indexOf("LONG") >= 0 || normalizedType.indexOf("LM") >= 0) {
            return ["LM1", "LM2"];
        }

        return [];
    }

    function getScannedRuleCount(rule) {
        if (!rule) {
            return 0;
        }

        return scannedMembers.filter(function (member) {
            return member.sizeRuleLabel === rule.label;
        }).length;
    }

    function getAvailableSizeRule(memberSize, memberType) {
        var matchingRules = getMatchingSizeRules(memberSize);
        var preferredLabels = getPreferredSizeRuleLabels(memberType);
        var preferredRules = preferredLabels.length
            ? matchingRules.filter(function (rule) { return preferredLabels.indexOf(rule.label) >= 0; })
            : matchingRules;
        var candidateRules = preferredRules.length ? preferredRules : matchingRules;

        return candidateRules.filter(function (rule) {
            return getScannedRuleCount(rule) < rule.qty;
        })[0] || null;
    }

    function getSizeCompleteMessage(memberSize, memberType) {
        var matchingRules = getMatchingSizeRules(memberSize);
        var preferredLabels = getPreferredSizeRuleLabels(memberType);
        var preferredRules = preferredLabels.length
            ? matchingRules.filter(function (rule) { return preferredLabels.indexOf(rule.label) >= 0; })
            : matchingRules;
        var completedRule = (preferredRules.length ? preferredRules : matchingRules).filter(function (rule) {
            return getScannedRuleCount(rule) >= rule.qty;
        })[0];

        return completedRule
            ? completedRule.label + " size " + memberSize + " required quantity is already complete."
            : "Member size " + (memberSize || "-") + " is not required for this order.";
    }

    function getCurrentLocation() {
        return getValue(selectedOrder, "Customer");
    }

    function getSelectedCustomerLocationCode()
    {
        return customerField ? customerField.getAttribute("data-assemble-location-code") || "" : "";
    }

    function addValidatedMember(member) {
        if (hasScannedMember(member)) {
            showDuplicateMemberMessage();
            return;
        }

        scannedMembers.push({
            id: member.id || "",
            rfid: member.rfid || "",
            memberCode: member.memberCode || "",
            memberType: member.memberType || "",
            memberSize: member.memberSize || "",
            sizeRuleLabel: member.sizeRuleLabel || ""
        });

        if (memberInput) {
            memberInput.value = "";
        }

        renderMembers();
        setMemberStatus("Member validated successfully and added to the table.", "valid");
    }

    function openBypassModal(member) {
        pendingBypassMember = member;
        setBypassLoading(false);
        if (bypassMessage) {
            bypassMessage.textContent = "Member location (" + (member.lastLocation || "-") + ") does not match selected location (" + (member.currentLocation || getCurrentLocation() || "-") + "). Do you want to update the member's last location and continue?";
        }
        if (bypassModal) {
            bypassModal.hidden = false;
        }
    }

    function closeBypassModal() {
        setBypassLoading(false);
        pendingBypassMember = null;
        if (bypassModal) {
            bypassModal.hidden = true;
        }
    }

    function setBypassLoading(isLoading) {
        if (bypassModal) {
            bypassModal.classList.toggle("is-loading", !!isLoading);
        }

        if (bypassContinueButton) {
            bypassContinueButton.disabled = !!isLoading;
            bypassContinueButton.setAttribute("aria-busy", isLoading ? "true" : "false");
            bypassContinueButton.textContent = isLoading ? "Please Wait" : "Continue";
        }

        if (bypassCancelButton) {
            bypassCancelButton.disabled = !!isLoading;
        }
    }

    function continueBypass() {
        var url = form ? form.getAttribute("data-assemble-bypass-url") : "";
        if (!pendingBypassMember || !url) {
            closeBypassModal();
            showPopup("Member bypass service is not available. Please try again.");
            return;
        }

        if (bypassModal && bypassModal.classList.contains("is-loading")) {
            return;
        }

        var member = pendingBypassMember;
        var body = [
            "rfid=" + encodeURIComponent(member.rfid || ""),
            "palletOrderdtlCode=" + encodeURIComponent(getValue(selectedOrder, "PalletOrderdtlCode")),
            "currentLocation=" + encodeURIComponent(getCurrentLocation())
        ].join("&");

        setMemberStatus("Updating member location...", "loading");
        setBypassLoading(true);
        fetch(url, {
            method: "POST",
            credentials: "same-origin",
            headers: {
                "Content-Type": "application/x-www-form-urlencoded; charset=UTF-8",
                "X-Requested-With": "XMLHttpRequest"
            },
            body: body
        })
            .then(function (response) {
                if (!response.ok) {
                    throw new Error("Unable to continue member bypass.");
                }

                return response.json();
            })
            .then(function (result) {
                closeBypassModal();
                if (!result || result.success === false) {
                    setMemberStatus(result && result.message ? result.message : "Member bypass is not allowed.", "invalid");
                    showPopup(result && result.message ? result.message : "Member bypass is not allowed.");
                    return;
                }

                addValidatedMember(member);
            })
            .catch(function () {
                closeBypassModal();
                setMemberStatus("Unable to continue member bypass. Please try again.", "invalid");
                showPopup("Unable to continue member bypass. Please try again.");
            });
    }

    function setOrderMessage(message) {
        if (orderList) {
            renderOrders([], message);
        }
    }

    function renderOrders(rows, emptyMessage, keepDetailsOpen) {
        if (!orderList) {
            return;
        }

        if (!keepDetailsOpen) {
            selectedOrder = null;
            closeOrderDetails();
        }

        orderList.innerHTML = [
            '<div class="apts-assemble-order-table-wrap">',
            '<table class="apts-assemble-order-table">',
            "<thead>",
            "<tr>",
            "<th>Shape</th>",
            "<th>Width</th>",
            "<th>Length 1</th>",
            "<th>Length 2</th>",
            "</tr>",
            "</thead>",
            "<tbody>",
            rows.length ? rows.map(function (row, index) {
                return [
                    '<tr tabindex="0" role="button" data-assemble-order-index="' + index + '">',
                    "<td>" + htmlEncode(getValue(row, "PalletShape")) + "</td>",
                    "<td>" + htmlEncode(getValue(row, "SheetWidth")) + "</td>",
                    "<td>" + htmlEncode(getValue(row, "SheetLength1")) + "</td>",
                    "<td>" + htmlEncode(getValue(row, "SheetLength2")) + "</td>",
                    "</tr>"
                ].join("");
            }).join("") : '<tr class="apts-assemble-order-empty"><td colspan="4">' + htmlEncode(emptyMessage || "No pending pallet orders found.") + "</td></tr>",
            "</tbody>",
            "</table>",
            "</div>"
        ].join("");
    }

    function loadOrders(customerCode) {
        var url = form ? form.getAttribute("data-assemble-orders-url") : "";
        orderRows = [];
        renderOrders(orderRows);

        if (!customerCode) {
            setOrderMessage("Select a customer to view pallet orders.");
            return;
        }

        if (!url) {
            setOrderMessage("Pallet order service is not configured.");
            return;
        }

        setOrderMessage("Loading pallet orders...");

        fetch(url + "?customerCode=" + encodeURIComponent(customerCode), {
            method: "GET",
            credentials: "same-origin",
            headers: {
                "X-Requested-With": "XMLHttpRequest"
            }
        })
            .then(function (response) {
                if (!response.ok) {
                    throw new Error("Unable to load pallet orders.");
                }

                return response.json();
            })
            .then(function (result) {
                isSubmitting = false;
                if (!result || result.success === false) {
                    setOrderMessage(result && result.message ? result.message : "Unable to load pallet orders.");
                    return;
                }

                orderRows = Array.isArray(result.orders) ? result.orders : [];
                renderOrders(orderRows);
            })
            .catch(function () {
                setOrderMessage("Unable to load pallet orders.");
            });
    }

    function getOrderKey(row) {
        return String(getValue(row, "PalletOrderdtlCode") || getValue(row, "PalletOrderCode") || "");
    }

    function isOrderComplete(row) {
        return row && getNumber(row, "PendingCount") <= 0;
    }

    function completeSelectedOrder() {
        selectedOrder = null;
        scannedMembers = [];
        pendingBypassMember = null;
        renderMembers();
        closeOrderDetails();
    }

    function populateOrderDetails() {
        if (!selectedOrder || !detailPanel) {
            return;
        }

        Array.prototype.forEach.call(detailPanel.querySelectorAll("[data-detail]"), function (node) {
            var key = node.getAttribute("data-detail");
            node.textContent = getValue(selectedOrder, key) || "-";
        });

        var customerTitle = detailPanel.querySelector("[data-detail-title-customer]");
        if (customerTitle) {
            customerTitle.textContent = getValue(selectedOrder, "Customer") || "-";
        }
    }

    function showOrderDetails(index) {
        selectedOrder = orderRows[index];

        if (!selectedOrder || !detailPanel) {
            return;
        }

        scannedMembers = [];
        pendingBypassMember = null;
        renderMembers();
        setMemberStatus("", "");
        populateOrderDetails();
        detailPanel.hidden = false;
    }

    function refreshSelectedOrderDetails() {
        var url = form ? form.getAttribute("data-assemble-orders-url") : "";
        var customerCode = customerField ? customerField.value : "";
        var selectedKey = getOrderKey(selectedOrder);

        if (!url || !customerCode || !selectedKey) {
            return;
        }

        fetch(url + "?customerCode=" + encodeURIComponent(customerCode), {
            method: "GET",
            credentials: "same-origin",
            headers: {
                "X-Requested-With": "XMLHttpRequest"
            }
        })
            .then(function (response) {
                return response.ok ? response.json() : null;
            })
            .then(function (result) {
                isSubmitting = false;
                if (!result || result.success === false) {
                    return;
                }

                orderRows = Array.isArray(result.orders) ? result.orders : [];
                renderOrders(orderRows, result.message || "No pending pallet orders found.", true);

                var refreshedOrder = orderRows.filter(function (row) {
                    return getOrderKey(row) === selectedKey;
                })[0] || null;

                if (refreshedOrder) {
                    if (isOrderComplete(refreshedOrder)) {
                        completeSelectedOrder();
                        return;
                    }

                    selectedOrder = refreshedOrder;
                    populateOrderDetails();
                    return;
                }

                completeSelectedOrder();
            })
            .catch(function () {
            });
    }

    function closeOrderDetails() {
        if (detailPanel) {
            detailPanel.hidden = true;
        }
        closeMessagePopup();
        closeBypassModal();
    }

    function renderMembers() {
        if (memberCount) {
            memberCount.textContent = scannedMembers.length;
        }

        if (!memberList) {
            return;
        }

        memberList.innerHTML = [
            '<div class="apts-assemble-member-table-wrap">',
            '<table class="apts-assemble-member-table">',
            "<thead><tr><th>Member</th><th>Type</th><th>Size</th></tr></thead>",
            "<tbody>",
            scannedMembers.length ? scannedMembers.map(function (member) {
                return [
                    "<tr>",
                    "<td>" + htmlEncode(member.rfid || member.id) + "</td>",
                    "<td>" + htmlEncode(member.memberType || "-") + "</td>",
                    "<td>" + htmlEncode(member.memberSize || "-") + "</td>",
                    "</tr>"
                ].join("");
            }).join("") : '<tr class="apts-assemble-member-empty"><td colspan="3">No members scanned yet.</td></tr>',
            "</tbody>",
            "</table>",
            "</div>"
        ].join("");
    }

    function addMember() {
        var value = memberInput ? memberInput.value.trim().toUpperCase() : "";
        var url = form ? form.getAttribute("data-assemble-validate-url") : "";
        if (!value) {
            setMemberStatus("Please scan or enter a member QR code.", "invalid");
            showPopup("Please scan or enter a member QR code.");
            return;
        }

        if (!selectedOrder) {
            setMemberStatus("Please select a pallet order before adding members.", "invalid");
            showPopup("Please select a pallet order before adding members.");
            return;
        }

        if (!url) {
            setMemberStatus("Member validation service is not available. Please try again.", "invalid");
            showPopup("Member validation service is not available. Please try again.");
            return;
        }

        if (hasScannedMember({ id: value, rfid: value, memberCode: value })) {
            showDuplicateMemberMessage();
            return;
        }

        if (getNumber(selectedOrder, "TotalMemQty") > 0 && scannedMembers.length >= getNumber(selectedOrder, "TotalMemQty")) {
            setMemberStatus("Required member quantity is already complete for this pallet order.", "invalid");
            showPopup("Required member quantity is already complete for this pallet order.");
            return;
        }

        setMemberStatus("Validating member...", "loading");

        fetch(url + "?tagRfid=" + encodeURIComponent(value) + "&currentLocation=" + encodeURIComponent(getCurrentLocation()), {
            method: "GET",
            credentials: "same-origin",
            headers: {
                "X-Requested-With": "XMLHttpRequest"
            }
        })
            .then(function (response) {
                if (!response.ok) {
                    throw new Error("Unable to validate member.");
                }

                return response.json();
            })
            .then(function (result) {
                isSubmitting = false;
                if (!result || result.success === false) {
                    setMemberStatus(result && result.message ? result.message : "Member is not in Pre Dispatch status.", "invalid");
                    showPopup(result && result.message ? result.message : "Member is not in Pre Dispatch status.");
                    return;
                }

                if (!isAllowedMemberSize(result.memberSize)) {
                    var sizeMessage = "Member size " + (result.memberSize || "-") + " does not match this order. Allowed sizes: " + (getAllowedMemberSizesText() || "-") + ".";
                    setMemberStatus(sizeMessage, "invalid");
                    showPopup(sizeMessage);
                    return;
                }

                var sizeRule = getAvailableSizeRule(result.memberSize, result.memberType);
                if (!sizeRule) {
                    var sizeLimitMessage = getSizeCompleteMessage(result.memberSize, result.memberType);
                    setMemberStatus(sizeLimitMessage, "invalid");
                    showPopup(sizeLimitMessage);
                    return;
                }

                var duplicateCheckMember = {
                    id: value,
                    rfid: result.rfid || "",
                    memberCode: result.memberCode || ""
                };

                if (hasScannedMember(duplicateCheckMember)) {
                    showDuplicateMemberMessage();
                    return;
                }

                var validatedMember = {
                    id: value,
                    rfid: result.rfid || "",
                    memberCode: result.memberCode || "",
                    memberType: result.memberType || "",
                    memberSize: result.memberSize || "",
                    sizeRuleLabel: sizeRule.label,
                    lastLocation: result.lastLocation || "",
                    currentLocation: result.currentLocation || getCurrentLocation()
                };

                if (result.locationMismatch) {
                    pendingBypassMember = validatedMember;
                    continueBypass();
                    return;
                }

                addValidatedMember(validatedMember);
            })
            .catch(function () {
                setMemberStatus("Unable to validate member right now. Please try again.", "invalid");
                showPopup("Unable to validate member right now. Please try again.");
            });
    }

    function submitAssemble() {
        var url = form ? form.getAttribute("data-assemble-save-url") : "";
        if (isSubmitting) {
            return;
        }
        if (!selectedOrder) {
            showPopup("Please select a pallet order before submit.");
            return;
        }

        if (!url) {
            showPopup("Pallet Assemble save service is not configured.");
            return;
        }

        if (!scannedMembers.length) {
            showPopup("Please scan at least one member.");
            return;
        }

        var totalRequired = getNumber(selectedOrder, "TotalMemQty");
        if (totalRequired > 0 && scannedMembers.length !== totalRequired) {
            showPopup("Please scan all required members before submit.");
            return;
        }

        var bodyParts = [
            "ClientLocation=" + encodeURIComponent(getSelectedCustomerLocationCode() || getValue(selectedOrder, "Customer")),
            "ToLocation=" + encodeURIComponent(getValue(selectedOrder, "Customer")),
            "PalletOrderCode=" + encodeURIComponent(getValue(selectedOrder, "PalletOrderCode")),
            "PalletOrderdtlCode=" + encodeURIComponent(getValue(selectedOrder, "PalletOrderdtlCode")),
            "SheetWidth=" + encodeURIComponent(getValue(selectedOrder, "SheetWidth")),
            "SheetLength1=" + encodeURIComponent(getValue(selectedOrder, "SheetLength1")),
            "SheetLength2=" + encodeURIComponent(getValue(selectedOrder, "SheetLength2")),
            "TotalMember=" + encodeURIComponent(scannedMembers.length)
        ];

        scannedMembers.forEach(function (member, index) {
            bodyParts.push("Members[" + index + "].Rfid=" + encodeURIComponent(member.rfid || member.id || ""));
            bodyParts.push("Members[" + index + "].MemberCode=" + encodeURIComponent(member.memberCode || ""));
        });

        var body = bodyParts.join("&");

        isSubmitting = true;
        setMemberStatus("Saving pallet assemble...", "loading");
        fetch(url, {
            method: "POST",
            credentials: "same-origin",
            headers: {
                "Content-Type": "application/x-www-form-urlencoded; charset=UTF-8",
                "X-Requested-With": "XMLHttpRequest"
            },
            body: body
        })
            .then(function (response) {
                if (!response.ok) {
                    throw new Error("Unable to save Pallet Assemble.");
                }

                return response.json();
            })
            .then(function (result) {
                isSubmitting = false;
                if (!result || result.success === false) {
                    setMemberStatus(result && result.message ? result.message : "Unable to save Pallet Assemble.", "invalid");
                    showPopup(result && result.message ? result.message : "Unable to save Pallet Assemble.");
                    return;
                }

                setMemberStatus("", "");
                showPalletQrPopup(result.message || "Pallet Assemble Saved Successfully", result.palletCode || "", function () {
                    scannedMembers = [];
                    renderMembers();
                    refreshSelectedOrderDetails();
                });
            })
            .catch(function () {
                isSubmitting = false;
                setMemberStatus("Unable to save Pallet Assemble. Please try again.", "invalid");
                showPopup("Unable to save Pallet Assemble. Please try again.");
            });
    }

    document.addEventListener("change", function (event) {
        if (event.target === customerField) {
            loadOrders(event.target.value);
        }
    });

    document.addEventListener("input", function (event) {
        if (event.target === memberInput && memberInput.value) {
            memberInput.value = memberInput.value.toUpperCase();
            setMemberStatus("", "");
        }
    });

    document.addEventListener("click", function (event) {
        var orderRow = event.target.closest("[data-assemble-order-index]");
        if (orderRow) {
            showOrderDetails(parseInt(orderRow.getAttribute("data-assemble-order-index"), 10));
            return;
        }

        if (event.target.closest("[data-order-detail-close]")) {
            closeOrderDetails();
            return;
        }

        if (event.target.closest("[data-assemble-add]")) {
            addMember();
            return;
        }

        if (event.target.closest("[data-assemble-submit]")) {
            submitAssemble();
        }

        if (event.target.closest("[data-assemble-message-close]")) {
            closeMessagePopup();
            return;
        }

        if (event.target.closest("[data-assemble-bypass-cancel]")) {
            closeBypassModal();
            setMemberStatus("Member bypass cancelled.", "invalid");
        }

        if (event.target.closest("[data-assemble-bypass-continue]")) {
            continueBypass();
        }
    });

    document.addEventListener("keydown", function (event) {
        var orderRow = event.target.closest ? event.target.closest("[data-assemble-order-index]") : null;
        if (orderRow && (event.key === "Enter" || event.key === " ")) {
            event.preventDefault();
            showOrderDetails(parseInt(orderRow.getAttribute("data-assemble-order-index"), 10));
            return;
        }

        if (event.key === "Escape" && messageModal && !messageModal.hidden) {
            closeMessagePopup();
            return;
        }

        if (event.key === "Escape") {
            closeOrderDetails();
        }
    });

    renderMembers();
    if (customerField && customerField.value) {
        loadOrders(customerField.value);
    } else {
        renderOrders([], "Customer is not found in session.");
    }
}());
