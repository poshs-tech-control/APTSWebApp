
'use strict';

// adding and removing invoice section
function addRemoveInvoiceItem() {

    var invoiceContainer = document.querySelector('.product-description-list');
    var invoiceEach = document.querySelector('.product-description-each')

    //remove product invoice
    function removeProductInvoice() {
        setInterval(() => {
            setTimeout(() => {
                var removeBtn = document.querySelectorAll('.delete-row-btn');
                for (let i = 0; i < removeBtn.length; i++) {
                    removeBtn[i].addEventListener('click', removeInvoice);
                }
            }, 1);
        }, 1);

        function removeInvoice($e) {
            invoiceContainer.removeChild($e.target.parentElement)
        }
    }
    removeProductInvoice();

    //add product invoice
    function addProductInvoice() {
        var invoiceEachCopy = invoiceEach.cloneNode(true);

        var addBtn = document.querySelector('.add-invoice-item-btn');
        var invoiceEachCopy = invoiceEach.cloneNode(true);

        addBtn.addEventListener('click', addInvoice);

        function addInvoice() {
            var newInvoice = invoiceEachCopy.cloneNode(true);
            invoiceContainer.appendChild(newInvoice);
            var genericExamples = document.querySelectorAll("[data-trigger]");
            for (let i = 0; i < genericExamples.length; ++i) {
                var element = genericExamples[i];
                new Choices(element, {
                  allowHTML: true,
                  searchPlaceholderValue: "Search",
                });
              }
        }
    }
    addProductInvoice();
}
addRemoveInvoiceItem();

//To choose date
flatpickr(".invoice-date", {});