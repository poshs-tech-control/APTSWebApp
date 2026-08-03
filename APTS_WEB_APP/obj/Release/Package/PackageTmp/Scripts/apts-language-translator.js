(function (window, document) {
    "use strict";

    var cookieDays = 365;
    var supportedLanguages = ["en", "hi", "mr", "gu", "ta", "te", "kn", "ml"];
    var googleScriptUrl = "https://translate.google.com/translate_a/element.js?cb=aptsGoogleTranslateInit";
    var retryDelay = 250;
    var maxApplyRetries = 40;

    function getCookie(name) {
        var prefix = name + "=";
        var cookies = document.cookie ? document.cookie.split(";") : [];

        for (var index = 0; index < cookies.length; index += 1) {
            var cookie = cookies[index].trim();
            if (cookie.indexOf(prefix) === 0) {
                return decodeURIComponent(cookie.substring(prefix.length));
            }
        }

        return "";
    }

    function setCookie(name, value) {
        var expires = new Date();
        expires.setDate(expires.getDate() + cookieDays);
        document.cookie = name + "=" + encodeURIComponent(value) + "; expires=" + expires.toUTCString() + "; path=/";
    }

    function clearCookie(name) {
        document.cookie = name + "=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/";
    }

    function storageGet(name) {
        try {
            return window.localStorage ? window.localStorage.getItem(name) : "";
        } catch (ignore) {
            return "";
        }
    }

    function storageSet(name, value) {
        try {
            if (window.localStorage) {
                window.localStorage.setItem(name, value);
            }
        } catch (ignore) {
            return;
        }
    }

    function normalizeLanguage(languageCode) {
        languageCode = (languageCode || "").toLowerCase();
        return supportedLanguages.indexOf(languageCode) >= 0 ? languageCode : "en";
    }

    function selectedLanguage() {
        return normalizeLanguage(getCookie("APTS_LANGUAGE_CODE") || storageGet("APTS_LANGUAGE_CODE"));
    }

    function ensureGoogleCookie(languageCode) {
        languageCode = normalizeLanguage(languageCode);
        setCookie("APTS_LANGUAGE_CODE", languageCode);
        storageSet("APTS_LANGUAGE_CODE", languageCode);

        if (languageCode === "en") {
            clearCookie("googtrans");
            return;
        }

        setCookie("googtrans", "/en/" + languageCode);
    }

    function ensureTranslateContainer() {
        var container = document.getElementById("google_translate_element");

        if (!container) {
            container = document.createElement("div");
            container.id = "google_translate_element";
            container.className = "apts-google-translate";
            document.body.appendChild(container);
        }
    }

    function hideGoogleChrome() {
        var style = document.getElementById("apts-google-translate-style");

        if (!style) {
            style = document.createElement("style");
            style.id = "apts-google-translate-style";
            style.textContent = ".apts-google-translate{position:absolute!important;left:-9999px!important;top:-9999px!important;width:1px!important;height:1px!important;overflow:hidden!important;}iframe.goog-te-banner-frame,iframe.skiptranslate,.goog-te-banner-frame,.goog-te-balloon-frame,#goog-gt-tt{display:none!important;visibility:hidden!important;height:0!important;width:0!important;}body{top:0!important;position:static!important;}";
            document.head.appendChild(style);
        }
    }

    function removeGoogleBanner() {
        var banners = document.querySelectorAll("iframe.goog-te-banner-frame, iframe.skiptranslate");

        document.body.style.top = "0";
        document.body.style.position = "static";
        document.documentElement.style.marginTop = "0";

        for (var index = 0; index < banners.length; index += 1) {
            banners[index].style.display = "none";
            banners[index].style.visibility = "hidden";
            banners[index].style.height = "0";
            banners[index].style.width = "0";
        }
    }

    function dispatchChange(element) {
        if (typeof window.Event === "function") {
            element.dispatchEvent(new Event("change"));
            return;
        }

        var changeEvent = document.createEvent("HTMLEvents");
        changeEvent.initEvent("change", true, true);
        element.dispatchEvent(changeEvent);
    }

    function applyLanguage(languageCode, attempt) {
        var combo = document.querySelector(".goog-te-combo");

        if (!combo) {
            if ((attempt || 0) < maxApplyRetries) {
                window.setTimeout(function () {
                    applyLanguage(languageCode, (attempt || 0) + 1);
                }, retryDelay);
            }
            return;
        }

        if (combo.value !== languageCode) {
            combo.value = languageCode;
            dispatchChange(combo);
        }

        removeGoogleBanner();
    }

    function resetToEnglish() {
        clearCookie("googtrans");
        storageSet("APTS_LANGUAGE_CODE", "en");

        var combo = document.querySelector(".goog-te-combo");
        if (combo && combo.value) {
            combo.value = "";
            dispatchChange(combo);
        }
    }

    window.aptsGoogleTranslateInit = function () {
        var languageCode = selectedLanguage();

        if (!window.google || !window.google.translate) {
            return;
        }

        new window.google.translate.TranslateElement({
            pageLanguage: "en",
            includedLanguages: supportedLanguages.join(","),
            autoDisplay: false
        }, "google_translate_element");

        if (languageCode !== "en") {
            applyLanguage(languageCode, 0);
        }
    };

    function loadGoogleTranslate() {
        var languageCode = selectedLanguage();

        hideGoogleChrome();
        ensureGoogleCookie(languageCode);
        ensureTranslateContainer();
        removeGoogleBanner();
        window.setInterval(removeGoogleBanner, 500);

        if (languageCode === "en") {
            resetToEnglish();
            return;
        }

        if (document.querySelector("script[data-apts-google-translate]")) {
            applyLanguage(languageCode, 0);
            return;
        }

        var script = document.createElement("script");
        script.src = googleScriptUrl;
        script.async = true;
        script.setAttribute("data-apts-google-translate", "true");
        document.head.appendChild(script);
    }

    window.AptsLanguageTranslator = {
        setLanguage: function (languageCode) {
            languageCode = normalizeLanguage(languageCode);
            ensureGoogleCookie(languageCode);

            if (languageCode === "en") {
                resetToEnglish();
                return;
            }

            loadGoogleTranslate();
            applyLanguage(languageCode, 0);
        },
        getLanguage: selectedLanguage
    };

    document.addEventListener("change", function (event) {
        var target = event.target;

        if (target && target.name === "language" && target.tagName === "SELECT") {
            ensureGoogleCookie(target.value);
        }
    });

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", loadGoogleTranslate);
    } else {
        loadGoogleTranslate();
    }
}(window, document));
