(function () {
    window.addEventListener("pageshow", function (event) {
        var navEntries = window.performance && window.performance.getEntriesByType
            ? window.performance.getEntriesByType("navigation")
            : [];
        var isBackForward = event.persisted || (navEntries.length && navEntries[0].type === "back_forward");

        if (isBackForward) {
            window.location.reload();
        }
    });
})();
