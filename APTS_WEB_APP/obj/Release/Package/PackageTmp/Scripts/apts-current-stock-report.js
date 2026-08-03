(function () {
    function bySelector(selector, root) {
        return (root || document).querySelector(selector);
    }

    function getOptionValue(option) {
        return option.Value || option.value || "";
    }

    function getOptionText(option) {
        return option.Text || option.text || "";
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
            var value = getOptionValue(option);
            var text = getOptionText(option);
            menu.appendChild(createOptionButton(value, text, index === 0));
        });

        input.value = getOptionValue(safeOptions[0]);
        label.textContent = getOptionText(safeOptions[0]);
        input.dispatchEvent(new Event("change", { bubbles: true }));
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

    function getSelectedStockLocation(form) {
        var selected = bySelector("[data-stock-location-mode]:checked", form);
        return selected ? selected.value : "Client";
    }

    function updateClientTitle(form) {
        var title = bySelector("[data-client-select-title]", form);
        var stockLocation = getSelectedStockLocation(form);

        if (!title) {
            return;
        }

        if (stockLocation === "Factory") {
            title.textContent = "Factory Location";
        } else if (stockLocation === "Warehouse") {
            title.textContent = "Warehouse Location";
        } else if (stockLocation === "Truck") {
            title.textContent = "Truck No";
        } else {
            title.textContent = "Client";
        }
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

    function setStatus(form, message, isError) {
        var status = bySelector("[data-current-stock-status]", form);

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

    function getReportLocationCode(form) {
        var locationInput = bySelector("[data-current-stock-client]", form);
        var selectedLabel = bySelector("[data-client-select-field] [data-custom-select-label]", form);

        if (!locationInput || !locationInput.value) {
            return "";
        }

        return selectedLabel ? selectedLabel.textContent.trim() : "";
    }

    function clearReport(form) {
        var summary = bySelector("[data-current-stock-summary]", form);
        var table = bySelector("[data-current-stock-table]", form);
        var rowContainer = bySelector("[data-current-stock-rows]", form);

        if (summary) {
            summary.innerHTML = "";
            summary.hidden = true;
        }

        if (rowContainer) {
            rowContainer.innerHTML = "";
        }

        if (table) {
            table.hidden = true;
        }
    }

    function renderReport(form, rows) {
        var summary = bySelector("[data-current-stock-summary]", form);
        var table = bySelector("[data-current-stock-table]", form);
        var rowContainer = bySelector("[data-current-stock-rows]", form);
        var safeRows = Array.isArray(rows) ? rows : [];
        var totals = {};

        clearReport(form);

        if (!safeRows.length) {
            setStatus(form, "No stock found for selected criteria.", false);
            return;
        }

        safeRows.forEach(function (row) {
            var parameter = getRowValue(row, "ParameterName");
            var count = parseInt(getRowValue(row, "Count") || "0", 10);
            totals[parameter] = (totals[parameter] || 0) + count;

            var item = document.createElement("div");
            item.className = "apts-current-stock-row";
            item.innerHTML = [
                "<span>" + escapeHtml(parameter) + "</span>",
                "<span>" + escapeHtml(getRowValue(row, "MemberSize")) + "</span>",
                "<span>" + count + "</span>"
            ].join("");
            rowContainer.appendChild(item);
        });

        Object.keys(totals).forEach(function (parameter) {
            var item = document.createElement("span");
            item.innerHTML = escapeHtml(parameter) + ": <strong>" + totals[parameter] + "</strong>";
            summary.appendChild(item);
        });

        summary.hidden = false;
        table.hidden = false;
        setStatus(form, "Report loaded successfully.", false);
    }

    function escapeHtml(value) {
        return String(value || "")
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#39;");
    }

    function loadClientOptions(form) {
        var stockLocation = getSelectedStockLocation(form);
        var url = form.getAttribute("data-client-options-url") + "?stockLocation=" + encodeURIComponent(stockLocation);
        var clientField = bySelector("[data-client-select-field]", form);

        updateClientTitle(form);

        return fetchJson(url).then(function (result) {
            setSelectOptions(clientField, result.options, getEmptyOptionText(stockLocation));
            clearReport(form);
            setStatus(form, "", false);
        });
    }

    function getEmptyOptionText(stockLocation) {
        if (stockLocation === "Client") {
            return "No client found";
        }

        if (stockLocation === "Truck") {
            return "No truck found";
        }

        return "No location found";
    }

    function submitReport(form) {
        var locationCode = getReportLocationCode(form);
        var reportDate = bySelector("#ReportDate", form);

        clearReport(form);

        if (!locationCode) {
            setStatus(form, "Please select location.", true);
            return;
        }

        if (!reportDate || !reportDate.value) {
            setStatus(form, "Please select date.", true);
            return;
        }

        setStatus(form, "Loading report...", false);

        postFormJson(form.getAttribute("data-report-url"), {
            LocationCode: locationCode,
            ReportDate: reportDate.value
        }).then(function (result) {
            if (!result.success) {
                setStatus(form, result.message || "Unable to load report.", true);
                return;
            }

            renderReport(form, result.rows);
        }).catch(function () {
            setStatus(form, "Unable to load report.", true);
        });
    }

    document.addEventListener("DOMContentLoaded", function () {
        var form = bySelector("[data-current-stock-form]");

        if (!form) {
            return;
        }

        updateClientTitle(form);

        document.addEventListener("change", function (event) {
            if (event.target.matches("[data-stock-location-mode]")) {
                loadClientOptions(form);
            }

            if (event.target.matches("[data-current-stock-client]")) {
                clearReport(form);
                setStatus(form, "", false);
            }
        });

        form.addEventListener("submit", function (event) {
            event.preventDefault();
            submitReport(form);
        });
    });
}());
