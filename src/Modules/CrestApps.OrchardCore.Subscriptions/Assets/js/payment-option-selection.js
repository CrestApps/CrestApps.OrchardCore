// enablePayButtonButton should be set globally.
const enablePayButtonButton = (button, enable) => {

    const processing = button.querySelector('.payment-processing');
    const label = button.querySelector('.payment-label');

    if (enable) {
        button.disabled = false;
        button.classList.remove('disabled');

        if (processing) {
            processing.classList.add('d-none')
        }

        if (label) {
            label.classList.remove('d-none')
        }

    } else {
        button.disabled = true;
        button.classList.remove('disabled');

        if (processing) {
            processing.classList.remove('d-none')
        }

        if (label) {
            label.classList.add('d-none')
        }
    }
};

document.addEventListener('DOMContentLoaded', () => {

    const payButton = document.getElementById('Subscription_Next_Button');
    const wrappers = document.getElementsByClassName('payment-method-wrapper');
    const paymentMethodElements = document.querySelectorAll('input[name="PaymentMethod"]');

    if (paymentMethodElements.length < 2) {

        for (let i = 0; i < wrappers.length; i++) {
            wrappers[i].classList.remove('d-none');
        }

        if (wrappers.length > 0) {
            payButton.setAttribute('data-method-name', wrappers[0].getAttribute('data-method-name'));

        }

        return;
    }

    const handleWrappers = (element) => {
        if (element.checked) {
            payButton.setAttribute('data-method-name', element.value);
        }
        for (let i = 0; i < wrappers.length; i++) {
            let wrapper = wrappers[i];
            if (element.checked && wrapper.getAttribute('data-method-name') == element.value) {
                wrapper.classList.remove('d-none');
            } else {
                wrapper.classList.add('d-none');
            }
        }
    }

    for (let y = 0; y < paymentMethodElements.length; y++) {
        const paymentMethodElement = paymentMethodElements[y];
        paymentMethodElement.addEventListener("change", (element) => {
            handleWrappers(element.target);
        });

        paymentMethodElement.addEventListener("click", (element) => {
            handleWrappers(element.target);
        });

        if (paymentMethodElement.checked) {
            handleWrappers(paymentMethodElement);
        }
    }
});
