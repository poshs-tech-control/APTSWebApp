(function () {
    function bySelector(selector, root) {
        return (root || document).querySelector(selector);
    }

    function setStatus(form, message, isError) {
        var status = bySelector("[data-query-status]", form);

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

    function getValue(item, field) {
        return item[field] || item[field.charAt(0).toLowerCase() + field.slice(1)] || "-";
    }

    function clearDetails(form) {
        form.querySelectorAll("[data-query-field]").forEach(function (field) {
            field.textContent = "-";
        });
    }

    function bindDetails(form, item) {
        form.querySelectorAll("[data-query-field]").forEach(function (field) {
            field.textContent = getValue(item || {}, field.getAttribute("data-query-field"));
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

    function loadQueryReport(form) {
        var input = bySelector("[data-query-rfid]", form);
        var rfid = input ? input.value.trim() : "";

        clearDetails(form);

        if (!rfid) {
            setStatus(form, "Please scan or enter member RFID.", true);
            return;
        }

        setStatus(form, "Loading report...", false);

        postFormJson(form.getAttribute("data-query-report-url"), { Rfid: rfid })
            .then(function (result) {
                if (!result.success) {
                    setStatus(form, result.message || "No data found for scanned member.", true);
                    clearDetails(form);
                    return;
                }

                bindDetails(form, result.item);
                setStatus(form, "Report loaded successfully.", false);
            })
            .catch(function () {
                setStatus(form, "Unable to load query report.", true);
                clearDetails(form);
            });
    }

    document.addEventListener("DOMContentLoaded", function () {
        var form = bySelector("[data-query-report-form]");
        var input = form ? bySelector("[data-query-rfid]", form) : null;

        if (!form || !input) {
            return;
        }

        form.addEventListener("submit", function (event) {
            event.preventDefault();
            loadQueryReport(form);
        });

        input.addEventListener("change", function () {
            if (input.value.trim()) {
                loadQueryReport(form);
            }
        });

        input.addEventListener("apts:qr-scan", function () {
            loadQueryReport(form);
        });

        form.addEventListener("reset", function () {
            window.setTimeout(function () {
                clearDetails(form);
                setStatus(form, "", false);
            }, 0);
        });
    });
}());
