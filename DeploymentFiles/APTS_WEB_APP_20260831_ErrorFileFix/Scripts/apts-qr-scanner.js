(function () {
    var activeStream = null;
    var activeTarget = null;
    var detector = null;
    var frameHandle = null;
    var fallbackCanvas = document.createElement("canvas");
    var fallbackContext = fallbackCanvas.getContext("2d");
    var fallbackDecoderLoading = null;
    var scannerScriptUrl = document.currentScript ? document.currentScript.src : "";
    var fallbackDecoderUrl = scannerScriptUrl
        ? scannerScriptUrl.replace(/apts-qr-scanner\.js(?:\?.*)?$/, "jsQR.js")
        : "/Scripts/jsQR.js";
    var autoAddSelectors = [
        { input: "[data-receipt-member-input]", action: "[data-receipt-add]" },
        { input: "[data-collection-transfer-input]", action: "[data-collection-transfer-add]" },
        { input: "[data-transfer-member-input]", action: "[data-transfer-summary]" },
        { input: "[data-disassemble-member-input]", action: "[data-disassemble-add]" },
        { input: "[data-disassemble-customer-input]", action: "[data-disassemble-customer-add]" },
        { input: "[data-predispatch-member-input]", action: "[data-predispatch-add]" },
        { input: "[data-member-scan-input]", action: "[data-member-add]" }
    ];

    function findElements() {
        return {
            modal: document.querySelector("[data-qr-scanner]"),
            video: document.querySelector("[data-qr-video]"),
            status: document.querySelector("[data-qr-status]")
        };
    }

    function setStatus(text) {
        var elements = findElements();
        if (elements.status) {
            elements.status.textContent = text;
        }
    }

    function stopScanner() {
        if (frameHandle) {
            cancelAnimationFrame(frameHandle);
            frameHandle = null;
        }

        if (activeStream) {
            activeStream.getTracks().forEach(function (track) {
                track.stop();
            });
            activeStream = null;
        }

        var elements = findElements();
        if (elements.video) {
            elements.video.srcObject = null;
        }
        if (elements.modal) {
            elements.modal.hidden = true;
        }

        activeTarget = null;
    }

    function writeScanValue(value) {
        if (!activeTarget || !value) {
            return;
        }

        activeTarget.value = value;
        activeTarget.dispatchEvent(new Event("input", { bubbles: true }));
        activeTarget.dispatchEvent(new Event("change", { bubbles: true }));
        activeTarget.dispatchEvent(new CustomEvent("apts:qr-scan", {
            bubbles: true,
            detail: { value: value }
        }));
        autoAddScannedMember(activeTarget);
        stopScanner();
    }

    function autoAddScannedMember(target) {
        var match = autoAddSelectors.filter(function (item) {
            return target.matches(item.input);
        })[0];
        var action = match ? document.querySelector(match.action) : null;

        if (action) {
            action.click();
        }
    }

    function ensureFallbackDecoder() {
        if (window.jsQR) {
            return Promise.resolve();
        }

        if (fallbackDecoderLoading) {
            return fallbackDecoderLoading;
        }

        fallbackDecoderLoading = new Promise(function (resolve, reject) {
            var script = document.createElement("script");
            script.src = fallbackDecoderUrl;
            script.async = true;
            script.onload = function () {
                if (window.jsQR) {
                    resolve();
                    return;
                }
                reject(new Error("QR decoder did not load."));
            };
            script.onerror = function () {
                reject(new Error("QR decoder could not be loaded."));
            };
            document.head.appendChild(script);
        });

        return fallbackDecoderLoading;
    }

    function detectWithFallback(video) {
        if (!window.jsQR || !fallbackContext || !video.videoWidth || !video.videoHeight) {
            return null;
        }

        fallbackCanvas.width = video.videoWidth;
        fallbackCanvas.height = video.videoHeight;
        fallbackContext.drawImage(video, 0, 0, fallbackCanvas.width, fallbackCanvas.height);

        var imageData = fallbackContext.getImageData(0, 0, fallbackCanvas.width, fallbackCanvas.height);
        var code = window.jsQR(imageData.data, imageData.width, imageData.height, {
            inversionAttempts: "attemptBoth"
        });

        return code ? code.data : null;
    }

    async function scanFrame() {
        var elements = findElements();
        if (!elements.video || elements.video.readyState < 2) {
            frameHandle = requestAnimationFrame(scanFrame);
            return;
        }

        try {
            if (detector) {
                var codes = await detector.detect(elements.video);
                if (codes && codes.length > 0) {
                    writeScanValue(codes[0].rawValue);
                    return;
                }
            } else {
                var value = detectWithFallback(elements.video);
                if (value) {
                    writeScanValue(value);
                    return;
                }
            }
        } catch (error) {
            if (!detector) {
                setStatus("Unable to read this frame. Keep the QR code inside the box.");
            } else {
                setStatus("Unable to read this frame. Hold the QR code steady.");
            }
        }

        frameHandle = requestAnimationFrame(scanFrame);
    }

    async function openScanner(targetSelector) {
        var elements = findElements();
        activeTarget = document.querySelector(targetSelector);

        if (!elements.modal || !elements.video || !activeTarget) {
            return;
        }

        if (!navigator.mediaDevices) {
            elements.modal.hidden = false;
            setStatus("Camera scanning is not available in this browser. Please use HTTPS or localhost.");
            return;
        }

        try {
            elements.modal.hidden = false;
            if ("BarcodeDetector" in window) {
                detector = detector || new BarcodeDetector({ formats: ["qr_code"] });
            } else {
                detector = null;
                setStatus("Loading QR scanner...");
                await ensureFallbackDecoder();
            }
            setStatus("Opening camera...");
            activeStream = await navigator.mediaDevices.getUserMedia({
                video: { facingMode: { ideal: "environment" } },
                audio: false
            });
            elements.video.srcObject = activeStream;
            await elements.video.play();
            setStatus("Point the camera at the QR code.");
            frameHandle = requestAnimationFrame(scanFrame);
        } catch (error) {
            elements.modal.hidden = false;
            setStatus("Camera permission is required to scan QR codes, and the QR scanner must be available.");
        }
    }

    document.addEventListener("click", function (event) {
        var scanButton = event.target.closest("[data-scan-target]");
        if (scanButton) {
            openScanner(scanButton.getAttribute("data-scan-target"));
            return;
        }

        if (event.target.closest("[data-qr-close]")) {
            stopScanner();
        }
    });

    document.addEventListener("keydown", function (event) {
        if (event.key === "Escape") {
            stopScanner();
        }
    });

    window.AptsQrScanner = {
        open: openScanner,
        close: stopScanner
    };
}());
