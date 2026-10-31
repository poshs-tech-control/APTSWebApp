(function () {
    var lastReportRows = [];

    function bySelector(selector, root) {
        return (root || document).querySelector(selector);
    }

    function getOptionValue(option) {
        return option.Value || option.value || "";
    }

    function getOptionText(option) {
        return option.Text || option.text || "";
    }

    function createOptionButton(value, text, selected) {
        var button = document.createElement("button");
        button.type = "button";
        button.setAttribute("role", "option");
        button.setAttribute("data-value", value || "");
        button.setAttribute("aria-selected", selected ? "true" : "false");
        button.textContent = text || "";
        return button;
    }

    function setStatus(form, message, isError) {
        var status = bySelector("[data-member-transaction-status]", form);

        if (!status) {
            return;
        }

        var isLoading = /^Loading/i.test(message || "");
        status.textContent = isLoading ? "" : (message || "");
        status.classList.toggle("is-invalid", !!isError);
        status.classList.toggle("is-valid", !!message && !isError && !isLoading);
        status.classList.toggle("is-loading", isLoading);

        if (isError && message && window.AptsProcessPopup) {
            window.AptsProcessPopup.show(message);
        }
    }

    function getSelectedLocationType(form) {
        var selected = bySelector("[data-member-transaction-location-mode]:checked", form);
        return selected ? selected.value : "Client";
    }

    function getEmptyOptionText(locationType) {
        if (locationType === "Client") {
            return "No client found";
        }

        if (locationType === "Truck") {
            return "No truck found";
        }

        return "No location found";
    }

    function updateLocationTitle(form) {
        var title = bySelector("[data-member-transaction-location-title]", form);
        var locationType = getSelectedLocationType(form);

        if (!title) {
            return;
        }

        if (locationType === "Factory") {
            title.textContent = "Factory Location";
        } else if (locationType === "Warehouse") {
            title.textContent = "Warehouse Location";
        } else if (locationType === "Truck") {
            title.textContent = "Truck No";
        } else {
            title.textContent = "Client";
        }
    }

    function setSelectOptions(field, options, emptyText) {
        var input = field ? bySelector("[data-custom-select-value]", field) : null;
        var label = field ? bySelector("[data-custom-select-label]", field) : null;
        var menu = field ? bySelector("[data-custom-select-menu]", field) : null;
        var safeOptions = Array.isArray(options) ? options : [];

        if (!field || !input || !label || !menu) {
            return;
        }

        menu.innerHTML = "";

        if (!safeOptions.length) {
            input.value = "";
            label.textContent = emptyText;
            menu.appendChild(createOptionButton("", emptyText, true));
            input.dispatchEvent(new Event("change", { bubbles: true }));
            return;
        }

        safeOptions.forEach(function (option, index) {
            menu.appendChild(createOptionButton(getOptionValue(option), getOptionText(option), index === 0));
        });

        input.value = getOptionValue(safeOptions[0]);
        label.textContent = getOptionText(safeOptions[0]);
        input.dispatchEvent(new Event("change", { bubbles: true }));
    }

    function fetchJson(url) {
        return fetch(url, {
            credentials: "same-origin",
            headers: { "Accept": "application/json" }
        }).then(function (response) {
            if (!response.ok) {
                throw new Error("Request failed");
            }

            return response.json();
        });
    }

    function postFormJson(url, data) {
        var body = new URLSearchParams();

        Object.keys(data).forEach(function (key) {
            body.append(key, data[key] || "");
        });

        return fetch(url, {
            method: "POST",
            credentials: "same-origin",
            headers: {
                "Accept": "application/json",
                "Content-Type": "application/x-www-form-urlencoded; charset=UTF-8"
            },
            body: body.toString()
        }).then(function (response) {
            if (!response.ok) {
                throw new Error("Request failed");
            }

            return response.json();
        });
    }

    function getRowValue(row, property) {
        return row[property] || row[property.charAt(0).toLowerCase() + property.slice(1)] || "";
    }

    function escapeHtml(value) {
        return String(value || "")
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#39;");
    }

    function getReportLocationCode(form) {
        var locationInput = bySelector("[data-member-transaction-location]", form);
        var selectedLabel = bySelector("[data-member-transaction-location-field] [data-custom-select-label]", form);

        if (!locationInput || !locationInput.value) {
            return "";
        }

        return selectedLabel ? selectedLabel.textContent.trim() : "";
    }

    function clearReport(form) {
        var table = bySelector("[data-member-transaction-table]", form);
        var rows = bySelector("[data-member-transaction-rows]", form);

        lastReportRows = [];
        setExportState(form, false);

        if (rows) {
            rows.innerHTML = "";
        }

        if (table) {
            table.hidden = true;
        }
    }

    function setExportState(form, enabled) {
        var exportButton = bySelector("[data-member-transaction-export]", form);

        if (exportButton) {
            exportButton.disabled = !enabled;
        }
    }

    function renderReport(form, rows) {
        var table = bySelector("[data-member-transaction-table]", form);
        var rowContainer = bySelector("[data-member-transaction-rows]", form);
        var safeRows = Array.isArray(rows) ? rows : [];

        clearReport(form);
        lastReportRows = safeRows.slice(0);

        if (!rowContainer || !table) {
            return;
        }

        if (!safeRows.length) {
            setStatus(form, "No member transaction records found.", false);
            return;
        }

        safeRows.forEach(function (item) {
            var row = document.createElement("div");
            row.className = "apts-report-table__row";
            row.innerHTML = [
                "<span>" + escapeHtml(getRowValue(item, "FromLocation")) + "</span>",
                "<span>" + escapeHtml(getRowValue(item, "ToLocation")) + "</span>",
                "<span>" + escapeHtml(getRowValue(item, "Status")) + "</span>",
                "<span>" + escapeHtml(getRowValue(item, "TotalMember")) + "</span>"
            ].join("");
            rowContainer.appendChild(row);
        });

        table.hidden = false;
        setExportState(form, true);
        setStatus(form, "Report loaded successfully.", false);
    }

    function buildExcelTable(form) {
        var locationType = getSelectedLocationType(form);
        var locationLabel = bySelector("[data-member-transaction-location-field] [data-custom-select-label]", form);
        var fromDate = bySelector("#TransactionFromDate", form);
        var toDate = bySelector("#TransactionToDate", form);
        var rowsHtml = lastReportRows.map(function (item) {
            return "<tr>" +
                "<td>" + escapeHtml(getRowValue(item, "FromLocation")) + "</td>" +
                "<td>" + escapeHtml(getRowValue(item, "ToLocation")) + "</td>" +
                "<td>" + escapeHtml(getRowValue(item, "Status")) + "</td>" +
                "<td>" + escapeHtml(getRowValue(item, "TotalMember")) + "</td>" +
                "</tr>";
        }).join("");

        return "<html><head><meta charset='utf-8' /></head><body>" +
            "<table border='1'>" +
            "<tr><th colspan='4'>Member Transaction Report</th></tr>" +
            "<tr><td>Location Type</td><td colspan='3'>" + escapeHtml(locationType) + "</td></tr>" +
            "<tr><td>Location</td><td colspan='3'>" + escapeHtml(locationLabel ? locationLabel.textContent : "") + "</td></tr>" +
            "<tr><td>From Date</td><td>" + escapeHtml(fromDate ? fromDate.value : "") + "</td><td>To Date</td><td>" + escapeHtml(toDate ? toDate.value : "") + "</td></tr>" +
            "<tr><th>From</th><th>To</th><th>Status</th><th>Count</th></tr>" +
            rowsHtml +
            "</table>" +
            "</body></html>";
    }

    function downloadExcel(form) {
        var locationType = getSelectedLocationType(form);
        var fromDate = bySelector("#TransactionFromDate", form);
        var toDate = bySelector("#TransactionToDate", form);
        var fileName = [
            "MemberTransactionReport",
            locationType,
            fromDate && fromDate.value ? fromDate.value : "",
            toDate && toDate.value ? toDate.value : ""
        ].filter(Boolean).join("_") + ".xls";

        if (!lastReportRows.length) {
            setStatus(form, "No grid data available to download.", true);
            return;
        }

        var blob = new Blob([buildExcelTable(form)], { type: "application/vnd.ms-excel;charset=utf-8" });
        var link = document.createElement("a");
        link.href = URL.createObjectURL(blob);
        link.download = fileName;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        window.setTimeout(function () {
            URL.revokeObjectURL(link.href);
        }, 0);
    }

    function loadLocationOptions(form) {
        var locationType = getSelectedLocationType(form);
        var locationField = bySelector("[data-member-transaction-location-field]", form);
        var url = form.getAttribute("data-location-options-url") + "?stockLocation=" + encodeURIComponent(locationType);

        clearReport(form);
        updateLocationTitle(form);
        setStatus(form, "Loading locations...", false);

        fetchJson(url).then(function (result) {
            if (!result.success) {
                setSelectOptions(locationField, [], getEmptyOptionText(locationType));
                setStatus(form, result.message || "Unable to load locations.", true);
                return;
            }

            setSelectOptions(locationField, result.options, getEmptyOptionText(locationType));
            setStatus(form, "", false);
        }).catch(function () {
            setSelectOptions(locationField, [], getEmptyOptionText(locationType));
            setStatus(form, "Unable to load locations.", true);
        });
    }

    function submitReport(form) {
        var locationCode = getReportLocationCode(form);
        var fromDate = bySelector("#TransactionFromDate", form);
        var toDate = bySelector("#TransactionToDate", form);

        clearReport(form);

        if (!locationCode) {
            setStatus(form, "Please select location.", true);
            return;
        }

        if (!fromDate || !fromDate.value || !toDate || !toDate.value) {
            setStatus(form, "Please select from date and to date.", true);
            return;
        }

        if (fromDate.value > toDate.value) {
            setStatus(form, "From date cannot be greater than to date.", true);
            return;
        }

        setStatus(form, "Loading report...", false);

        postFormJson(form.getAttribute("data-report-url"), {
            LocationCode: locationCode,
            FromDate: fromDate.value,
            ToDate: toDate.value
        }).then(function (result) {
            if (!result.success) {
                setStatus(form, result.message || "Unable to load report.", true);
                return;
            }

            renderReport(form, result.rows);
        }).catch(function () {
            setStatus(form, "Unable to load member transaction report.", true);
        });
    }

    document.addEventListener("DOMContentLoaded", function () {
        var form = bySelector("[data-member-transaction-form]");

        if (!form) {
            return;
        }

        updateLocationTitle(form);

        form.addEventListener("submit", function (event) {
            event.preventDefault();
            submitReport(form);
        });

        var exportButton = bySelector("[data-member-transaction-export]", form);

        if (exportButton) {
            exportButton.addEventListener("click", function () {
                downloadExcel(form);
            });
        }

        form.addEventListener("change", function (event) {
            if (event.target.matches("[data-member-transaction-location-mode]")) {
                loadLocationOptions(form);
            }

            if (event.target.matches("[data-member-transaction-location], #TransactionFromDate, #TransactionToDate")) {
                clearReport(form);
                setStatus(form, "", false);
            }
        });
    });
}());
