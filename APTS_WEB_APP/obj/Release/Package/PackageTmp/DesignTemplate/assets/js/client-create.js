function showPassword(){
    const showPasswordBtn = document.querySelector('.show-password');
    const inputMain = document.querySelector('.input-password');
    const eyeOpenIcon = document.querySelector('.eye-open');
    const eyeCloseIcon = document.querySelector('.eye-close');

    showPasswordBtn.addEventListener('click', changeInputType);

    function changeInputType(){

        if(inputMain.type === "password"){
            inputMain.type = "text";
            eyeCloseIcon.classList.remove('d-none');
            eyeOpenIcon.classList.add('d-none');
        }
        else{
            inputMain.type = "password";
            eyeCloseIcon.classList.add('d-none');
            eyeOpenIcon.classList.remove('d-none');
        }
    }
}
showPassword();