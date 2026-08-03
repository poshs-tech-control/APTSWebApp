(function () {
    "use strict"

     // for wishlist items delete
     document.querySelectorAll(".btn-delete1").forEach((element) => {
        let i = document.querySelectorAll(".btn-delete1").length

        element.onclick = () => {
            Swal.fire({
                title: 'Are you sure?',
                text: "You won't be able to revert this!",
                icon: 'warning',
                showCancelButton: true,
                confirmButtonColor: '#3085d6',
                cancelButtonColor: '#d33',
                confirmButtonText: 'Yes, delete it!'
            }).then((result) => {
                if (result.isConfirmed) {
                    Swal.fire(
                        'Deleted!',
                        'Your file has been deleted.',
                        'success'
                    )
                    element.parentElement.parentElement.remove()
                    i = document.querySelectorAll(".btn-delete1").length
                    if (i < 1) {
                        document.getElementById("wishlist-container-delete").remove()
                        document.getElementById("wishlist-empty-wishlist").classList.remove("d-none")
                    }
                }
            })
        }
    })
})();