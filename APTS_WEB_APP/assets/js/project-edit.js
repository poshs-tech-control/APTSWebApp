(function () {
    'use strict';
    var toolbarOptions = [
        [{ 'header': [1, 2, 3, 4, 5, 6, false] }],
        [{ 'font': [] }],
        ['bold', 'italic', 'underline', 'strike'],        // toggled buttons
        ['blockquote', 'code-block'],

        [{ 'header': 1 }, { 'header': 2 }],               // custom button values
        [{ 'list': 'ordered' }, { 'list': 'bullet' }],
        [{ 'script': 'sub' }, { 'script': 'super' }],      // superscript/subscript
        [{ 'indent': '-1' }, { 'indent': '+1' }],          // outdent/indent
        [{ 'direction': 'rtl' }],                         // text direction

        [{ 'size': ['small', false, 'large', 'huge'] }],  // custom dropdown

        [{ 'color': [] }, { 'background': [] }],          // dropdown with defaults from theme
        [{ 'align': [] }],

        ['image', 'video'],
        ['clean']                                         // remove formatting button
    ];
    var quill = new Quill('#editor', {
        modules: {
            toolbar: toolbarOptions
        },
        theme: 'snow'
    });
    var quill = new Quill('#editor2', {
        modules: {
            toolbar: toolbarOptions
        },
        theme: 'snow'
    });

})();

//removing end date with checkbox
const endDateCheckboxContainer = document.querySelector('.end-date-checkbox-container');
const endDateCheckbox = document.querySelector('.end-date-checkbox');
const endDateContainer = document.querySelector('.end-date-container');
removeElementsOnCheck(endDateCheckboxContainer, endDateCheckbox, endDateContainer);

//hide elements using checkbox
function removeElementsOnCheck(checkboxContainer, checkboxMain, elementToRemove) {

    checkboxContainer.addEventListener('click', mainFunction);

    function mainFunction() {
        if (checkboxMain.checked == true) {
            elementToRemove.classList.add('d-none');
        }
        else {
            elementToRemove.classList.remove('d-none');
        }
    }
}

//display elements using checkbox
function addElementsOnCheck(checkboxContainer, checkboxMain, elementToRemove) {

    checkboxContainer.addEventListener('click', mainFunction);

    function mainFunction() {
        if (checkboxMain.checked == true) {
            elementToRemove.classList.remove('d-none');
        }
        else {
            elementToRemove.classList.add('d-none');
        }
    }
}

function rotateIcon(icon) {
    icon.classList.toggle('rotated');
}
//To choose date
flatpickr(".date-input", {});

/* dropzone */
let myDropzone = new Dropzone(".dropzone");
    myDropzone.on("addedfile", file => {
});