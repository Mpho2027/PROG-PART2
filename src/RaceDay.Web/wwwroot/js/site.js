// RaceDay: small page behaviours shared by every page.

// Ask before destructive actions: <form data-confirm="Delete this event?">
document.addEventListener("submit", function (e) {
    var form = e.target;
    var message = form.getAttribute("data-confirm");
    if (message && !window.confirm(message)) {
        e.preventDefault();
    }
});

// Show a preview of a chosen image: <input type="file" data-preview="#imgId">
document.addEventListener("change", function (e) {
    var input = e.target;
    var target = input.getAttribute && input.getAttribute("data-preview");
    if (!target || !input.files || !input.files[0]) return;

    var file = input.files[0];
    var img = document.querySelector(target);
    if (!img || !file.type.startsWith("image/")) return;

    var reader = new FileReader();
    reader.onload = function (ev) {
        img.src = ev.target.result;
        img.classList.remove("d-none");
    };
    reader.readAsDataURL(file);
});

// Submit a form when a select changes: <select data-autosubmit>
document.addEventListener("change", function (e) {
    if (e.target.hasAttribute && e.target.hasAttribute("data-autosubmit")) {
        e.target.form.submit();
    }
});
