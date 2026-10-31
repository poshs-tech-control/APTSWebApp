(function () {
    var stickers = [];
    var activeVisionStream = null;

    function bySelector(selector) {
        return document.querySelector(selector);
    }

    function getForm() {
        return bySelector(".apts-member-tagging-form");
    }

    function getFormUrl(attributeName) {
        var form = getForm();
        return form ? form.getAttribute(attributeName) : "";
    }

    function normalizeScan(value) {
        return (value || "").replace(/\s+/g, "").trim().toUpperCase();
    }

    function setValue(selector, value) {
        var input = bySelector(selector);
        if (!input) {
            return;
        }

        input.value = value || "";
        input.dispatchEvent(new Event("input", { bubbles: true }));
        input.dispatchEvent(new Event("change", { bubbles: true }));
    }

    function setStatus(state, message) {
        var status = bySelector("[data-member-tagging-status]");
        if (!status) {
            return;
        }

        status.textContent = message || "";
        status.classList.toggle("is-valid", state === "valid");
        status.classList.toggle("is-invalid", state === "invalid");
        status.classList.toggle("is-loading", state === "loading");
    }

    function showPopup(message) {
        if (window.AptsProcessPopup) {
            window.AptsProcessPopup.show(message || "Done");
        } else {
            setStatus("valid", message || "Done");
        }
    }

    function renderStickers() {
        var list = bySelector("[data-sticker-list]");
        if (!list) {
            return;
        }

        list.innerHTML = "";
        if (stickers.length === 0) {
            var empty = document.createElement("p");
            empty.setAttribute("data-empty-stickers", "");
            empty.textContent = "No sticker added";
            list.appendChild(empty);
            return;
        }

        stickers.forEach(function (sticker, index) {
            var row = document.createElement("div");
            row.className = "apts-tagging-table__row";
            row.innerHTML = '<span>' + (index + 1) + '</span><strong></strong>';
            row.querySelector("strong").textContent = sticker;
            list.appendChild(row);
        });
    }

    function resetForm() {
        var form = getForm();
        if (form) {
            form.reset();
        }

        stickers = [];
        setValue("#MemberCode", "");
        setValue("#OriginalRfid", "");
        setValue("#MemberPlate", "");
        setValue("#StickerCode", "");
        renderStickers();
        setStatus("", "");
    }

    function showMember() {
        var input = bySelector("#MemberPlate");
        var url = getFormUrl("data-show-member-url");
        var memberPlate = normalizeScan(input ? input.value : "");
        var button = bySelector("[data-show-member]");

        if (input) {
            input.value = memberPlate;
        }

        if (!memberPlate) {
            setStatus("invalid", "Please scan or enter Member Plate.");
            return;
        }

        if (!url || !window.fetch) {
            setStatus("invalid", "Unable to load member details.");
            return;
        }

        setValue("#MemberCode", "");
        setValue("#OriginalRfid", "");
        setStatus("loading", "Loading member details...");
        if (button) {
            button.disabled = true;
        }

        fetch(url + "?memberPlate=" + encodeURIComponent(memberPlate), {
            credentials: "same-origin",
            headers: { "Accept": "application/json" }
        })
            .then(function (response) {
                return response.ok ? response.json() : null;
            })
            .then(function (result) {
                if (!result || !result.success) {
                    setStatus("invalid", result && result.message ? result.message : "Member not found.");
                    return;
                }

                setValue("#MemberCode", result.memberCode);
                setValue("#OriginalRfid", result.rfid);
                setStatus("valid", "Member details loaded.");
            })
            .catch(function () {
                setStatus("invalid", "Unable to load member details.");
            })
            .then(function () {
                if (button) {
                    button.disabled = false;
                }
            });
    }

    function addSticker() {
        var input = bySelector("#StickerCode");
        var url = getFormUrl("data-validate-sticker-url");
        var stickerCode = normalizeScan(input ? input.value : "");
        var button = bySelector("[data-add-sticker]");

        if (input) {
            input.value = stickerCode;
        }

        if (!stickerCode) {
            setStatus("invalid", "Please scan or enter QR Sticker.");
            return;
        }

        if (!bySelector("#MemberCode") || !bySelector("#MemberCode").value) {
            setStatus("invalid", "Please scan member plate and get MemberCode.");
            return;
        }

        if (stickers.some(function (item) { return item.toUpperCase() === stickerCode.toUpperCase(); })) {
            setStatus("invalid", "Sticker already added in list.");
            return;
        }

        if (!url || !window.fetch) {
            setStatus("invalid", "Unable to validate sticker code.");
            return;
        }

        setStatus("loading", "Checking sticker code...");
        if (button) {
            button.disabled = true;
        }

        fetch(url + "?stickerCode=" + encodeURIComponent(stickerCode), {
            credentials: "same-origin",
            headers: { "Accept": "application/json" }
        })
            .then(function (response) {
                return response.ok ? response.json() : null;
            })
            .then(function (result) {
                if (!result || !result.success || !result.isValid) {
                    setStatus("invalid", result && result.message ? result.message : "Sticker Code is not valid.");
                    return;
                }

                stickers.push(result.stickerCode || stickerCode);
                setValue("#StickerCode", "");
                renderStickers();
                setStatus("valid", "Sticker added.");
            })
            .catch(function () {
                setStatus("invalid", "Unable to validate sticker code.");
            })
            .then(function () {
                if (button) {
                    button.disabled = false;
                }
            });
    }

    function saveTagging(form) {
        var url = getFormUrl("data-save-url");
        var submitButton = form ? form.querySelector("[type='submit']") : null;
        var body = new FormData();

        if (!url || !form || !window.fetch) {
            setStatus("invalid", "Unable to save member tagging.");
            return;
        }

        if (!bySelector("#MemberCode") || !bySelector("#MemberCode").value) {
            setStatus("invalid", "Please scan member plate and get MemberCode.");
            return;
        }

        if (stickers.length === 0) {
            setStatus("invalid", "Please add at least one QR Sticker.");
            return;
        }

        body.append("MemberCode", bySelector("#MemberCode").value);
        body.append("Rfid", bySelector("#OriginalRfid").value);
        stickers.forEach(function (sticker) {
            body.append("StickerCodes", sticker);
        });

        setStatus("loading", "Saving member tagging...");
        if (submitButton) {
            submitButton.disabled = true;
        }

        fetch(url, {
            method: "POST",
            credentials: "same-origin",
            body: body,
            headers: { "Accept": "application/json" }
        })
            .then(function (response) {
                return response.ok ? response.json() : null;
            })
            .then(function (result) {
                if (result && result.success) {
                    showPopup(result.message || "Member Added");
                    resetForm();
                    return;
                }

                setStatus("invalid", result && result.message ? result.message : "Unable to save member tagging.");
            })
            .catch(function () {
                setStatus("invalid", "Unable to save member tagging.");
            })
            .then(function () {
                if (submitButton) {
                    submitButton.disabled = false;
                }
            });
    }

    function stopVisionCamera() {
        if (activeVisionStream) {
            activeVisionStream.getTracks().forEach(function (track) {
                track.stop();
            });
            activeVisionStream = null;
        }
    }

    function closeVisionScanner() {
        var modal = bySelector("[data-vision-scanner]");
        stopVisionCamera();
        if (modal) {
            modal.hidden = true;
        }
    }

    function setVisionStatus(message) {
        var status = bySelector("[data-vision-status]");
        if (status) {
            status.textContent = message || "";
        }
    }

    function openVisionScanner() {
        var modal = bySelector("[data-vision-scanner]");
        var video = bySelector("[data-vision-video]");
        var textArea = bySelector("[data-vision-text]");

        if (!modal || !video || !navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
            setStatus("invalid", "Camera is not available on this device.");
            return;
        }

        modal.hidden = false;
        if (textArea) {
            textArea.hidden = true;
            textArea.value = "";
        }
        setVisionStatus("Opening camera...");

        navigator.mediaDevices.getUserMedia({ video: { facingMode: { ideal: "environment" } }, audio: false })
            .then(function (stream) {
                activeVisionStream = stream;
                video.srcObject = stream;
                video.play();
                setVisionStatus("Point the camera at the member plate.");
            })
            .catch(function () {
                closeVisionScanner();
                setStatus("invalid", "Unable to open camera.");
            });
    }

    function captureVisionText() {
        var endpoint = getFormUrl("data-vision-endpoint");
        var video = bySelector("[data-vision-video]");
        var canvas = bySelector("[data-vision-canvas]");
        var textArea = bySelector("[data-vision-text]");
        var captureButton = bySelector("[data-vision-capture]");

        if (!endpoint || !video || !canvas || !window.fetch) {
            setVisionStatus("Unable to read image.");
            return;
        }

        canvas.width = video.videoWidth || 1280;
        canvas.height = video.videoHeight || 720;
        canvas.getContext("2d").drawImage(video, 0, 0, canvas.width, canvas.height);
        setVisionStatus("Reading member plate...");
        if (captureButton) {
            captureButton.disabled = true;
        }

        fetch(endpoint, {
            method: "POST",
            credentials: "same-origin",
            headers: {
                "Accept": "application/json",
                "Content-Type": "application/json"
            },
            body: JSON.stringify({ imageData: canvas.toDataURL("image/jpeg", 0.88) })
        })
            .then(function (response) {
                return response.ok ? response.json() : null;
            })
            .then(function (result) {
                if (!result || !result.success) {
                    setVisionStatus(result && result.message ? result.message : "Unable to read member plate.");
                    return;
                }

                if (textArea) {
                    textArea.hidden = false;
                    textArea.value = result.text || "";
                }

                if (result.memberPlate) {
                    setValue("#MemberPlate", result.memberPlate);
                    closeVisionScanner();
                    showMember();
                    return;
                }

                setVisionStatus("Member plate not found in image.");
            })
            .catch(function () {
                setVisionStatus("Unable to read member plate.");
            })
            .then(function () {
                if (captureButton) {
                    captureButton.disabled = false;
                }
            });
    }

    document.addEventListener("click", function (event) {

        if (event.target.closest("[data-show-member]")) {
            showMember();
            return;
        }

        if (event.target.closest("[data-add-sticker]")) {
            addSticker();
            return;
        }

        if (event.target.closest("[data-member-tagging-cancel]")) {
            resetForm();
            return;
        }

        if (event.target.closest("[data-vision-open]")) {
            openVisionScanner();
            return;
        }

        if (event.target.closest("[data-vision-close]")) {
            closeVisionScanner();
            return;
        }

        if (event.target.closest("[data-vision-capture]")) {
            captureVisionText();
        }
    });

    document.addEventListener("input", function (event) {
        if (event.target && (event.target.matches("#MemberPlate") || event.target.matches("#StickerCode"))) {
            event.target.value = normalizeScan(event.target.value);
        }
    });

    document.addEventListener("apts:qr-scan", function (event) {
        if (event.target && event.target.matches("#StickerCode")) {
            addSticker();
        }
    });

    document.addEventListener("submit", function (event) {
        var form = event.target && event.target.closest(".apts-member-tagging-form");
        if (!form) {
            return;
        }

        event.preventDefault();
        saveTagging(form);
    });

    renderStickers();
}());


