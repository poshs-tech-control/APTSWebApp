(function () {
    'use strict'

     /* mail editor */
     var toolbarOptions = [
        [{ 'header': [1, 2, 3, 4, 5, 6, false] }],
        [{ 'font': [] }],
        ['bold', 'italic', 'underline', 'strike'],        // toggled buttons
        ['blockquote', 'code-block'],

        [{ 'header': 1 }, { 'header': 2 }],               // custom button values
        [{ 'list': 'ordered' }, { 'list': 'bullet' }],

        [{ 'color': [] }, { 'background': [] }],          // dropdown with defaults from theme
        [{ 'align': [] }],

        ['image', 'video'],
        ['clean']                                         // remove formatting button
    ];

    var quill1 = new Quill('#mail-compose-editor', {
        modules: {
            toolbar: toolbarOptions
        },
        theme: 'snow'
    });

    /* to choices js */
    const multipleCancelButton = new Choices(
        '#toMail',
        {
            allowHTML: true,
            removeItemButton: true,
        });
})();

(function () {
    'use strict';

    let loadFile = function (event) {
        var reader = new FileReader();
        reader.onload = function () {
            var output = document.getElementById('profile-img');
            if (event.target.files[0].type.match('image.*')) {
                output.src = reader.result;
            } else {
                event.target.value = '';
                alert('please select a valid image');
            }
        };
        reader.readAsDataURL(event.target.files[0]);
    };
})();