"use strict";

//To choose date
flatpickr(".invoice-date", {});

//disply textarea on click
function displayTextArea() {
  var textAreaElement = document.querySelector("#shipping-address"),
    addAddressBtn = document.querySelector("#addShippingAddress");

  addAddressBtn.addEventListener("click", () => {
    if (textAreaElement.classList.contains("d-none")) {
      textAreaElement.classList.remove("d-none");
    } else {
      textAreaElement.classList.add("d-none");
    }
    addAddressBtn.classList.add("d-none");
  });
}
displayTextArea();

//adding and removing invoice
function addRemoveInvoiceItem() {
  var invoiceContainer = document.querySelector(".product-description-list");
  var invoiceEach = document.querySelector(".product-description-each");

  //remove product invoice
  function removeProductInvoice() {
    setInterval(() => {
      setTimeout(() => {
        var removeBtn = document.querySelectorAll(".delete-row-btn");
        for (let i = 0; i < removeBtn.length; i++) {
          removeBtn[i].addEventListener("click", removeInvoice);
        }
      }, 1);
    }, 1);

    function removeInvoice($e) {
      invoiceContainer.removeChild($e.target.parentElement);
    }
  }
  removeProductInvoice();

  //add product invoice
  function addProductInvoice() {
    var invoiceEachCopy = invoiceEach.cloneNode(true);

    var addBtn = document.querySelector(".add-invoice-item-btn");

    addBtn.addEventListener("click", addInvoice);

    function addInvoice() {
      var newInvoice = invoiceEachCopy.cloneNode(true);
      invoiceContainer.appendChild(newInvoice);
    }
  }
  addProductInvoice();
}
addRemoveInvoiceItem();
