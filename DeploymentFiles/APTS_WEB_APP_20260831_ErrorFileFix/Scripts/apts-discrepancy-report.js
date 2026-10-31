(function () {
    var lastReportRows = [];

    function bySelector(selector, root) {
        return (root || document).querySelector(selector);
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

    function setStatus(form, message, isError) {
        var status = bySelector("[data-discrepancy-status]", form);

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

    function getValue(item, property) {
        return item[property] || item[property.charAt(0).toLowerCase() + property.slice(1)] || "";
    }

    function escapeHtml(value) {
        return String(value || "")
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#39;");
    }

    function setExportState(form, enabled) {
        var exportButton = bySelector("[data-discrepancy-export]", form);

        if (exportButton) {
            exportButton.disabled = !enabled;
        }
    }

    function clearReport(form) {
        var rows = bySelector("[data-discrepancy-rows]", form);
        var damage = bySelector("[data-discrepancy-damage-count]", form);
        var loss = bySelector("[data-discrepancy-loss-count]", form);

        lastReportRows = [];
        setExportState(form, false);

        if (rows) {
            rows.innerHTML = "";
        }

        if (damage) {
            damage.textContent = "0";
        }

        if (loss) {
            loss.textContent = "0";
        }
    }

    function renderReport(form, result) {
        var rows = bySelector("[data-discrepancy-rows]", form);
        var damage = bySelector("[data-discrepancy-damage-count]", form);
        var loss = bySelector("[data-discrepancy-loss-count]", form);
        var safeResult = result || {};
        var safeRows = safeResult.Rows || safeResult.rows || [];

        clearReport(form);
        lastReportRows = safeRows.slice(0);

        if (damage) {
            damage.textContent = safeResult.DamageCount || safeResult.damageCount || 0;
        }

        if (loss) {
            loss.textContent = safeResult.LossCount || safeResult.lossCount || 0;
        }

        if (!rows) {
            return;
        }

        if (!safeRows.length) {
            setStatus(form, "No discrepancy records found.", false);
            return;
        }

        safeRows.forEach(function (item) {
            var row = document.createElement("div");
            row.className = "apts-report-table__row";
            row.innerHTML = [
                "<span>" + escapeHtml(getValue(item, "RFIDNo")) + "</span>",
                "<span>" + escapeHtml(getValue(item, "FromLocation")) + "</span>",
                "<span>" + escapeHtml(getValue(item, "ToLocation")) + "</span>",
                "<span>" + escapeHtml(getValue(item, "LastEntryBy")) + "</span>",
                "<span>" + escapeHtml(getValue(item, "ActionType")) + "</span>",
                "<span>" + escapeHtml(getValue(item, "Date")) + "</span>"
            ].join("");
            rows.appendChild(row);
        });

        setExportState(form, true);
        setStatus(form, "Report loaded successfully.", false);
    }

    function buildExcelTable(form) {
        var selectedType = bySelector("input[name='DiscrepancyType']:checked", form);
        var fromDate = bySelector("#DiscrepancyFromDate", form);
        var toDate = bySelector("#DiscrepancyToDate", form);
        var damage = bySelector("[data-discrepancy-damage-count]", form);
        var loss = bySelector("[data-discrepancy-loss-count]", form);
        var rowsHtml = lastReportRows.map(function (item) {
            return "<tr>" +
                "<td>" + escapeHtml(getValue(item, "RFIDNo")) + "</td>" +
                "<td>" + escapeHtml(getValue(item, "FromLocation")) + "</td>" +
                "<td>" + escapeHtml(getValue(item, "ToLocation")) + "</td>" +
                "<td>" + escapeHtml(getValue(item, "LastEntryBy")) + "</td>" +
                "<td>" + escapeHtml(getValue(item, "ActionType")) + "</td>" +
                "<td>" + escapeHtml(getValue(item, "Date")) + "</td>" +
                "</tr>";
        }).join("");

        return "<html><head><meta charset='utf-8' /></head><body>" +
            "<table border='1'>" +
            "<tr><th colspan='6'>Discrepancy Report</th></tr>" +
            "<tr><td>Type</td><td colspan='5'>" + escapeHtml(selectedType ? selectedType.value : "") + "</td></tr>" +
            "<tr><td>From Date</td><td colspan='2'>" + escapeHtml(fromDate ? fromDate.value : "") + "</td><td>To Date</td><td colspan='2'>" + escapeHtml(toDate ? toDate.value : "") + "</td></tr>" +
            "<tr><td>Damage</td><td>" + escapeHtml(damage ? damage.textContent : "0") + "</td><td>Loss</td><td colspan='3'>" + escapeHtml(loss ? loss.textContent : "0") + "</td></tr>" +
            "<tr><th>Member</th><th>From</th><th>To</th><th>EntryBy</th><th>Status</th><th>Date</th></tr>" +
            rowsHtml +
            "</table>" +
            "</body></html>";
    }

    function downloadExcel(form) {
        var selectedType = bySelector("input[name='DiscrepancyType']:checked", form);
        var fromDate = bySelector("#DiscrepancyFromDate", form);
        var toDate = bySelector("#DiscrepancyToDate", form);
        var fileName = [
            "DiscrepancyReport",
            selectedType ? selectedType.value : "Report",
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

    function loadReport(form) {
        var selectedType = bySelector("input[name='DiscrepancyType']:checked", form);
        var fromDate = bySelector("#DiscrepancyFromDate", form);
        var toDate = bySelector("#DiscrepancyToDate", form);

        clearReport(form);

        if (!selectedType) {
            setStatus(form, "Please select discrepancy type.", true);
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

        postFormJson(form.getAttribute("data-discrepancy-report-url"), {
            DiscrepancyType: selectedType.value,
            FromDate: fromDate.value,
            ToDate: toDate.value
        }).then(function (response) {
            if (!response.success) {
                setStatus(form, response.message || "Unable to load report.", true);
                return;
            }

            renderReport(form, response.result);
        }).catch(function () {
            setStatus(form, "Unable to load discrepancy report.", true);
        });
    }

    document.addEventListener("DOMContentLoaded", function () {
        var form = bySelector("[data-discrepancy-report-form]");

        if (!form) {
            return;
        }

        form.addEventListener("submit", function (event) {
            event.preventDefault();
            loadReport(form);
        });

        var exportButton = bySelector("[data-discrepancy-export]", form);

        if (exportButton) {
            exportButton.addEventListener("click", function () {
                downloadExcel(form);
            });
        }

        form.addEventListener("change", function (event) {
            if (event.target.matches("input[name='DiscrepancyType']")) {
                clearReport(form);
                setStatus(form, "", false);
            }
        });
    });
}());
