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
        var status = bySelector("[data-high-age-status]", form);

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

    function getSelectedHighAgeType(form) {
        var selected = bySelector("[data-high-age-type]:checked", form);
        return selected ? selected.value : "Client";
    }

    function getEmptyOptionText(reportType) {
        return reportType === "Customer" ? "No customer location found" : "No client found";
    }

    function updateLocationTitle(form) {
        var title = bySelector("[data-high-age-location-title]", form);
        var reportType = getSelectedHighAgeType(form);

        if (title) {
            title.textContent = reportType === "Customer" ? "Customer" : "Client";
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

    function clearReport(form) {
        var count = bySelector("[data-high-age-count]", form);
        var table = bySelector("[data-high-age-table]", form);
        var rows = bySelector("[data-high-age-rows]", form);

        lastReportRows = [];
        setExportState(form, false);

        if (count) {
            count.textContent = "0";
        }

        if (rows) {
            rows.innerHTML = "";
        }

        if (table) {
            table.hidden = true;
        }
    }

    function setExportState(form, enabled) {
        var exportButton = bySelector("[data-high-age-export]", form);

        if (exportButton) {
            exportButton.disabled = !enabled;
        }
    }

    function setHeader(form, reportType) {
        var table = bySelector("[data-high-age-table]", form);
        var head = bySelector("[data-high-age-head]", form);
        var headers = reportType === "Customer"
            ? ["ClientBatchNo", "From", "To", "DispatchDate", "Members", "Status", "Age"]
            : ["LocationTitle", "PreDispatchdate", "TotalMember", "PreDispatch", "Age"];

        if (table) {
            table.classList.toggle("apts-report-table-five", reportType !== "Customer");
            table.classList.toggle("apts-report-table-seven", reportType === "Customer");
        }

        if (head) {
            head.innerHTML = headers.map(function (header) {
                return "<span>" + escapeHtml(header) + "</span>";
            }).join("");
        }
    }

    function appendRow(container, cells) {
        var row = document.createElement("div");
        row.className = "apts-report-table__row";
        row.innerHTML = cells.map(function (cell) {
            return "<span>" + escapeHtml(cell) + "</span>";
        }).join("");
        container.appendChild(row);
    }

    function renderReport(form, result) {
        var reportType = getSelectedHighAgeType(form);
        var count = bySelector("[data-high-age-count]", form);
        var table = bySelector("[data-high-age-table]", form);
        var rows = bySelector("[data-high-age-rows]", form);
        var safeResult = result || {};
        var safeRows = safeResult.Rows || safeResult.rows || [];

        clearReport(form);
        setHeader(form, reportType);
        lastReportRows = safeRows.slice(0);

        if (count) {
            count.textContent = safeResult.Count || safeResult.count || 0;
        }

        if (!rows || !table) {
            return;
        }

        if (!safeRows.length) {
            setStatus(form, "No high age pallet records found.", false);
            return;
        }

        safeRows.forEach(function (row) {
            if (reportType === "Customer") {
                appendRow(rows, [
                    getRowValue(row, "ClientBatchNo"),
                    getRowValue(row, "FromLocation"),
                    getRowValue(row, "ToLocation"),
                    getRowValue(row, "DispatchDate"),
                    getRowValue(row, "Members"),
                    getRowValue(row, "Status"),
                    getRowValue(row, "Ageing")
                ]);
                return;
            }

            appendRow(rows, [
                getRowValue(row, "LocationTitle"),
                getRowValue(row, "PreDispatchDate"),
                getRowValue(row, "TotalMember"),
                getRowValue(row, "Status"),
                getRowValue(row, "Ageing")
            ]);
        });

        table.hidden = false;
        setExportState(form, true);
        setStatus(form, "Report loaded successfully.", false);
    }

    function getExportHeaders(reportType) {
        return reportType === "Customer"
            ? ["ClientBatchNo", "From", "To", "DispatchDate", "Members", "Status", "Age"]
            : ["LocationTitle", "PreDispatchdate", "TotalMember", "PreDispatch", "Age"];
    }

    function getExportCells(row, reportType) {
        if (reportType === "Customer") {
            return [
                getRowValue(row, "ClientBatchNo"),
                getRowValue(row, "FromLocation"),
                getRowValue(row, "ToLocation"),
                getRowValue(row, "DispatchDate"),
                getRowValue(row, "Members"),
                getRowValue(row, "Status"),
                getRowValue(row, "Ageing")
            ];
        }

        return [
            getRowValue(row, "LocationTitle"),
            getRowValue(row, "PreDispatchDate"),
            getRowValue(row, "TotalMember"),
            getRowValue(row, "Status"),
            getRowValue(row, "Ageing")
        ];
    }

    function buildExcelTable(form) {
        var reportType = getSelectedHighAgeType(form);
        var locationLabel = bySelector("[data-high-age-location-field] [data-custom-select-label]", form);
        var totalDays = bySelector("#TotalDays", form);
        var count = bySelector("[data-high-age-count]", form);
        var headers = getExportHeaders(reportType);
        var headerHtml = headers.map(function (header) {
            return "<th>" + escapeHtml(header) + "</th>";
        }).join("");
        var rowsHtml = lastReportRows.map(function (row) {
            return "<tr>" + getExportCells(row, reportType).map(function (cell) {
                return "<td>" + escapeHtml(cell) + "</td>";
            }).join("") + "</tr>";
        }).join("");

        return "<html><head><meta charset='utf-8' /></head><body>" +
            "<table border='1'>" +
            "<tr><th colspan='" + headers.length + "'>High Age Pallet Report</th></tr>" +
            "<tr><td>Type</td><td colspan='" + (headers.length - 1) + "'>" + escapeHtml(reportType) + "</td></tr>" +
            "<tr><td>Location</td><td colspan='" + (headers.length - 1) + "'>" + escapeHtml(locationLabel ? locationLabel.textContent : "") + "</td></tr>" +
            "<tr><td>Total Days</td><td>" + escapeHtml(totalDays ? totalDays.value : "") + "</td><td>Count</td><td colspan='" + Math.max(headers.length - 3, 1) + "'>" + escapeHtml(count ? count.textContent : "0") + "</td></tr>" +
            "<tr>" + headerHtml + "</tr>" +
            rowsHtml +
            "</table>" +
            "</body></html>";
    }

    function downloadExcel(form) {
        var reportType = getSelectedHighAgeType(form);
        var totalDays = bySelector("#TotalDays", form);
        var fileName = [
            "HighAgePalletReport",
            reportType,
            totalDays && totalDays.value ? totalDays.value + "Days" : ""
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
        var reportType = getSelectedHighAgeType(form);
        var url = form.getAttribute("data-location-options-url") + "?highAgeType=" + encodeURIComponent(reportType);
        var locationField = bySelector("[data-high-age-location-field]", form);

        clearReport(form);
        updateLocationTitle(form);
        setStatus(form, "Loading locations...", false);

        fetchJson(url).then(function (result) {
            if (!result.success) {
                setSelectOptions(locationField, [], getEmptyOptionText(reportType));
                setStatus(form, result.message || "Unable to load locations.", true);
                return;
            }

            setSelectOptions(locationField, result.options, getEmptyOptionText(reportType));
            setStatus(form, "", false);
        }).catch(function () {
            setSelectOptions(locationField, [], getEmptyOptionText(reportType));
            setStatus(form, "Unable to load locations.", true);
        });
    }

    function submitReport(form) {
        var reportType = getSelectedHighAgeType(form);
        var location = bySelector("#HighAgeLocation", form);
        var totalDays = bySelector("#TotalDays", form);

        clearReport(form);
        setHeader(form, reportType);

        if (!location || !location.value) {
            setStatus(form, "Please select location.", true);
            return;
        }

        if (!totalDays || totalDays.value === "" || parseInt(totalDays.value, 10) < 0) {
            setStatus(form, "Please enter valid total days.", true);
            return;
        }

        setStatus(form, "Loading report...", false);

        postFormJson(form.getAttribute("data-report-url"), {
            HighAgeType: reportType,
            LocationCode: location.value,
            TotalDays: totalDays.value
        }).then(function (response) {
            if (!response.success) {
                setStatus(form, response.message || "Unable to load report.", true);
                return;
            }

            renderReport(form, response.result);
        }).catch(function () {
            setStatus(form, "Unable to load high age report.", true);
        });
    }

    document.addEventListener("DOMContentLoaded", function () {
        var form = bySelector("[data-high-age-form]");

        if (!form) {
            return;
        }

        updateLocationTitle(form);
        setHeader(form, getSelectedHighAgeType(form));

        form.addEventListener("submit", function (event) {
            event.preventDefault();
            submitReport(form);
        });

        var exportButton = bySelector("[data-high-age-export]", form);

        if (exportButton) {
            exportButton.addEventListener("click", function () {
                downloadExcel(form);
            });
        }

        form.addEventListener("change", function (event) {
            if (event.target.matches("[data-high-age-type]")) {
                loadLocationOptions(form);
            }

            if (event.target.matches("#HighAgeLocation, #TotalDays")) {
                clearReport(form);
                setStatus(form, "", false);
            }
        });
    });
}());
