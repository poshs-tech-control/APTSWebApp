(function () {
    document.addEventListener("DOMContentLoaded", function () {
        var form = document.querySelector("[data-client-batch-report-form]");
        if (!form) { return; }

        var input = form.querySelector("[data-client-batch-input]");
        var status = form.querySelector("[data-client-batch-status]");
        var count = form.querySelector("[data-client-batch-count]");
        var results = form.querySelector("[data-client-batch-results]");
        var submit = form.querySelector("[type='submit']");
        var requestVersion = 0;
        var pendingBatch = null;

        function setStatus(message, isError, isLoading) {
            status.textContent = isLoading ? "" : (message || "");
            status.classList.toggle("is-invalid", !!isError);
            status.classList.toggle("is-valid", !!message && !isError && !isLoading);
            status.classList.toggle("is-loading", !!isLoading);
            submit.disabled = !!isLoading;
            if (isError && message && window.AptsProcessPopup) {
                window.AptsProcessPopup.show(message);
            }
        }

        function clearDetails() {
            results.textContent = "";
            count.textContent = "Records: 0";
        }

        function bindDetails(rows) {
            clearDetails();
            rows.forEach(function (item, index) {
                var card = document.createElement("section");
                card.className = "apts-query-detail-table";
                card.setAttribute("aria-label", "Member " + (index + 1));
                [["Member ID", "RFID"], ["Entry By", "LastEntryBy"],
                    ["Date", "LastUpdatedDate"], ["Transfer To", "LastTransferTo"],
                    ["Last Status", "LastStatus"]].forEach(function (field) {
                    var row = document.createElement("div");
                    row.className = "apts-query-detail-row";
                    var label = document.createElement("strong");
                    label.textContent = field[0];
                    var value = document.createElement("span");
                    value.textContent = item[field[1]] == null || item[field[1]] === "" ? "-" : String(item[field[1]]);
                    row.appendChild(label);
                    row.appendChild(value);
                    card.appendChild(row);
                });
                results.appendChild(card);
            });
            count.textContent = "Records: " + rows.length;
        }

        function loadReport() {
            var batch = input.value.trim();
            // QR scanning emits both change and apts:qr-scan events.
            if (pendingBatch === batch) { return; }
            var version = ++requestVersion;
            pendingBatch = null;
            clearDetails();
            if (!batch || batch.length > 50) {
                setStatus(!batch ? "Please scan or enter Client Batch No." : "Client Batch No cannot exceed 50 characters.", true, false);
                return;
            }

            pendingBatch = batch;
            setStatus("Loading report...", false, true);
            var body = new URLSearchParams();
            body.append("ClientBatchNo", batch);
            return fetch(form.getAttribute("data-client-batch-report-url"), {
                method: "POST",
                credentials: "same-origin",
                headers: { "Accept": "application/json", "Content-Type": "application/x-www-form-urlencoded; charset=UTF-8" },
                body: body.toString()
            }).then(function (response) {
                if (!response.ok) { throw new Error("Request failed"); }
                return response.json();
            }).then(function (result) {
                if (version !== requestVersion) { return; }
                pendingBatch = null;
                if (!result || !result.success) {
                    setStatus(result && result.message ? result.message : "Unable to load Members Details By ClientBatchNo report. Please try again.", true, false);
                    return;
                }
                var rows = Array.isArray(result.rows) ? result.rows : [];
                bindDetails(rows);
                setStatus(rows.length ? "Report loaded successfully." : "No members found for Client Batch No.", false, false);
            }).catch(function () {
                if (version !== requestVersion) { return; }
                pendingBatch = null;
                clearDetails();
                setStatus("Unable to load Members Details By ClientBatchNo report. Please try again.", true, false);
            });
        }

        form.addEventListener("submit", function (event) { event.preventDefault(); loadReport(); });
        input.addEventListener("change", loadReport);
        input.addEventListener("apts:qr-scan", loadReport);
        form.addEventListener("reset", function () {
            ++requestVersion;
            pendingBatch = null;
            clearDetails();
            setStatus("", false, false);
        });
    });
}());
