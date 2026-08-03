(function () {
    function closeSelect(select) {
        var menu = select.querySelector("[data-custom-select-menu]");
        var button = select.querySelector("[data-custom-select-button]");

        if (menu) {
            menu.hidden = true;
        }

        if (button) {
            button.setAttribute("aria-expanded", "false");
        }

        select.classList.remove("is-open");

        var search = select.querySelector("[data-custom-select-search]");
        if (search) {
            search.value = "";
            select.querySelectorAll("[data-custom-select-menu] [role='option']").forEach(function (option) {
                option.hidden = false;
            });
        }
    }

    function closeAllSelects(except) {
        document.querySelectorAll("[data-custom-select]").forEach(function (select) {
            if (select !== except) {
                closeSelect(select);
            }
        });
    }

    function toggleSelect(button) {
        var select = button.closest("[data-custom-select]");
        var menu = select ? select.querySelector("[data-custom-select-menu]") : null;

        if (!select || !menu) {
            return;
        }

        var willOpen = menu.hidden;
        closeAllSelects(select);
        menu.hidden = !willOpen;
        button.setAttribute("aria-expanded", willOpen ? "true" : "false");
        select.classList.toggle("is-open", willOpen);

        if (willOpen) {
            var search = select.querySelector("[data-custom-select-search]");
            if (search) { window.setTimeout(function () { search.focus(); }, 0); }
        }
    }

    function chooseOption(option) {
        var select = option.closest("[data-custom-select]");
        var field = option.closest(".apts-custom-select-field");
        var valueInput = field ? field.querySelector("[data-custom-select-value]") : null;
        var label = select ? select.querySelector("[data-custom-select-label]") : null;
        var value = option.getAttribute("data-value") || option.textContent.trim();

        if (valueInput) {
            valueInput.value = value;
            valueInput.dispatchEvent(new Event("change", { bubbles: true }));
        }

        if (label) {
            label.textContent = option.textContent.trim();
        }

        if (select) {
            select.querySelectorAll("[role='option']").forEach(function (item) {
                item.setAttribute("aria-selected", item === option ? "true" : "false");
            });
            closeSelect(select);
        }
    }

    document.addEventListener("click", function (event) {
        var selectButton = event.target.closest("[data-custom-select-button]");
        if (selectButton) {
            toggleSelect(selectButton);
            return;
        }

        var option = event.target.closest("[data-custom-select-menu] [role='option']");
        if (option) {
            chooseOption(option);
            return;
        }

        if (!event.target.closest("[data-custom-select]")) {
            closeAllSelects();
        }
    });

    document.addEventListener("input", function (event) {
        if (!event.target.matches("[data-custom-select-search]")) {
            return;
        }

        var menu = event.target.closest("[data-custom-select-menu]");
        var query = event.target.value.trim().toLowerCase();
        if (!menu) {
            return;
        }

        menu.querySelectorAll("[role='option']").forEach(function (option) {
            var text = (option.getAttribute("data-location-title") || option.textContent || "") + " " +
                (option.getAttribute("data-location-code") || option.getAttribute("data-value") || "");
            option.hidden = text.toLowerCase().indexOf(query) === -1;
        });
    });
    document.addEventListener("keydown", function (event) {
        if (event.key === "Escape") {
            closeAllSelects();
        }
    });
}());
