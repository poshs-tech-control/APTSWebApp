(function () {
    var activeStream = null;
    var captureCanvas = document.createElement("canvas");

    function bySelector(selector) {
        return document.querySelector(selector);
    }

    function getFormUrl(attributeName) {
        var form = bySelector("[data-predispatch-form]");
        return form ? form.getAttribute(attributeName) : "";
    }

    function setMemberStatus(state, message) {
        var status = bySelector("[data-predispatch-member-status]");
        var input = bySelector("[data-predispatch-member-input]");

        if (input) {
            input.classList.toggle("is-valid", state === "valid");
            input.classList.toggle("is-invalid", state === "invalid");
        }

        if (status) {
            status.textContent = message || "";
            status.classList.toggle("is-valid", state === "valid");
            status.classList.toggle("is-invalid", state === "invalid");
        }
    }

    function setPalletCode(value) {
        var palletCode = bySelector("[data-predispatch-pallet-code]");
        var palletCodeValue = bySelector("[data-predispatch-pallet-code-value]");
        if (palletCode) {
            palletCode.textContent = value || "";
        }
        if (palletCodeValue) {
            palletCodeValue.value = value || "";
        }
    }

    function setPalletDimensions(width, length1, length2) {
        var values = {
            "#SheetWidth": width || "",
            "#SheetLengthH1": length1 || "",
            "#SheetLengthH2": length2 || ""
        };

        Object.keys(values).forEach(function (selector) {
            var input = bySelector(selector);
            if (input) {
                input.value = values[selector];
            }
        });
    }
    function setSubmitStatus(state, message) {
        var status = bySelector("[data-predispatch-submit-status]");
        if (!status) {
            return;
        }

        status.textContent = message || "";
        status.classList.toggle("is-valid", state === "valid");
        status.classList.toggle("is-invalid", state === "invalid");
        status.classList.toggle("is-loading", state === "loading");
    }

    function showProcessMessage(message, variant) {
        if (window.AptsProcessPopup) {
            window.AptsProcessPopup.show(message, variant || "success");
            return;
        }

        setSubmitStatus(variant === "success" ? "valid" : "invalid", message);
    }

    function setVisionStatus(text) {
        var status = bySelector("[data-vision-status]");
        if (status) {
            status.textContent = text;
        }
    }

    function stopCamera() {
        if (activeStream) {
            activeStream.getTracks().forEach(function (track) {
                track.stop();
            });
            activeStream = null;
        }

        var video = bySelector("[data-vision-video]");
        if (video) {
            video.srcObject = null;
        }

        var modal = bySelector("[data-vision-scanner]");
        if (modal) {
            modal.hidden = true;
        }
    }

    async function openCamera() {
        var modal = bySelector("[data-vision-scanner]");
        var video = bySelector("[data-vision-video]");
        if (!modal || !video) {
            return;
        }

        modal.hidden = false;

        if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
            setVisionStatus("Camera is not available in this browser. Please use HTTPS or localhost.");
            return;
        }

        try {
            setVisionStatus("Opening camera...");
            activeStream = await navigator.mediaDevices.getUserMedia({
                video: { facingMode: { ideal: "environment" } },
                audio: false
            });
            video.srcObject = activeStream;
            await video.play();
            setVisionStatus("Position the pallet text clearly, then capture.");
        } catch (error) {
            setVisionStatus("Camera permission is required to read text.");
        }
    }

    function captureImage() {
        var video = bySelector("[data-vision-video]");
        if (!video || !video.videoWidth || !video.videoHeight) {
            return "";
        }

        var maxSize = 1280;
        var scale = Math.min(1, maxSize / Math.max(video.videoWidth, video.videoHeight));
        captureCanvas.width = Math.round(video.videoWidth * scale);
        captureCanvas.height = Math.round(video.videoHeight * scale);
        captureCanvas.getContext("2d").drawImage(video, 0, 0, captureCanvas.width, captureCanvas.height);

        return captureCanvas.toDataURL("image/jpeg", 0.85);
    }

    async function readTextFromCamera() {
        var modal = bySelector("[data-vision-scanner]");
        var imageData = captureImage();
        var endpoint = modal ? modal.getAttribute("data-vision-endpoint") : "";

        if (!imageData || !endpoint) {
            setVisionStatus("Camera image could not be captured.");
            return;
        }

        try {
            setVisionStatus("Reading text with Google Vision...");
            var response = await fetch(endpoint, {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ imageData: imageData })
            });
            var result = await response.json();

            if (!result.success) {
                setVisionStatus(result.message || "Google Vision could not read this image.");
                return;
            }

            applyVisionResult(result);
            setVisionStatus("Text captured and fields updated.");
            validateExistingPreDispatch({ showSuccess: false });
            setTimeout(stopCamera, 900);
        } catch (error) {
            setVisionStatus("Unable to call Google Vision. Check network and API key.");
        }
    }

    function applyVisionResult(result) {
        var textArea = bySelector("[data-vision-text]");
        if (textArea) {
            textArea.hidden = false;
            textArea.value = result.text || "";
        }

        setValue("#ClientBatchNo", result.fields && result.fields.clientBatchNo);
        setValue("#SheetLengthH1", result.fields && result.fields.sheetLengthH1);
        setValue("#SheetLengthH2", result.fields && result.fields.sheetLengthH2);
        setValue("#SheetWidth", result.fields && result.fields.sheetWidth);
        setValue("#Thickness", result.fields && result.fields.thickness);
        setValue("#Weight", result.fields && result.fields.weight);
    }

    function setValue(selector, value) {
        var input = bySelector(selector);
        if (!input || !value) {
            return;
        }

        input.value = value;
        input.dispatchEvent(new Event("input", { bubbles: true }));
        input.dispatchEvent(new Event("change", { bubbles: true }));
    }

    function resetPreDispatchForm(form) {
        if (form) {
            form.reset();
        }

        setPalletCode("");
        setMemberStatus("", "");
        setSubmitStatus("", "");

        var textArea = bySelector("[data-vision-text]");
        if (textArea) {
            textArea.hidden = true;
            textArea.value = "";
        }

        var label = bySelector("[data-upload-label]");
        if (label) {
            label.textContent = "UPLOAD";
        }
    }

    async function validateMember() {
        var input = bySelector("[data-predispatch-member-input]");
        var url = getFormUrl("data-member-validation-url");
        var rfid = input ? input.value.trim() : "";

        setPalletCode("");
        setPalletDimensions("", "", "");

        if (!rfid) {
            setMemberStatus("invalid", "");
            showProcessMessage("Please scan member.", "error");
            return;
        }

        if (!url || !window.fetch) {
            setMemberStatus("invalid", "");
            showProcessMessage("Unable to validate member.", "error");
            return;
        }

        setMemberStatus("", "Validating member...");

        try {
            var response = await fetch(url + "?rfid=" + encodeURIComponent(rfid), {
                credentials: "same-origin",
                headers: { "Accept": "application/json" }
            });
            var result = response.ok ? await response.json() : null;

            if (result && result.success) {
                var successMessage = result.message || "Member validated successfully.";
                setMemberStatus("valid", "");
                setPalletCode(result.palletCode || "");
                setPalletDimensions(result.width, result.length1, result.length2);
                showProcessMessage(successMessage);
                return;
            }

            setMemberStatus("invalid", "");
            showProcessMessage(result && result.message ? result.message : "Member validation failed.", "error");
        } catch (error) {
            setMemberStatus("invalid", "");
            showProcessMessage("Unable to validate member.", "error");
        }
    }

    async function validateExistingPreDispatch(options) {
        options = options || {};
        var showSuccess = options.showSuccess !== false;
        var url = getFormUrl("data-existing-validation-url");
        var palletCodeInput = bySelector("[data-predispatch-pallet-code-value]");
        var clientBatchInput = bySelector("#ClientBatchNo");
        var palletCode = palletCodeInput ? palletCodeInput.value.trim() : "";
        var clientBatchNo = clientBatchInput ? clientBatchInput.value.trim() : "";

        if (!palletCode && !clientBatchNo) {
            setSubmitStatus("invalid", "");
            showProcessMessage("Please validate member or enter Client Batch No.", "error");
            return false;
        }

        if (!url || !window.fetch) {
            setSubmitStatus("invalid", "");
            showProcessMessage("Unable to validate Pallet Pre-Dispatch.", "error");
            return false;
        }

        setSubmitStatus("", "");

        try {
            var response = await fetch(url + "?palletCode=" + encodeURIComponent(palletCode) + "&clientBatchNo=" + encodeURIComponent(clientBatchNo), {
                credentials: "same-origin",
                headers: { "Accept": "application/json" }
            });
            var result = response.ok ? await response.json() : null;

            if (result && result.success && result.exists) {
                var item = result.item || {};
                var message = result.message || "Pallet Pre-Dispatch already exists.";
                if (item.PalletPreDispatchCode || item.palletPreDispatchCode) {
                    message += " Code: " + (item.PalletPreDispatchCode || item.palletPreDispatchCode);
                }
                setSubmitStatus("invalid", "");
                showProcessMessage(message, "error");
                return false;
            }

            if (result && result.success) {
                setSubmitStatus("valid", "");
                return true;
            }

            setSubmitStatus("invalid", "");
            showProcessMessage(result && result.message ? result.message : "Unable to validate Pallet Pre-Dispatch.", "error");
            return false;
        } catch (error) {
            setSubmitStatus("invalid", "");
            showProcessMessage("Unable to validate Pallet Pre-Dispatch.", "error");
            return false;
        }
    }

    async function savePreDispatch(form) {
        var url = getFormUrl("data-save-url");
        var submitButton = form ? form.querySelector("[type='submit']") : null;

        if (!url || !form || !window.fetch) {
            showProcessMessage("Unable to save Pallet Pre-Dispatch.", "error");
            return;
        }

        if (submitButton) {
            submitButton.disabled = true;
        }

        setSubmitStatus("loading", "");

        try {
            var response = await fetch(url, {
                method: "POST",
                credentials: "same-origin",
                body: new FormData(form),
                headers: { "Accept": "application/json" }
            });
            var result = response.ok ? await response.json() : null;

            if (result && result.success) {
                showProcessMessage(result.message || "Pallet Pre-Dispatch Saved Successfully", "success");
                resetPreDispatchForm(form);
                return;
            }

            showProcessMessage(result && result.message ? result.message : "Unable to save Pallet Pre-Dispatch.", "error");
        } catch (error) {
            showProcessMessage("Unable to save Pallet Pre-Dispatch.", "error");
        } finally {
            if (submitButton) {
                submitButton.disabled = false;
            }
        }
    }

    document.addEventListener("change", function (event) {
        var uploadInput = event.target.closest("[data-upload-input]");
        if (!uploadInput) {
            return;
        }

        var label = document.querySelector("[data-upload-label]");
        if (label) {
            label.textContent = uploadInput.files && uploadInput.files.length
                ? uploadInput.files[0].name
                : "UPLOAD";
        }
    });

    document.addEventListener("click", function (event) {
        if (event.target.closest("[data-predispatch-add]")) {
            validateMember();
            return;
        }

        if (event.target.closest("[data-vision-open]")) {
            openCamera();
            return;
        }

        if (event.target.closest("[data-vision-capture]")) {
            readTextFromCamera();
            return;
        }

        if (event.target.closest("[data-vision-close]")) {
            stopCamera();
        }
    });

    document.addEventListener("submit", async function (event) {
        var form = event.target && event.target.closest("[data-predispatch-form]");
        if (!form) {
            return;
        }

        event.preventDefault();
        if (await validateExistingPreDispatch({ showSuccess: false })) {
            await savePreDispatch(form);
        }
    });

    document.addEventListener("keydown", function (event) {
        if (event.key === "Escape") {
            stopCamera();
        }
    });

}());
